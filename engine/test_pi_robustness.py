import unittest

from pi_robustness import calculate_sensitivity
from pi_simulation import calculate
from test_pi_simulation import payload


def scenario(**changes):
    return payload(**(dict(delaySeconds=0.5, maximumMvRate=2) | changes)) | {"variationPercent": 20}


class PiRobustnessTests(unittest.TestCase):
    def test_nominal_exactly_matches_existing_simulator(self):
        data = scenario()
        snapshot = {key: (value.copy() if isinstance(value, dict) else value)
                    for key, value in data.items()}
        result = calculate_sensitivity(data)
        self.assertEqual(data, snapshot)
        self.assertEqual(result["algorithmVersion"], "1.0.0")
        self.assertEqual(len(result["cases"]), 7)
        nominal = calculate({key: value for key, value in data.items()
                             if key != "variationPercent"})
        for method in ("imc", "simc"):
            self.assertEqual(result["cases"][0][method]["iae"], nominal[method]["iae"])
            self.assertEqual(result["cases"][0][method]["ise"], nominal[method]["ise"])
            self.assertEqual(result["cases"][0][method]["saturationSeconds"],
                             nominal[method]["saturationSeconds"])

    def test_independent_parameter_variations_and_fixed_pi(self):
        data = scenario()
        results = calculate_sensitivity(data)["cases"]
        expected = [
            ("nominal", 2, 4, .5),
            ("gain_minus", 1.6, 4, .5),
            ("gain_plus", 2.4, 4, .5),
            ("tau_minus", 2, 3.2, .5),
            ("tau_plus", 2, 4.8, .5),
            ("delay_minus", 2, 4, .4),
            ("delay_plus", 2, 4, .6),
        ]
        for result, (key, gain, tau, delay) in zip(results, expected):
            self.assertEqual(result["key"], key)
            self.assertAlmostEqual(result["gain"], gain)
            self.assertAlmostEqual(result["tauSeconds"], tau)
            self.assertAlmostEqual(result["delaySeconds"], delay)
            candidate = {k: v for k, v in data.items() if k != "variationPercent"}
            candidate.update(gain=gain, tauSeconds=tau, delaySeconds=delay)
            reference = calculate(candidate)
            self.assertAlmostEqual(result["imc"]["iae"], reference["imc"]["iae"], places=12)
            self.assertAlmostEqual(result["simc"]["finalError"],
                                   reference["simc"]["finalError"], places=12)

    def test_reverse_gain_remains_negative_and_controller_is_not_retuned(self):
        data = scenario(gain=-2, imc={"kp": -.5, "tiSeconds": 4},
                        simc={"kp": -.5, "tiSeconds": 2})
        result = calculate_sensitivity(data)
        self.assertTrue(all(case["gain"] < 0 for case in result["cases"]))
        self.assertEqual(result["cases"][1]["gain"], -1.6)
        self.assertEqual(result["cases"][2]["gain"], -2.4)
        self.assertIn("fixed_pi_coefficients", result["warnings"])

    def test_zero_nominal_delay_does_not_invent_delay(self):
        result = calculate_sensitivity(scenario(delaySeconds=0))
        self.assertEqual(result["cases"][5]["delaySeconds"], 0)
        self.assertEqual(result["cases"][6]["delaySeconds"], 0)

    def test_errors_are_explicit(self):
        for value in (0, -1, 51, True, float("nan")):
            with self.subTest(variation=value), self.assertRaises(ValueError):
                calculate_sensitivity(scenario(variationPercent=value))
        invalid = scenario()
        del invalid["imc"]
        with self.assertRaisesRegex(ValueError, "robust_input"):
            calculate_sensitivity(invalid)
        with self.assertRaisesRegex(ValueError, "sim_domain"):
            calculate_sensitivity(scenario(tauSeconds=-2))


if __name__ == "__main__":
    unittest.main()
