import base64
from datetime import datetime, timedelta, timezone
import json
import unittest

from mkr_engine import respond

from model_comparison import compare
from test_fopdt import source as first_order
from test_sopdt import source as second_order


def compare_trials(training, validation):
    return compare(dict(trials=dict(training=training, validation=validation,
                                    mvTolerance=0.01, pvTolerance=0.01),
                        maximumRmse=0.001, maximumResidualLagOne=0.5))


class ComparisonTests(unittest.TestCase):
    def test_parsimony_on_single_pole(self):
        self.assertEqual(compare_trials(first_order(1), first_order(2))["selected"], "FOPDT")

    def test_two_resolvable_poles(self):
        self.assertEqual(compare_trials(second_order(1, 5, 2), second_order(2, 5, 2))["selected"], "SOPDT")

    def test_good_training_fit_cannot_override_validation_drift(self):
        result = compare_trials(second_order(1, 5, 2), second_order(2, 8, 2))
        self.assertEqual(result["decision"], "insufficient_evidence")
        self.assertIsNone(result["selected"])

    def test_reexported_recording_refused_through_ipc(self):
        original = first_order(1)
        _, *rows = base64.b64decode(original["raw"]).decode().splitlines()
        exported = ["PV;MV;heure"]
        local_zone = timezone(timedelta(hours=1))
        for row in rows:
            stamp, mv, pv = row.split(",")
            same_instant = datetime.fromisoformat(stamp).astimezone(local_zone)
            exported.append(f"{pv};{mv};{same_instant.isoformat()}")
        validation = dict(raw=base64.b64encode("\r\n".join(exported).encode()).decode(),
                          delimiter=";", timeColumn="heure", pvColumn="PV", mvColumn="MV")
        self.assertNotEqual(original["raw"], validation["raw"])
        trials = dict(training=original, validation=validation, mvTolerance=0.01, pvTolerance=0.01)
        for operation in ("identify_fopdt", "identify_sopdt", "identify_integrator", "identify_broida", "compare_models"):
            with self.subTest(operation=operation):
                payload = (dict(trials=trials, maximumRmse=0.001, maximumResidualLagOne=0.5)
                           if operation == "compare_models" else trials)
                reply = respond(json.dumps(dict(version=1, requestId="duplicate", operation=operation,
                                                payload=payload)).encode())
                self.assertEqual(reply.get("error"), {"code": "fit_independence"})
                self.assertNotIn("result", reply)
