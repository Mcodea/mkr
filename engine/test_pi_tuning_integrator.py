import math
import unittest

import numpy as np

from pi_tuning_integrator import calculate, loop_margins


def closed_loop(slope, delay, kp, ti, setpoint, dt, horizon, load=0.0, load_at=None):
    """Euler simulation of PV' = k' (u(t - delay) + load) with the ideal PI, sampled every dt, unbounded output."""
    queue = [0.0] * max(1, round(delay / dt))
    pv = integral = 0.0
    pvs = []
    for step in range(int(horizon / dt)):
        error = setpoint - pv
        integral += error * dt
        u = kp * (error + integral / ti)
        queue.append(u)
        d = load if load_at is not None and step * dt >= load_at else 0.0
        pv += dt * slope * (queue.pop(0) + d)
        pvs.append(pv)
    return np.array(pvs)


class RuleAndLoop(unittest.TestCase):
    def test_rule_values(self):
        result = calculate(dict(gain=0.05, delaySeconds=3.0, responseSeconds=7.0))
        self.assertAlmostEqual(result["kp"], 1 / (0.05 * 10))
        self.assertAlmostEqual(result["tiSeconds"], 40.0)
        self.assertEqual(result["rule"], "SIMC_integrating")

    def test_negative_slope_gives_negative_kp(self):
        result = calculate(dict(gain=-0.02, delaySeconds=1, responseSeconds=4))
        self.assertLess(result["kp"], 0)
        self.assertGreater(result["tiSeconds"], 0)

    def test_closed_loop_settles_and_the_warnings_follow_the_real_overshoot(self):
        for slope, delay, response in itertools_product():
            with self.subTest(slope=slope, delay=delay, response=response):
                r = calculate(dict(gain=slope, delaySeconds=delay, responseSeconds=response))
                dt = min(delay, response) / 40
                pv = closed_loop(slope, delay, r["kp"], r["tiSeconds"], 1.0, dt, 40 * (response + delay))
                self.assertAlmostEqual(pv[-1], 1.0, delta=0.01)             # integral action: no offset
                if response >= delay:
                    self.assertLess(pv.max(), 1.35)                         # the SIMC recommendation tau_c >= theta: moderate overshoot
                    self.assertNotIn("tight_response", r["warnings"])
                    self.assertNotIn("high_sensitivity", r["warnings"])
                else:
                    self.assertIn("tight_response", r["warnings"])          # tau_c < theta: announced, the overshoot is larger
                    self.assertLess(pv.max(), 1.8)
                    if pv.max() > 1.45:
                        self.assertIn("high_sensitivity", r["warnings"])

    def test_a_constant_load_is_rejected_without_offset(self):
        r = calculate(dict(gain=0.05, delaySeconds=3, responseSeconds=6))
        pv = closed_loop(0.05, 3, r["kp"], r["tiSeconds"], 0.0, 0.1, 600, load=2.0, load_at=5)
        self.assertAlmostEqual(pv[-1], 0.0, delta=0.01)

    def test_margins_match_the_simulated_loop_stability_boundary(self):
        r = calculate(dict(gain=0.05, delaySeconds=3, responseSeconds=6))
        gm = r["margins"]["gainMargin"]
        self.assertGreater(gm, 1)
        stable = closed_loop(0.05, 3, r["kp"] * gm * 0.9, r["tiSeconds"], 1.0, 0.05, 1500)
        unstable = closed_loop(0.05, 3, r["kp"] * gm * 1.2, r["tiSeconds"], 1.0, 0.05, 1500)
        self.assertLess(abs(stable[-1] - 1), 0.05)
        self.assertGreater(np.abs(unstable[-300:] - 1).max(), 1.0)          # diverging oscillation beyond the gain margin

    def test_margins_function_is_independent_of_the_time_scale(self):
        a = loop_margins(0.05, 3, 1 / (0.05 * 9), 36)
        b = loop_margins(0.05 / 1000, 3000, 1 / (0.05 / 1000 * 9000), 36000)
        for x, y in zip(a, b):
            self.assertAlmostEqual(x, y, delta=1e-2 * abs(x))

    def test_domain(self):
        for bad in (dict(gain=0, delaySeconds=1, responseSeconds=1), dict(gain=1, delaySeconds=-1, responseSeconds=1),
                    dict(gain=1, delaySeconds=1, responseSeconds=0)):
            with self.assertRaises(ValueError):
                calculate(bad)
        with self.assertRaises(ValueError):
            calculate(dict(gain=1))


def itertools_product():
    for slope in (0.05, -0.8):
        for delay, response in ((1.0, 10.0), (5.0, 5.0), (10.0, 20.0), (10.0, 5.0), (10.0, 2.0)):
            yield slope, delay, response


if __name__ == "__main__":
    unittest.main()
