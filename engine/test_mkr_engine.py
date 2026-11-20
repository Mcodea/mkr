import json
from pathlib import Path
import subprocess
import sys
import unittest

from mkr_engine import respond


class ContractTests(unittest.TestCase):
    def test_process_round_trip(self):
        request = {"version": 1, "requestId": "job-42", "operation": "ping", "payload": {}}
        result = subprocess.run(
            [sys.executable, "-I", str(Path(__file__).with_name("mkr_engine.py"))],
            input=json.dumps(request).encode(),
            capture_output=True, check=True, timeout=5,
        )
        self.assertEqual(result.stderr, b"")
        self.assertEqual(json.loads(result.stdout), {
            "version": 1, "requestId": "job-42", "engineVersion": "0.5.0", "result": {"status": "ready"},
        })

    def test_invalid_json(self):
        for raw in (b"{", b"\xff", b'{"version":1,"version":1}', b" " * 2_900_001):
            with self.subTest(raw=raw[:64]):
                self.assertEqual(respond(raw)["error"]["code"], "invalid_json")

    def test_invalid_request(self):
        for raw in (
            b"null",
            b'{"version":true,"requestId":"x","operation":"ping","payload":{}}',
            b'{"version":1,"requestId":"a.b","operation":"ping","payload":{}}',
            b'{"version":1,"requestId":"x","operation":"ping","payload":{"value":2}}',
            b'{"version":1,"requestId":"x","operation":"ping","payload":{},"extra":1}',
        ):
            with self.subTest(raw=raw):
                self.assertEqual(respond(raw)["error"]["code"], "invalid_request")

    def test_unsupported_version_and_operation(self):
        for version, operation, code in ((2, "ping", "unsupported_version"), (1, "eval", "unsupported_operation")):
            with self.subTest(code=code):
                request = {"version": version, "requestId": "abc", "operation": operation, "payload": {}}
                result = respond(json.dumps(request).encode())
                self.assertEqual(result["requestId"], "abc")
                self.assertEqual(result["error"]["code"], code)


if __name__ == "__main__":
    unittest.main()
