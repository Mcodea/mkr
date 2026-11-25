"""Regression from a versioned trial to an identified, tuned and simulated loop."""
import base64
from datetime import datetime, timedelta, timezone
import math
from pathlib import Path
import unittest

from fopdt import identify
from pi_tuning import calculate as tune
from pi_simulation import calculate as simulate


def trial(raw):
    return dict(raw=base64.b64encode(raw).decode(), delimiter=",",
                timeColumn="timestamp", mvColumn="MV", pvColumn="PV")


class ScientificWorkflowTests(unittest.TestCase):
    def test_reverse_action_and_fractional_sample_delay(self):
        path = Path(__file__).resolve().parents[1] / "samples/synthetic/negative_gain.csv"
        start = datetime(2026, 2, 1, tzinfo=timezone.utc)
        rows = ["timestamp,MV,PV"]
        for index in range(200):
            elapsed = index * 0.8
            mv = -1.5 if index >= 10 else 0
            pv = 3 * (1 - math.exp(-max(0, elapsed-12)/12))
            rows.append(f"{(start+timedelta(seconds=elapsed)).isoformat()},{mv},{pv}")
        model = identify(dict(training=trial(path.read_bytes()), validation=trial("\n".join(rows).encode()),
                              mvTolerance=0.001, pvTolerance=0.001))
        # Training PV is rounded to nine decimal places. Allow 1e-6 for fitting.
        for key, truth in (("gain", -2), ("tauSeconds", 12), ("delaySeconds", 4)):
            self.assertAlmostEqual(model[key], truth, delta=1e-6)
        self.assertLess(model["validation"]["rmse"], 1e-6)
        plant = {key: model[key] for key in ("gain", "tauSeconds", "delaySeconds")}
        tuning = tune({**plant, "responseSeconds": 8})
        controllers = {method: {key: tuning[method][key] for key in ("kp", "tiSeconds")}
                       for method in ("imc", "simc")}
        run = simulate(dict(**plant, **controllers, sampleSeconds=0.7, durationSeconds=21,
                            setpoint=100, minimumMv=-1, maximumMv=1,
                            disturbance=0, disturbanceAtSeconds=0))["simc"]
        # A large setpoint keeps MV clamped at -1 throughout this short trial.
        for point in run["points"]:
            exact = 2 * (1-math.exp(-max(0, point["seconds"]-4)/12))
            self.assertAlmostEqual(point["pv"], exact, delta=1e-6)
            if point["mv"] is not None:
                self.assertEqual(point["mv"], -1)
