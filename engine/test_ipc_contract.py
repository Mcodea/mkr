"""LOT 1 : le contrat IPC est un objet vérifié, pas une prose qui dérive.

(1) Toute opération servie par mkr_engine.respond figure dans OPERATIONS, et inversement (analyse du source : une opération ajoutée sans être
listée fait échouer ce test). (2) Chaque opération est décrite dans docs/communication/ (ipc-v1.md ou ipc-operations.md). (3) describe_contract
renvoie exactement cette liste. Critères fixés a priori ; aucun n'est ajusté pour passer.
"""
import json
import re
import unittest
from pathlib import Path

from mkr_engine import CONTRACT_REVISION, OPERATIONS, respond

ENGINE = Path(__file__).resolve().parent
DOCS = ENGINE.parent / "docs" / "communication"


def served_operations():
    source = (ENGINE / "mkr_engine.py").read_text(encoding="utf-8")
    dispatch = source[source.index("def respond"):]
    names = set(re.findall(r'request\["operation"\]\s*==\s*"([a-z_]+)"', dispatch))
    for group in re.findall(r'request\["operation"\]\s+(?:not\s+)?in\s+\(([^)]*)\)', dispatch):
        names |= set(re.findall(r'"([a-z_]+)"', group))
    return names


class IpcContract(unittest.TestCase):
    def test_operations_constant_matches_what_the_engine_serves(self):
        self.assertEqual(served_operations() - {"__none__"}, set(OPERATIONS))
        self.assertEqual(len(OPERATIONS), len(set(OPERATIONS)))
        self.assertEqual(list(OPERATIONS), sorted(OPERATIONS))

    def test_every_operation_is_documented(self):
        text = "".join(path.read_text(encoding="utf-8") for path in DOCS.glob("ipc-*.md"))
        missing = [name for name in OPERATIONS if f"`{name}`" not in text]
        self.assertEqual(missing, [], f"opérations non documentées dans docs/communication : {missing}")

    def test_describe_contract_reports_the_same_list(self):
        reply = respond(json.dumps(dict(version=1, requestId="c-1", operation="describe_contract", payload={})).encode())
        self.assertEqual(reply["result"], dict(protocolVersion=1, contractRevision=CONTRACT_REVISION, operations=list(OPERATIONS)))

    def test_describe_contract_refuses_a_payload(self):
        reply = respond(json.dumps(dict(version=1, requestId="c-2", operation="describe_contract", payload={"x": 1})).encode())
        self.assertEqual(reply["error"]["code"], "invalid_request")


if __name__ == "__main__":
    unittest.main()
