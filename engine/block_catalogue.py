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
        if not all(fact["source"] in references for fact in block["facts"]):
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
