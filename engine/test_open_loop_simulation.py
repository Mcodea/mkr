import json
import math
import unittest

from mkr_engine import respond


def simulate(**changes):
    payload = dict(gain=-2, tauSeconds=4, delaySeconds=0.35, sampleSeconds=0.25,
                   durationSeconds=2, stepMv=3, stepAtSeconds=0.5, minimumMv=-1, maximumMv=2)
    request = dict(version=1, requestId="open-loop", operation="simulate_open_loop", payload=payload | changes)
    return respond(json.dumps(request).encode())


class OpenLoopSimulationTests(unittest.TestCase):
    def test_delayed_saturated_step_with_negative_gain(self):
        result = simulate()["result"]
        self.assertEqual(result["steadyPv"], -4)
        self.assertEqual(result["saturationSeconds"], 1.5)
        self.assertEqual(len(result["points"]), 9)
        for point in result["points"]:
            time = point["seconds"]
            elapsed = max(0, time - 0.85)
            self.assertAlmostEqual(point["pv"], -4 * (1 - math.exp(-elapsed / 4)), places=12)
            if time < 2:
                self.assertEqual(point["requestedMv"], 3 if time >= 0.5 else 0)
                self.assertEqual(point["appliedMv"], 2 if time >= 0.5 else 0)
        self.assertIsNone(result["points"][-1]["requestedMv"])
        self.assertIsNone(result["points"][-1]["appliedMv"])

    def test_immediate_step_and_response_outside_horizon(self):
        immediate = simulate(stepMv=-0.5, stepAtSeconds=0, delaySeconds=0)["result"]
        self.assertEqual(immediate["steadyPv"], 1)
        self.assertEqual(immediate["saturationSeconds"], 0)
        self.assertAlmostEqual(immediate["points"][1]["pv"], 1 - math.exp(-0.25 / 4))

        delayed = simulate(delaySeconds=1.5)["result"]
        self.assertIn("response_beyond_horizon", delayed["warnings"])
        self.assertTrue(all(point["pv"] == 0 for point in delayed["points"]))

    def test_rate_limited_steps_match_superposed_analytic_responses(self):
        result = simulate(maximumMvRate=4)["result"]
        self.assertEqual(result["rateLimitedSeconds"], 0.25)
        self.assertEqual(result["saturationSeconds"], 1.5)
        self.assertEqual([point["appliedMv"] for point in result["points"][2:5]], [1, 2, 2])
        for point in result["points"]:
            expected = sum(-2 * (1 - math.exp(-max(0, point["seconds"] - onset) / 4))
                           for onset in (0.85, 1.10))
            self.assertAlmostEqual(point["pv"], expected, places=12)

    def test_invalid_scenarios(self):
        for changes, code in ((dict(stepMv=True), "sim_input"), (dict(maximumMvRate=0), "sim_domain"), (dict(stepMv=0), "sim_domain"),
                              (dict(stepAtSeconds=2), "sim_domain"), (dict(stepAtSeconds=0.1), "sim_grid")):
            with self.subTest(changes=changes):
                self.assertEqual(simulate(**changes)["error"]["code"], code)
