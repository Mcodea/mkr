import json
import unittest

from mkr_engine import respond
from pi_combined_robustness import calculate_combined
from pi_simulation import calculate
from test_pi_simulation import payload


def scenario(**changes):
    variation = changes.pop("variationPercent", 20)
    return payload(**(dict(delaySeconds=0.5, maximumMvRate=2) | changes)) | {
        "variationPercent": variation
    }


class CombinedRobustnessTests(unittest.TestCase):
    def test_27_independent_parameter_combinations_and_nominal(self):
        data = scenario()
        unchanged = data.copy()
        result = calculate_combined(data)
        self.assertEqual(data, unchanged)
        cases = result["cases"]
        self.assertEqual(len(cases), 27)
        self.assertEqual(len({case["key"] for case in cases}), 27)
        self.assertEqual(cases[0]["key"], "K+0_T+0_D+0")
        self.assertEqual(
            {(round(case["gain"], 8), round(case["tauSeconds"], 8),
              round(case["delaySeconds"], 8)) for case in cases},
            {(gain, tau, delay)
             for gain in (1.6, 2, 2.4)
             for tau in (3.2, 4, 4.8)
             for delay in (.4, .5, .6)},
        )
        reference = calculate({key: value for key, value in data.items()
                               if key != "variationPercent"})
        for method in ("imc", "simc"):
            self.assertEqual(cases[0][method]["iae"], reference[method]["iae"])
            self.assertEqual(cases[0][method]["ise"], reference[method]["ise"])

    def test_pv_envelopes_exactly_bound_all_simulated_points(self):
        data = scenario(sampleSeconds=0.2, durationSeconds=4)
        result = calculate_combined(data)
        envelope = result["envelope"]
        self.assertEqual(len(envelope["seconds"]), 21)
        for method in ("imc", "simc"):
            lows = [float("inf")] * 21
            highs = [float("-inf")] * 21
            for case in result["cases"]:
                candidate = {k: v for k, v in data.items() if k != "variationPercent"}
                candidate.update(gain=case["gain"], tauSeconds=case["tauSeconds"],
                                 delaySeconds=case["delaySeconds"])
                points = calculate(candidate)[method]["points"]
                for i, point in enumerate(points):
                    self.assertEqual(point["seconds"], envelope["seconds"][i])
                    lows[i] = min(lows[i], point["pv"])
                    highs[i] = max(highs[i], point["pv"])
            self.assertEqual(envelope[method]["minPv"], lows)
            self.assertEqual(envelope[method]["maxPv"], highs)
            self.assertTrue(all(lo <= hi for lo, hi in zip(lows, highs)))

    def test_reverse_gain_and_zero_delay_are_preserved(self):
        data = scenario(gain=-2, delaySeconds=0,
                        imc={"kp": -.5, "tiSeconds": 4},
                        simc={"kp": -.5, "tiSeconds": 2})
        results = calculate_combined(data)
        self.assertTrue(all(c["gain"] < 0 for c in results["cases"]))
        self.assertTrue(all(c["delaySeconds"] == 0 for c in results["cases"]))
        self.assertIn("not_a_confidence_band", results["warnings"])
        self.assertIn("not_a_stability_proof", results["warnings"])

    def test_reject_bad_variations_and_broken_payload(self):
        for value in (0, -1, 50.1, True, float("nan")):
            with self.subTest(variation=value), self.assertRaises(ValueError):
                calculate_combined(scenario(variationPercent=value))
        missing = scenario()
        del missing["simc"]
        with self.assertRaisesRegex(ValueError, "combined_input"):
            calculate_combined(missing)
        with self.assertRaisesRegex(ValueError, "sim_domain"):
            calculate_combined(scenario(tauSeconds=-1))

    def test_ipc_contract_and_envelope(self):
        request = {"version": 1, "requestId": "joint-27", "operation": "simulate_pi_combined",
                   "payload": scenario(durationSeconds=2, sampleSeconds=0.1)}
        answer = respond(json.dumps(request).encode())
        self.assertNotIn("error", answer)
        self.assertEqual(answer["requestId"], "joint-27")
        self.assertEqual(len(answer["result"]["cases"]), 27)
        self.assertEqual(len(answer["result"]["envelope"]["seconds"]), 21)


if __name__ == "__main__":
    unittest.main()
