import math
import unittest

from pi_simulation import calculate, response_metrics


def payload(**changes):
    return dict(gain=2, tauSeconds=4, delaySeconds=0, imc=dict(kp=0.5, tiSeconds=4),
                simc=dict(kp=0.5, tiSeconds=2), sampleSeconds=0.1, durationSeconds=8,
                setpoint=1, minimumMv=-10, maximumMv=10, disturbance=0, disturbanceAtSeconds=0) | changes


class SimulationTests(unittest.TestCase):
    def test_converges_to_continuous_no_delay_reference(self):
        coarse = calculate(payload(sampleSeconds=0.1))["imc"]["points"][-1]["pv"]
        fine = calculate(payload(sampleSeconds=0.01))["imc"]["points"][-1]["pv"]
        expected = 1 - math.exp(-8 / 4)
        self.assertLess(abs(fine - expected), abs(coarse - expected) / 5)
        self.assertAlmostEqual(fine, expected, delta=0.001)

    def test_exact_fractional_delay_with_saturated_step(self):
        result = calculate(payload(gain=2, tauSeconds=4, delaySeconds=0.35, sampleSeconds=0.1,
                                   durationSeconds=0.6, setpoint=100, maximumMv=1))["imc"]
        for point in result["points"]:
            t = point["seconds"]
            self.assertAlmostEqual(point["pv"], 2 * (1 - math.exp(-max(t - 0.35, 0) / 4)), places=12)
            if t < 0.6:
                self.assertEqual(point["mv"], 1)
        self.assertEqual(result["frozenSteps"], 6)
        self.assertAlmostEqual(result["saturationSeconds"], 0.6)

    def test_input_disturbance_and_reverse_process(self):
        result = calculate(payload(gain=-2, delaySeconds=0.15, sampleSeconds=0.1, durationSeconds=0.6,
                                   setpoint=0, disturbance=1, disturbanceAtSeconds=0.2,
                                   imc=dict(kp=-0.5, tiSeconds=4), simc=dict(kp=-0.5, tiSeconds=2)))["imc"]
        self.assertEqual(result["points"][3]["pv"], 0)
        self.assertAlmostEqual(result["points"][4]["pv"], -2 * (1 - math.exp(-0.05 / 4)), places=12)
        self.assertLess(result["points"][4]["mv"], 0)

    def test_rate_constraint_also_freezes_integral_for_reverse_action(self):
        result = calculate(payload(gain=-2, imc=dict(kp=-0.5, tiSeconds=4),
                                   simc=dict(kp=-0.5, tiSeconds=2), maximumMvRate=1))["imc"]
        mv = [0] + [point["mv"] for point in result["points"][:-1]]
        self.assertEqual(mv[1], -0.1)
        self.assertTrue(all(abs(next_mv - previous) <= 0.1 + 1e-12
                            for previous, next_mv in zip(mv, mv[1:])))
        self.assertGreater(result["rateLimitedSeconds"], 0)
        self.assertGreater(result["frozenSteps"], 0)
        self.assertEqual(result["saturationSeconds"], 0)

    def test_response_metrics_are_signed_and_require_a_settled_tail(self):
        points = [dict(seconds=t, pv=pv) for t, pv in enumerate([0, -2.4, -2, -2.01])]
        self.assertEqual(response_metrics(points, -2), dict(
            windowEndSeconds=3, overshootPv=abs(-2.4 + 2), settlingSeconds=2))
        self.assertIsNone(response_metrics(points[:-1], -2)["settlingSeconds"])
        self.assertIsNone(response_metrics(points, 0)["overshootPv"])

    def test_response_window_ends_at_disturbance_even_with_sampled_delay(self):
        result = calculate(payload(disturbance=3, disturbanceAtSeconds=3, delaySeconds=0.35))
        for method in ("imc", "simc"):
            run = result[method]
            self.assertEqual(run["setpointResponse"], response_metrics(run["points"][:31], 1))
        zero_window = calculate(payload(disturbance=3, disturbanceAtSeconds=0))["imc"]["setpointResponse"]
        self.assertEqual(zero_window, dict(windowEndSeconds=0, overshootPv=None, settlingSeconds=None))

    def test_grid_and_action_validation(self):
        for changes, error in ((dict(durationSeconds=0.25), "sim_grid"),
                               (dict(disturbanceAtSeconds=0.15), "sim_grid"),
                               (dict(gain=-2), "sim_domain"),
                               (dict(minimumMv=0), "sim_domain")):
            with self.subTest(changes=changes), self.assertRaisesRegex(ValueError, error):
                calculate(payload(**changes))


if __name__ == "__main__":
    unittest.main()
