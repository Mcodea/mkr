import base64
from datetime import datetime, timedelta, timezone
import math
import random
import unittest

import numpy as np

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

    def test_noise_sensitivity_uses_training_only(self):
        def noisy(level):
            generator = random.Random(8321)

            def adjust(i, cells):
                if i >= 6:
                    cells[2] = str(float(cells[2]) + level * generator.gauss(0, 1))
            return adjust

        first = calculate(source(1, change=noisy(0.05)))
        stronger = calculate(source(1, change=noisy(0.10)))
        self.assertAlmostEqual(first["gain"], -3, delta=0.05)
        self.assertAlmostEqual(first["tauSeconds"], 4, delta=0.3)
        self.assertAlmostEqual(first["delaySeconds"], 2.5, delta=0.3)
        for parameter in ("gain", "tauSeconds", "delaySeconds"):
            low = first["standardErrors"][parameter]
            high = stronger["standardErrors"][parameter]
            self.assertGreater(low, 0)
            self.assertAlmostEqual(high / low, 2, delta=0.22)

        # Check the scaled analytic sensitivities against finite differences
        # in the physical coordinates of the three reported parameters.
        times = np.array([point["seconds"] for point in first["training"]["points"]])
        parameters = np.array([first["gain"], first["tauSeconds"], first["delaySeconds"]])

        def curve(p):
            gain, tau, delay = p
            return 7 + 2 * gain * (1 - np.exp(-np.maximum(times - delay, 0) / tau))

        epsilon = 1e-5
        changes = epsilon * np.eye(3)
        derivatives = np.column_stack([
            (curve(parameters + change) - curve(parameters - change)) / (2 * epsilon)
            for change in changes
        ])
        sample_count = len(times)
        covariance = (
            first["training"]["rmse"] ** 2 * sample_count / (sample_count - 3)
            * np.linalg.inv(derivatives.T @ derivatives)
        )
        np.testing.assert_allclose(list(first["standardErrors"].values()), np.sqrt(np.diag(covariance)), rtol=1e-6)

        alternate = calculate(source(1, change=noisy(0.05)), source(2, gain=-0.7))
        self.assertEqual(first["standardErrors"], alternate["standardErrors"])

    def test_autocorrelated_disturbance_suppresses_standard_errors(self):
        def drift(i, cells):
            if i >= 6:
                cells[2] = str(float(cells[2]) + 0.3 * math.sin(i / 7))
        report = calculate(source(1, change=drift))
        self.assertIn("correlated_residuals", report["warnings"])
        self.assertIsNone(report["standardErrors"])

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
