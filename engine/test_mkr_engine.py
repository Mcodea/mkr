import json
from pathlib import Path
import subprocess
import sys
import unittest

from unittest import mock

from mkr_engine import respond, serve


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
            "version": 1, "requestId": "job-42", "engineVersion": "0.10.0", "result": {"status": "ready"},
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


class SafetyNet(unittest.TestCase):
    def reply(self, operation="check_model_drift", request_id="job-7"):
        raw = json.dumps(dict(version=1, requestId=request_id, operation=operation, payload={})).encode()
        return json.loads(serve(raw))

    def test_an_unexpected_exception_becomes_a_coded_error_for_the_right_request(self):
        with mock.patch("model_drift.check", side_effect=ZeroDivisionError("boom")):
            reply = self.reply()
        self.assertEqual((reply["error"]["code"], reply["requestId"], reply["version"]), ("engine_failure", "job-7", 1))
        self.assertNotIn("boom", json.dumps(reply))                      # no internal detail leaks

    def test_a_non_finite_number_in_a_result_becomes_a_coded_error(self):
        with mock.patch("model_drift.check", return_value={"x": float("nan")}):
            reply = self.reply()
        self.assertEqual((reply["error"]["code"], reply["requestId"]), ("engine_non_finite", "job-7"))

    def test_ordinary_replies_are_unchanged_and_garbage_input_is_still_refused(self):
        self.assertEqual(self.reply("ping")["result"], {"status": "ready"})
        self.assertEqual(json.loads(serve(b"pas du json"))["error"]["code"], "invalid_json")

    def test_the_process_never_ends_with_a_traceback(self):
        script = Path(__file__).with_name("mkr_engine.py")
        run = subprocess.run([sys.executable, "-I", str(script)], input=b"\xff\xfe", capture_output=True, timeout=30)
        self.assertEqual(run.returncode, 0)
        self.assertEqual(json.loads(run.stdout)["error"]["code"], "invalid_json")


if __name__ == "__main__":
    unittest.main()
