"""Load versioned manufacturer evidence and filter explicitly documented targets."""
import json
from pathlib import Path


from block_adapters import pid_compact, pid_int, pidff

ADAPTERS = {"PIDFF": pidff, "PID_INT": pid_int, "PID_Compact": pid_compact}
TARGET = ("manufacturer", "cpu", "firmware", "software", "softwareVersion", "library", "libraryVersion")


def read_catalogue():
    path = Path(__file__).parent / "catalogue" / "schneider.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    if data["schemaVersion"] != 1:
        raise ValueError("catalogue_version")
    references = {source["id"] for source in data["sources"]}
    for block in data["blocks"]:
        if not all(fact["source"] in references for fact in block["facts"] + block["parameters"]):
            raise ValueError("catalogue_reference")
    return data


def lookup(payload):
    if payload.keys() - {*TARGET, "name"} or any(not isinstance(v, str) or not v.strip() for v in payload.values()):
        raise ValueError("catalogue_filter")
    data = read_catalogue()
    entries = []
    for block in data["blocks"]:
        if any(block[key] is not None and block[key] != value for key, value in payload.items()):
            continue
        missing = [key for key in TARGET if block[key] is None]
        entries.append(dict(**block, unverifiedTargetFields=missing, selectable=(
            block["status"] == "verified" and not missing and bool(block["equation"])
            and bool(block["parameters"]) and bool(block["facts"])
            and all(key in payload for key in TARGET)
        )))
    return dict(schemaVersion=data["schemaVersion"], catalogueVersion=data["catalogueVersion"],
                entries=entries, sources=data["sources"])


def _same_library_version(declared, given):
    """A catalogue family such as "V3.x" covers "V3.0"; otherwise the versions must be identical."""
    if not isinstance(declared, str) or not isinstance(given, str):
        return False
    if declared.endswith(".x"):
        return given.split(".")[0] == declared.split(".")[0]
    return declared == given


def convert(payload):
    """Documented conversion when an adapter exists for the block; otherwise the conversion is refused."""
    if set(payload) != {"name", "target", "parameters"} or not isinstance(payload["target"], dict):
        raise ValueError("catalogue_conversion_input")
    result = lookup({**payload["target"], "name": payload["name"]})
    refusal = dict(catalogueVersion=result["catalogueVersion"], converted=False, parameters=None,
                   reason="no_verified_adapter" if result["entries"] else "no_matching_entry")
    adapter = ADAPTERS.get(payload["name"])
    if adapter is None or not result["entries"]:
        return refusal
    # The library version of the declared target must be the one the parameters are given for (a V3.x target never receives V2.x parameters).
    declared_version = payload["target"].get("libraryVersion")
    given_version = payload["parameters"].get("version") if isinstance(payload["parameters"], dict) else None
    if declared_version is not None and given_version is not None and not _same_library_version(declared_version, given_version):
        return {**refusal, "reason": "conversion_version_mismatch"}
    try:
        converted = adapter(payload["parameters"])
    except ValueError as error:
        return {**refusal, "reason": str(error)}
    # A target field the catalogue could not check is UNVERIFIED, never "compatible": the verdict is explicit and travels with the conversion
    # (LOT 0b, P0-7). `verified_target` needs a catalogue entry verified for every field of the declared target.
    unverified = sorted({field for entry in result["entries"] for field in entry["unverifiedTargetFields"]} | {field for field in TARGET if field not in payload["target"]})
    verified = any(entry["selectable"] for entry in result["entries"])
    compatibility = dict(status="verified_target" if verified else "not_verified", unverifiedTargetFields=unverified,
                         targetProvided=sorted(payload["target"]))
    if not verified:
        converted = {**converted, "warnings": list(converted.get("warnings", [])) + ["hardware_compatibility_not_verified"]}
    return dict(catalogueVersion=result["catalogueVersion"], converted=True, reason=None, compatibility=compatibility,
                parameters=converted["parameters"], conversion=converted)


def recommend(payload):
    """Return a recommendation only for an exactly verified target and function set."""
    if set(payload) != {"target", "strategy", "functions", "studyId", "modelRunId"}:
        raise ValueError("catalogue_recommendation_input")
    target, strategy, functions = payload["target"], payload["strategy"], payload["functions"]
    if (not isinstance(target, dict) or target.keys() - set(TARGET)
            or strategy not in ("P", "PI", "PID") or not isinstance(functions, list)
            or any(not isinstance(item, str) or not item.strip() for item in functions)
            or any(not isinstance(payload[key], str) or not payload[key].strip()
                   for key in ("studyId", "modelRunId"))):
        raise ValueError("catalogue_recommendation_input")
    result = lookup(target)
    evaluated, eligible = [], []
    for entry in result["entries"]:
        missing = sorted(set(functions) - set(entry.get("verifiedFunctions", [])))
        reasons = []
        if not entry["selectable"]:
            reasons.append("target_or_block_not_verified")
        if strategy not in entry.get("verifiedStrategies", []):
            reasons.append("strategy_not_verified")
        if missing:
            reasons.append("functions_not_verified")
        evaluated.append(dict(name=entry["name"], reasons=reasons, missingFunctions=missing,
                              unknownTargetFields=entry["unverifiedTargetFields"], evidence=entry["facts"]))
        if not reasons:
            eligible.append(entry["name"])
    return dict(catalogueVersion=result["catalogueVersion"], studyId=payload["studyId"],
                modelRunId=payload["modelRunId"], requiredStrategy=strategy, target=target,
                requiredFunctions=functions, evaluated=evaluated, eligible=eligible,
                decision="compatible_candidates" if eligible else "insufficient_evidence")
