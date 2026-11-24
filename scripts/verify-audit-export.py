#!/usr/bin/env python3
"""Verify a MKR audit export (JSON Lines) WITHOUT MKR: recompute the SHA-256 chain independently.

Usage: python3 -I verify-audit-export.py EXPORT_FILE [EXPECTED_HEAD_HASH]
Exit code 0: chain intact (and head hash equal to EXPECTED_HEAD_HASH if given). 1: broken or inconsistent. 2: usage or unreadable file.
Fields are length-prefixed in UTF-16 code units, exactly as the .NET implementation does (an emoji counts for 2).
"""
import hashlib
import json
import sys

GENESIS = "0" * 64


def field(value):
    return f"{len(value.encode('utf-16-le')) // 2}:{value}"


def entry_hash(project_id, entry, previous):
    text = "|".join(field(v) for v in (project_id, str(entry["sequence"]), entry["at"], entry["actor"], entry["action"],
                                         entry["subject"], entry["detail"], previous))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def verify(lines, expected_head=None):
    header = json.loads(lines[0])
    if header.get("format") != "mkr-audit-export-v1":
        return False, "format inconnu"
    previous = GENESIS
    for index, line in enumerate(lines[1:], start=1):
        entry = json.loads(line)
        if entry["sequence"] != index or entry["previousHash"] != previous or entry["hash"] != entry_hash(header["projectId"], entry, previous):
            return False, f"chaîne rompue à l'entrée {index}"
        previous = entry["hash"]
    if header["entries"] != len(lines) - 1 or header["headHash"] != previous:
        return False, "en-tête incohérent avec les entrées"
    if expected_head is not None and previous != expected_head.lower():
        return False, f"empreinte de tête {previous} différente de l'attendue"
    return True, f"chaîne intacte : {len(lines) - 1} entrée(s), empreinte de tête {previous}"


def main(argv):
    if len(argv) not in (2, 3):
        print(__doc__)
        return 2
    try:
        with open(argv[1], encoding="utf-8") as handle:
            lines = [line for line in handle.read().split("\n") if line]
        if not lines:
            print("fichier vide")
            return 1
        ok, message = verify(lines, argv[2] if len(argv) == 3 else None)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"fichier illisible ou mal formé : {error}")
        return 2 if isinstance(error, OSError) else 1
    print(message)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
