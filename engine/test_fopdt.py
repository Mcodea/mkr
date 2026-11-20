import base64
from datetime import datetime, timedelta, timezone
import math
import unittest

from fopdt import identify


def source(day, gain=-3, tau=4, delay=2.5, amplitude=2, change=None, cadence=1):
    start = datetime(2026, 1, day, tzinfo=timezone.utc)
    lines = ["timestamp,mv,pv"]
    for i in range(70):
        t = (i - 6) * cadence
        value = 7 + gain * amplitude * (1 - math.exp(-max(t - delay, 0) / tau))
        stamp = (start + timedelta(seconds=i * cadence)).isoformat()
        cells = [stamp, str(amplitude if t >= 0 else 0), str(value)]
        if change:
            change(i, cells)
        lines.append(",".join(cells))
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",",
                timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


def calculate(training=None, validation=None):
    return identify(dict(training=training or source(1),
                         validation=validation or source(2, amplitude=-1.5),
                         mvTolerance=0.01, pvTolerance=0.01))


class FopdtTests(unittest.TestCase):
    def test_analytic_fit_and_independent_reverse_step(self):
        report = calculate(source(1, cadence=0.75), source(2, amplitude=-1.5, cadence=1.25))
        self.assertAlmostEqual(report["gain"], -3, places=5)
        self.assertAlmostEqual(report["tauSeconds"], 4, places=5)
        self.assertAlmostEqual(report["delaySeconds"], 2.5, places=5)
        self.assertLess(report["validation"]["rmse"], 1e-6)
        self.assertEqual(report["validation"]["samples"], 64)

    def test_validation_changes_diagnostics_only(self):
        original = calculate()
        drifted = calculate(validation=source(2, gain=-1, amplitude=2))
        for field in ("gain", "tauSeconds", "delaySeconds"):
            self.assertEqual(original[field], drifted[field])
        self.assertGreater(drifted["validation"]["rmse"], 2)

    def test_zero_delay_is_resolution_limited(self):
        report = calculate(source(1, delay=0), source(2, delay=0))
        self.assertLess(report["delaySeconds"], 1e-5)
        self.assertIn("delay_resolution", report["warnings"])

    def test_rejects_overlap_incompatible_amplitudes_and_bad_inputs(self):
        with self.assertRaisesRegex(ValueError, "fit_independence"):
            calculate(validation=source(1, gain=-2))
        with self.assertRaisesRegex(ValueError, "fit_amplitude"):
            calculate(validation=source(2, amplitude=10))
        cases = [
            ("fit_step", 0, lambda i, c: None),  # No input excitation.
            ("fit_time", 2, lambda i, c: c.__setitem__(0, "2026-01-01T00:00:00Z") if i == 1 else None),
            ("fit_step", 2, lambda i, c: c.__setitem__(1, "3") if i > 20 else None),
            ("fit_baseline", 2, lambda i, c: c.__setitem__(2, "8") if i == 2 else None),
            ("fit_data", 2, lambda i, c: c.__setitem__(2, "") if i == 30 else None),
        ]
        for code, amplitude, change in cases:
            with self.subTest(code=code), self.assertRaisesRegex(ValueError, code):
                calculate(source(1, amplitude=amplitude, change=change))


if __name__ == "__main__":
    unittest.main()
