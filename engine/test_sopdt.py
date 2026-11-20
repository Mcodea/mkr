import base64
from datetime import datetime, timedelta, timezone
import math
import unittest

import numpy as np

from fopdt import identify as fopdt
from sopdt import basis, identify


def source(day, slow, fast, amplitude=2, delay=1.4):
    start = datetime(2026, 3, day, tzinfo=timezone.utc)
    rows = ["t;u;y"]
    for i in range(115):
        t = i * 0.5
        x = max(0, t - 4 - delay)
        if slow == fast:
            shape = 1 - (1 + x / slow) * math.exp(-x / slow)
        else:
            shape = 1 - (slow * math.exp(-x / slow) - fast * math.exp(-x / fast)) / (slow - fast)
        rows.append(f"{(start + timedelta(seconds=t)).isoformat()};{amplitude if t >= 4 else 0};{5 - 3 * amplitude * shape}")
    return dict(raw=base64.b64encode("\n".join(rows).encode()).decode(), delimiter=";",
                timeColumn="t", mvColumn="u", pvColumn="y")


class SopdtTests(unittest.TestCase):
    def test_two_poles_and_independent_prediction(self):
        payload = dict(training=source(1, 5, 2), validation=source(2, 5, 2, -1.5),
                       mvTolerance=0.01, pvTolerance=0.01)
        report = identify(payload)
        self.assertAlmostEqual(report["gain"], -3, places=5)
        self.assertAlmostEqual(report["tauSeconds"], 5, places=5)
        self.assertAlmostEqual(report["secondTauSeconds"], 2, places=5)
        self.assertAlmostEqual(report["delaySeconds"], 1.4, places=5)
        self.assertLess(report["validation"]["rmse"], 1e-6)
        self.assertGreater(fopdt(payload)["validation"]["rmse"], 0.01)
        drift = identify({**payload, "validation": source(2, 8, 2, -1.5)})
        for key in ("gain", "tauSeconds", "secondTauSeconds", "delaySeconds"):
            self.assertEqual(drift[key], report[key])
        self.assertGreater(drift["validation"]["rmse"], 0.2)

    def test_repeated_pole_limit(self):
        x = np.linspace(0, 50, 500)
        expected = 1 - (1 + x / 4) * np.exp(-x / 4)
        np.testing.assert_allclose(basis(x, 4, 4, 0), expected, atol=3e-16)
        np.testing.assert_allclose(basis(x, 4, 4 - 1e-10, 0), expected, atol=9e-12)
        report = identify(dict(training=source(1, 4, 4), validation=source(2, 4, 4),
                               mvTolerance=0.01, pvTolerance=0.01))
        self.assertIn("close_poles", report["warnings"])
        self.assertLess(report["validation"]["rmse"], 1e-6)


if __name__ == "__main__":
    unittest.main()
