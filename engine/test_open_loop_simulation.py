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


class SecondImplementation(unittest.TestCase):
    """Rate-limited and saturated steps against exact zero-order-hold steps (scipy), split at the delayed input changes, for fractional
    delays, both gain signs and random scenarios (audit F-10). Without a rate limit the module itself uses the closed form, which the
    first test class checks analytically."""

    def run_case(self, gain, tau, delay, h, n, step_at, step, lower, upper, rate):
        import numpy as np
        from test_support_plant import first_order, stepper
        request = dict(gain=gain, tauSeconds=tau, delaySeconds=delay, sampleSeconds=h, durationSeconds=n * h, stepMv=step, stepAtSeconds=step_at * h,
                       minimumMv=lower, maximumMv=upper, maximumMvRate=rate)
        from open_loop_simulation import calculate
        points = calculate(request)["points"]
        state, output, advance = stepper(first_order(gain, tau), h, delay)
        applied = min(upper, max(lower, step))
        mv, history, expected = 0.0, [], []
        for k in range(n + 1):
            expected.append(output(state))
            if k == n:
                break
            target = applied if k >= step_at else 0.0
            mv = max(mv - rate * h, min(mv + rate * h, target))
            history.append(mv)
            state = advance(state, k, history)
        got = np.array([p["pv"] for p in points])
        self.assertLess(float(np.max(np.abs(got - np.array(expected)))), 1e-9)

    def test_rate_limited_saturated_steps_with_fractional_delays(self):
        for case in (dict(gain=2.0, tau=5.0, delay=1.3, h=0.2, n=300, step_at=10, step=1.5, lower=-2.0, upper=2.0, rate=0.3),
                     dict(gain=-1.5, tau=8.0, delay=0.77, h=0.25, n=300, step_at=0, step=-6.0, lower=-2.0, upper=2.0, rate=0.5),
                     dict(gain=0.5, tau=20.0, delay=0.0, h=0.5, n=200, step_at=40, step=1.0, lower=-1.0, upper=3.0, rate=0.05)):
            with self.subTest(case=case):
                self.run_case(**case)

    def test_random_scenarios(self):
        import random
        rng = random.Random(3)
        for _ in range(30):
            h = rng.choice([0.1, 0.25, 0.5])
            self.run_case(gain=rng.choice([-1, 1]) * rng.uniform(0.3, 4), tau=rng.uniform(2, 40), delay=rng.uniform(0, 12) * h, h=h, n=200, step_at=rng.randint(0, 100),
                          step=rng.choice([-1, 1]) * rng.uniform(0.2, 5), lower=-rng.uniform(0.2, 3), upper=rng.uniform(0.2, 3), rate=rng.uniform(0.05, 1.0))

