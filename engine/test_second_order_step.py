import math
import unittest

from fopdt_simulation import second_order_step


class SecondOrderStepTests(unittest.TestCase):
    def test_fractional_delay_and_repeated_poles_against_analytic_step(self):
        for fast in (0.8, 3, 3 - 1e-12):
            for h in (0.2, 0.05):
                step = second_order_step(-2, 3, fast, 0.37, h, 8)
                pv, history = 0.0, []
                for i in range(round(8/h)):
                    history.append(1)
                    pv = step(pv, history)
                    t = max(0, (i+1)*h - 0.37)
                    if abs(3-fast) < 1e-10:
                        expected = 1 - (1+t/3)*math.exp(-t/3)
                    else:
                        expected = 1 - (3*math.exp(-t/3)-fast*math.exp(-t/fast))/(3-fast)
                    self.assertAlmostEqual(pv, -2*expected, places=11)

    def test_proportional_feedback_and_sopdt_disturbance(self):
        from pid_simulation import calculate
        from test_pid_simulation import case
        scenario = case(secondTauSeconds=1, delaySeconds=0, durationSeconds=80,
                        controller={"kind": "P", "kp": 0.5})
        result = calculate(scenario)
        self.assertEqual(result["controllerForm"], "proportional")
        self.assertAlmostEqual(result["finalError"], 0.5, places=6)
        disturbed = calculate({**scenario, "disturbance": 0.25, "disturbanceAtSeconds": 4})
        self.assertAlmostEqual(disturbed["finalError"], 0.25, places=6)
        with self.assertRaises(ValueError):
            calculate({**scenario, "controller": {"kind": "P", "kp": -0.5}})

    def test_tracking_and_travel_oracle(self):
        from pi_simulation import additional_metrics
        points = [{"seconds": t, "pv": pv, "mv": mv} for t, pv, mv in
                  [(0, 0, 2), (1, -1, 1), (2, -2, None)]]
        r = additional_metrics(points, -2, 2)
        self.assertEqual(r["itae"], 1)
        self.assertEqual(r["mvTravel"], 3)
        self.assertAlmostEqual(r["riseTimeSeconds"], 1.6)
        self.assertIsNone(additional_metrics(points, -2, 1)["riseTimeSeconds"])
