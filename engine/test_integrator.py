import base64
from datetime import datetime, timedelta, timezone
import unittest

import numpy as np

from integrator import identify, profile_delay


def source(day, amplitude, response, cadence=1):
    start = datetime(2026, 4, day, tzinfo=timezone.utc)
    lines = ["ts,mv,pv"]
    for i in range(70):
        t = (i - 6) * cadence
        u = amplitude if t >= 0 else 0
        stamp = (start + timedelta(seconds=i * cadence)).isoformat()
        lines.append(f"{stamp},{u},{7 + amplitude * response(t)}")
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",",
                timeColumn="ts", mvColumn="mv", pvColumn="pv")


class IntegratorTests(unittest.TestCase):
    def test_disjoint_signed_validation(self):
        def ramp(t):
            return -0.12 * max(t - 2.5, 0)
        training = source(1, 2, ramp, cadence=0.75)
        validation = source(2, -1.5, ramp)
        payload = dict(training=training, validation=validation, mvTolerance=0.01, pvTolerance=0.01)
        result = identify(payload)
        self.assertAlmostEqual(result["gain"], -0.12, places=8)
        self.assertAlmostEqual(result["delaySeconds"], 2.5, places=8)
        self.assertLess(result["validation"]["rmse"], 1e-9)
        reversed_behavior = identify({**payload, "validation": source(2, -1.5, lambda t: -ramp(t))})
        self.assertEqual(reversed_behavior["gain"], result["gain"])
        self.assertIn("integrator_validation_tail", reversed_behavior["warnings"])

    def test_rejects_plateau_in_training(self):
        def plateau(t):
            return min(5, max(t - 2.5, 0))
        payload = dict(training=source(1, 2, plateau), validation=source(2, -1.5, plateau),
                       mvTolerance=0.01, pvTolerance=0.01)
        with self.assertRaisesRegex(ValueError, "fit_integrator_tail"):
            identify(payload)

    def test_profile_no_worse_than_independent_grid(self):
        t = np.linspace(0, 1, 43)
        y = 2.4 * np.maximum(t - 0.17, 0) + 0.2 * np.sin(20 * t) + 0.02
        delay, gain = profile_delay(t, y)
        error = np.linalg.norm(y - gain * np.maximum(t - delay, 0))
        for other in np.linspace(0, 0.5, 1001):
            a = np.maximum(t - other, 0)
            fitted = a * np.linalg.lstsq(a.reshape(-1, 1), y, rcond=None)[0][0]
            self.assertLessEqual(error, np.linalg.norm(y - fitted) + 1e-12)


if __name__ == "__main__":
    unittest.main()
