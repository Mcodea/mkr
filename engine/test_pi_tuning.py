import math
import unittest

from pi_tuning import calculate


class PiTuningTests(unittest.TestCase):
    def test_signed_gain_and_simc_integral_limit(self):
        report = calculate(dict(gain=-2, tauSeconds=100, delaySeconds=3, responseSeconds=2))
        self.assertEqual(report["imc"], dict(kp=-10, tiSeconds=100, parallelKi=-0.1))
        self.assertEqual(report["simc"], dict(kp=-10, tiSeconds=20, parallelKi=-0.5))
        self.assertIn("tight_response", report["warnings"])

    def test_slow_target_matches_imc_and_ideal_no_delay_dynamics(self):
        result = calculate(dict(gain=2, tauSeconds=5, delaySeconds=0, responseSeconds=7))
        self.assertEqual(result["imc"], result["simc"])
        settings = result["imc"]
        # Check the analytical closed-loop transfer at several complex frequencies:
        # the PI zero cancels the nominal plant pole and yields 1/(lambda*s+1).
        for s in (0.1j, 0.5j, 3j, complex(1, 2)):
            plant = 2 / (5 * s + 1)
            pi = settings["kp"] * (1 + 1 / (settings["tiSeconds"] * s))
            self.assertAlmostEqual(pi * plant / (1 + pi * plant), 1 / (7 * s + 1))

    def test_invalid_and_unrepresentable_inputs(self):
        valid = dict(gain=2, tauSeconds=10, delaySeconds=0, responseSeconds=3)
        for key, value, code in (
            ("gain", 0, "pi_domain"), ("tauSeconds", -1, "pi_domain"), ("delaySeconds", -1, "pi_domain"),
            ("responseSeconds", 0, "pi_domain"), ("responseSeconds", math.nan, "pi_input"),
            ("gain", True, "pi_input"), ("gain", 5e-324, "pi_range"),
        ):
            with self.subTest(key=key, value=value), self.assertRaisesRegex(ValueError, code):
                calculate({**valid, key: value})


if __name__ == "__main__":
    unittest.main()
