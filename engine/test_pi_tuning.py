import math

import numpy as np
import unittest

from pi_tuning import calculate


class PiTuningTests(unittest.TestCase):
    def test_signed_gain_and_simc_integral_limit(self):
        report = calculate(dict(gain=-2, tauSeconds=100, delaySeconds=3, responseSeconds=2))
        coefficients = lambda entry: {key: entry[key] for key in ("kp", "tiSeconds", "parallelKi")}
        self.assertEqual(coefficients(report["imc"]), dict(kp=-10, tiSeconds=100, parallelKi=-0.1))
        self.assertEqual(coefficients(report["simc"]), dict(kp=-10, tiSeconds=20, parallelKi=-0.5))
        self.assertIn("tight_response", report["warnings"])
        # The sign of K does not change the loop: |Kp K| and the margins are those of the positive-gain case.
        positive = calculate(dict(gain=2, tauSeconds=100, delaySeconds=3, responseSeconds=2))
        self.assertEqual(report["simc"]["margins"], positive["simc"]["margins"])

    def test_every_tuning_carries_its_nominal_margins(self):
        result = calculate(dict(gain=1.0, tauSeconds=5.0, delaySeconds=1.0, responseSeconds=1.0))
        for method in ("imc", "simc"):
            margins = result[method]["margins"]
            self.assertGreater(margins["phaseMarginDegrees"], 30)
            self.assertGreater(margins["gainMargin"], 1.5)
            self.assertGreater(margins["peakSensitivity"], 1.0)
            self.assertEqual(margins["warnings"], [])
        # A tight target on a long delay is flagged, never hidden (Skogestad: tau_c < theta is aggressive).
        tight = calculate(dict(gain=1.0, tauSeconds=5.0, delaySeconds=2.0, responseSeconds=0.2))["simc"]["margins"]
        self.assertTrue({"low_phase_margin", "low_gain_margin", "high_sensitivity"} & set(tight["warnings"]))

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

    def test_published_imc_pi_is_the_pade_internal_model_controller_without_its_derivative_term(self):
        """Independent derivation (not the module's code). IMC filter 1/(lambda s+1), plant model K exp(-theta s)/(tau s+1) with the first-order
        Pade exp(-theta s) ~ (1 - theta s/2)/(1 + theta s/2): Q = (tau s+1)(1+theta s/2) / (K (lambda s+1)), C = Q / (1 - P Q)
        = (tau s+1)(1+theta s/2) / (K (lambda+theta/2) s), an ideal PID with Kp = (tau+theta/2)/(K(lambda+theta/2)), Ti = tau+theta/2,
        Td = tau theta/(2 tau+theta). The published PI keeps Kp and Ti."""
        gain, tau, theta, lam = -2.5, 40.0, 10.0, 12.0
        result = calculate({"gain": gain, "tauSeconds": tau, "delaySeconds": theta, "responseSeconds": lam})["imcPublished"]
        w = np.logspace(-4, 0, 200)
        s = 1j * w
        controller = (tau * s + 1) * (1 + theta * s / 2) / (gain * (lam + theta / 2) * s)               # exact IMC controller
        pade_plant = gain * (1 - theta * s / 2) / ((1 + theta * s / 2) * (tau * s + 1))
        loop = controller * pade_plant
        target = (1 - theta * s / 2) / (lam * s + 1)                                                        # IMC closed loop P Q
        np.testing.assert_allclose(loop / (1 + loop), target, rtol=1e-9, atol=1e-12)
        kp = (tau + theta / 2) / (gain * (lam + theta / 2))
        ti = tau + theta / 2
        td = tau * theta / (2 * tau + theta)
        ideal = kp * (1 + 1 / (ti * s) + td * s)
        np.testing.assert_allclose(controller, ideal, rtol=1e-9)                                           # the PID expansion
        self.assertAlmostEqual(result["kp"], kp, places=12)
        self.assertAlmostEqual(result["tiSeconds"], ti, places=12)
        self.assertLess(result["kp"], 0)                                                                    # sign follows the gain
        self.assertIsNotNone(result["margins"])

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
