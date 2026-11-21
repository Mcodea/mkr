"""Load versioned manufacturer evidence and filter explicitly documented targets."""
import json
from pathlib import Path


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


def convert(payload):
    if set(payload) != {"name", "target", "parameters"} or not isinstance(payload["target"], dict):
        raise ValueError("catalogue_conversion_input")
    result = lookup({**payload["target"], "name": payload["name"]})
    return dict(catalogueVersion=result["catalogueVersion"], converted=False,
                reason="no_verified_adapter" if result["entries"] else "no_matching_entry",
                parameters=None)


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
