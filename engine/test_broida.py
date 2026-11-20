"""Broïda 28%/40% regression tests on disjoint, traceable step trials."""
import base64
import json
import math
from datetime import datetime, timedelta, timezone
import unittest

from broida import identify, crossing
from mkr_engine import respond
from test_fopdt import source


def fit(training=None, validation=None):
    return identify(dict(
        training=training or source(1, cadence=.5),
        validation=validation or source(2, amplitude=-1.5, cadence=.75),
        mvTolerance=.01, pvTolerance=.01))


def edit_source(src, fn):
    lines = base64.b64decode(src["raw"]).decode().splitlines()
    for i, line in enumerate(lines[1:]):
        row = line.split(",")
        fn(i, row)
        lines[i+1] = ",".join(row)
    return src | {"raw": base64.b64encode("\n".join(lines).encode()).decode()}


class BroidaTests(unittest.TestCase):
    def test_synthetic_negative_gain_and_validation(self):
        result = fit()
        self.assertAlmostEqual(result["gain"], -3, delta=.025)
        self.assertAlmostEqual(result["tauSeconds"], 4, delta=.25)
        self.assertAlmostEqual(result["delaySeconds"], 2.5, delta=.15)
        evidence = result["broidaEvidence"]
        self.assertGreater(evidence["crossing28Seconds"], 0)
        self.assertGreater(evidence["crossing40Seconds"], evidence["crossing28Seconds"])
        self.assertEqual(evidence["thresholdFractions"], [.28, .40])
        self.assertEqual(evidence["terminalSamples"], 6)
        self.assertLess(result["training"]["rmse"], .1)
        self.assertLess(result["validation"]["rmse"], .1)
        self.assertIsNone(result["standardErrors"])

    def test_validation_does_not_leak_into_estimated_parameters(self):
        a = fit()
        b = fit(validation=source(2, gain=-1, amplitude=-1.5, cadence=.75))
        for field in ("gain", "tauSeconds", "delaySeconds", "broidaEvidence"):
            self.assertEqual(a[field], b[field])
        self.assertGreater(b["validation"]["rmse"], a["validation"]["rmse"] + .5)

    def test_positive_step_and_positive_process_gain(self):
        report = fit(training=source(1, gain=2, amplitude=2, cadence=.5),
                     validation=source(2, gain=2, amplitude=-1.5, cadence=.75))
        self.assertAlmostEqual(report["gain"], 2, delta=.02)
        self.assertGreater(report["tauSeconds"], 0)

    def test_reject_nonstationary_terminal_measurements(self):
        train = edit_source(source(1, cadence=.5),
                            lambda i, cells: cells.__setitem__(2, str(float(cells[2]) +
                                (0.25 if i == 69 else 0))))
        with self.assertRaisesRegex(ValueError, "broida_plateau"):
            fit(training=train)

    def test_reject_large_nonmonotone_output(self):
        train = edit_source(source(1, cadence=.5),
                            lambda i, cells: cells.__setitem__(2,
                                str(float(cells[2]) + 1) if i == 20 else cells[2]))
        with self.assertRaisesRegex(ValueError, "broida_shape"):
            fit(training=train)

    def test_reject_short_horizon(self):
        with self.assertRaisesRegex(ValueError, "broida_plateau|broida_horizon"):
            fit(training=source(1, tau=20, delay=2, cadence=.5))

    def test_reject_no_step_or_overlapping_imports(self):
        with self.assertRaisesRegex(ValueError, "fit_step"):
            fit(training=source(1, amplitude=0, cadence=.5))
        with self.assertRaisesRegex(ValueError, "fit_independence"):
            fit(validation=source(1, amplitude=-1.5, cadence=.75))

    def test_crossing_interpolation_is_not_nearest_sample(self):
        import numpy as np
        self.assertAlmostEqual(
            crossing(np.array([0., 1., 2.]), np.array([0., .2, .6]), .28),
            1.2, delta=1e-12)

    def test_legacy_fopdt_operation_is_unmodified(self):
        from fopdt import identify as original
        payload = dict(training=source(1), validation=source(2, amplitude=-1.5),
                       mvTolerance=.01, pvTolerance=.01)
        legacy = original(payload)
        self.assertAlmostEqual(legacy["gain"], -3, places=4)
        self.assertNotIn("broidaEvidence", legacy)

    def test_ipc_reports_evidence_and_rejection_code(self):
        request = dict(version=1, requestId="broida-test", operation="identify_broida",
                       payload=dict(training=source(1, cadence=.5),
                                    validation=source(2, amplitude=-1.5, cadence=.75),
                                    mvTolerance=.01, pvTolerance=.01))
        response = respond(json.dumps(request).encode())
        self.assertNotIn("error", response)
        self.assertEqual(response["result"]["algorithmVersion"], "1.0.0")
        self.assertIn("crossing28Seconds", response["result"]["broidaEvidence"])
        request["payload"]["training"] = source(1, tau=20, delay=2, cadence=.5)
        invalid = respond(json.dumps(request).encode())
        self.assertTrue(invalid["error"]["code"].startswith("broida_"))


if __name__ == "__main__":
    unittest.main()
