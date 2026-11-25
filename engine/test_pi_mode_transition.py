"""Regression tests for the exploratory PI manual/automatic transfer."""
import math
import unittest

from pi_mode_transition import calculate


def scenario(**changes):
    base = dict(gain=2, tauSeconds=4, delaySeconds=0, sampleSeconds=0.1,
                durationSeconds=2, setpoint=1, minimumMv=-2, maximumMv=2,
                manualMv=0.5, automaticAtSeconds=1,
                kp=0.5, tiSeconds=4)
    return calculate(base | changes)


class PiModeTransitionTests(unittest.TestCase):
    def test_bumpless_transfer_tracks_applied_manual_mv(self):
        result = scenario()
        points = result["points"]
        previous = points[9]
        first_auto = points[10]
        self.assertEqual(previous["mode"], "manual")
        self.assertIsNone(previous["cv"])
        self.assertEqual(first_auto["mode"], "automatic")
        self.assertAlmostEqual(first_auto["mv"], previous["mv"])
        self.assertAlmostEqual(first_auto["cv"], previous["mv"])
        self.assertAlmostEqual(result["transferBumpMv"], 0)
        self.assertEqual(len(points), 21)
        self.assertIsNone(points[-1]["mv"])

    def test_automatic_control_resumes_after_transfer(self):
        first, second = scenario()["points"][10:12]
        self.assertAlmostEqual(first["pv"], 1 - math.exp(-1 / 4))
        self.assertAlmostEqual(second["pv"], 1 - math.exp(-1.1 / 4))
        expected = first["mv"] + 0.5 * (first["pv"] - second["pv"])
        expected += 0.5 * 0.1 / 4 * (1 - first["pv"])
        self.assertAlmostEqual(second["cv"], expected)

    def test_transfer_tracks_saturated_command_not_requested(self):
        result = scenario(manualMv=9)
        first_auto = result["points"][10]
        self.assertEqual(result["points"][9]["mv"], 2)
        self.assertEqual(first_auto["cv"], 2)
        self.assertEqual(first_auto["mv"], 2)
        self.assertGreater(result["saturationSeconds"], 0)

    def test_transfer_tracks_rate_limited_command(self):
        result = scenario(manualMv=2, maximumMvRate=0.5)
        points = result["points"]
        self.assertGreater(result["rateLimitedSeconds"], 0)
        self.assertAlmostEqual(points[9]["mv"], 0.5)
        self.assertAlmostEqual(points[10]["mv"], points[9]["mv"])
        self.assertAlmostEqual(result["transferBumpMv"], 0)
        for before, after in zip(points, points[1:-1]):
            self.assertLessEqual(abs(after["mv"] - before["mv"]), 0.05 + 1e-12)

    def test_reverse_process_sign_and_fractional_delay(self):
        result = scenario(gain=-2, kp=-0.5, delaySeconds=0.35, manualMv=-0.5)
        self.assertAlmostEqual(result["transferBumpMv"], 0)
        self.assertLess(result["points"][10]["mv"], 0)
        self.assertGreater(result["points"][-1]["pv"], 0)

    def test_rejects_bad_transition_or_controller(self):
        cases = (
            (dict(automaticAtSeconds=0), "sim_domain"),
            (dict(automaticAtSeconds=2), "sim_domain"),
            (dict(automaticAtSeconds=0.15), "sim_grid"),
            (dict(kp=-0.5), "sim_domain"),
            (dict(maximumMvRate=0), "sim_domain"),
            (dict(manualMv=True), "sim_input"),
            (dict(tiSeconds=0), "sim_domain"),
        )
        for changes, code in cases:
            with self.subTest(changes=changes):
                with self.assertRaisesRegex(ValueError, code):
                    scenario(**changes)


if __name__ == "__main__":
    unittest.main()


class SecondImplementation(unittest.TestCase):
    """Manual-to-automatic transfer against a second implementation written from the documented convention only (manual: the applied
    command follows the manual target inside the actuator limits and the integral tracks it; at the switch the first automatic command
    is the applied manual command, bumpless; then conditional integration), with the process advanced by scipy's exact zero-order-hold
    steps split at the delayed input changes (fractional delays included). The convention is shared; the implementation is not (audit F-10)."""

    @staticmethod
    def reference(gain, tau, delay, h, n, switch, setpoint, lower, upper, rate, manual, kp, ti):
        import numpy as np
        from test_support_plant import first_order, stepper
        state, output, advance = stepper(first_order(gain, tau), h, delay)
        previous = integral = 0.0
        history, pv, mv = [], [], []
        for k in range(n + 1):
            y = output(state)
            pv.append(y)
            if k == n:
                break
            error = setpoint - y
            low, high = max(lower, previous - rate * h), min(upper, previous + rate * h)
            if k < switch:
                requested = manual
            else:
                if k == switch:
                    integral = previous - kp * error
                requested = previous if k == switch else kp * error + integral
            out = min(max(min(max(requested, lower), upper), low), high)
            if k < switch:
                integral = out - kp * error
            else:
                step = kp * h / ti * error
                if not ((requested >= high and step > 0) or (requested <= low and step < 0)):
                    integral += step
            previous = out
            mv.append(out)
            history.append(out)
            state = advance(state, k, history)
        return np.array(pv), np.array(mv)

    def compare(self, **case):
        import numpy as np
        base = dict(gain=2.0, tau=6.0, delay=1.1, h=0.2, n=250, switch=60, setpoint=1.5, lower=-2.0, upper=2.0, rate=math.inf, manual=0.5, kp=1.1, ti=5.0)
        case = {**base, **case}
        request = dict(gain=case["gain"], tauSeconds=case["tau"], delaySeconds=case["delay"], sampleSeconds=case["h"], durationSeconds=case["n"] * case["h"],
                       setpoint=case["setpoint"], minimumMv=case["lower"], maximumMv=case["upper"], manualMv=case["manual"],
                       automaticAtSeconds=case["switch"] * case["h"], kp=case["kp"], tiSeconds=case["ti"])
        if case["rate"] != math.inf:
            request["maximumMvRate"] = case["rate"]
        result = calculate(request)
        pv, mv = self.reference(case["gain"], case["tau"], case["delay"], case["h"], case["n"], case["switch"], case["setpoint"], case["lower"],
                                case["upper"], case["rate"], case["manual"], case["kp"], case["ti"])
        got_pv = np.array([p["pv"] for p in result["points"]])
        got_mv = np.array([p["mv"] for p in result["points"][:-1]])
        self.assertLess(float(np.max(np.abs(got_pv - pv))), 1e-9)
        self.assertLess(float(np.max(np.abs(got_mv - mv))), 1e-12)
        return result, mv

    def test_transfer_scenarios_match_including_saturation_rate_limit_and_fractional_delay(self):
        scenarios = (dict(), dict(manual=5.0), dict(manual=1.8, rate=0.4), dict(delay=0.0), dict(delay=0.77, setpoint=-1.0, manual=-1.2),
                     dict(gain=-2.0, kp=-1.1, manual=-0.8, setpoint=-1.0, rate=0.5), dict(switch=1), dict(switch=249, manual=1.0))
        for changes in scenarios:
            with self.subTest(changes=changes):
                result, mv = self.compare(**changes)
                switch = changes.get("switch", 60)
                self.assertAlmostEqual(result["transferBumpMv"], 0.0, places=12)            # bumpless by construction of the convention
                self.assertAlmostEqual(result["points"][switch]["cv"], mv[switch - 1], places=12)

    def test_random_scenarios_keep_the_actuator_invariants(self):
        import random
        rng = random.Random(5)
        for _ in range(30):
            gain = rng.choice([-1, 1]) * rng.uniform(0.3, 4)
            h = rng.choice([0.1, 0.25, 0.5])
            lower, upper = -rng.uniform(0.2, 3), rng.uniform(0.2, 3)
            rate = rng.choice([math.inf, rng.uniform(0.05, 1.0)])
            _, mv = self.compare(gain=gain, tau=rng.uniform(3, 30), delay=rng.uniform(0, 10) * h, h=h, n=200, switch=rng.randint(1, 150), lower=lower, upper=upper,
                                 rate=rate, manual=rng.uniform(lower, upper), setpoint=rng.uniform(-4, 4), kp=math.copysign(rng.uniform(0.05, 1.2) / abs(gain), gain),
                                 ti=rng.uniform(2, 30))
            self.assertLessEqual(float(mv.max()), upper + 1e-12)
            self.assertGreaterEqual(float(mv.min()), lower - 1e-12)
            if rate != math.inf:
                self.assertLessEqual(max(abs(b - a) for a, b in zip([0.0] + list(mv[:-1]), mv)), rate * h + 1e-12)

