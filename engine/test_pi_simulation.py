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


class IndependentOracle(unittest.TestCase):
    """The unsaturated loop is linear: its set-point and load responses are those of the discrete closed-loop transfer functions
    (zero-order-hold plant G(z) = K (1 - p) z^-d / (z - p), controller C(z) = kp (z - 1 + h/Ti) / (z - 1)), evaluated here by scipy's
    dlsim from polynomial arithmetic written in this test: nothing is shared with the simulator under test (audit F-10)."""

    @staticmethod
    def closed_loop(gain, tau, h, delay_steps, kp, ti, source):
        import numpy as np
        p = math.exp(-h / tau)
        plant_num = np.array([gain * (1 - p)])
        plant_den = np.polymul([1.0, -p], np.r_[1.0, np.zeros(delay_steps)])            # (z - p) z^d
        ctrl_num, ctrl_den = kp * np.array([1.0, -1.0 + h / ti]), np.array([1.0, -1.0])
        open_num, open_den = np.polymul(ctrl_num, plant_num), np.polymul(ctrl_den, plant_den)
        char = np.polyadd(open_den, open_num)                                           # 1 + C G, common denominator
        numerator = open_num if source == "setpoint" else np.polymul(ctrl_den, plant_num)     # load: G / (1 + C G)
        return numerator, char

    def run_case(self, gain, tau, h, delay_steps, kp, ti, duration, setpoint, disturbance, event_steps):
        import numpy as np
        from scipy import signal
        n = round(duration / h)
        mine = calculate(payload(gain=gain, tauSeconds=tau, delaySeconds=delay_steps * h, sampleSeconds=h, durationSeconds=duration,
                                 setpoint=setpoint, disturbance=disturbance, disturbanceAtSeconds=event_steps * h,
                                 imc=dict(kp=kp, tiSeconds=ti), simc=dict(kp=kp, tiSeconds=ti)))["imc"]["points"]
        expected = np.zeros(n + 1)
        if setpoint:
            num, den = self.closed_loop(gain, tau, h, delay_steps, kp, ti, "setpoint")
            _, y = signal.dlsim(signal.dlti(num, den, dt=h), setpoint * np.ones(n + 1))
            expected += y[:, 0]
        if disturbance:
            num, den = self.closed_loop(gain, tau, h, delay_steps, kp, ti, "load")
            load = np.where(np.arange(n + 1) >= event_steps, disturbance, 0.0)
            _, y = signal.dlsim(signal.dlti(num, den, dt=h), load)
            expected += y[:, 0]
        return np.array([point["pv"] for point in mine]), expected

    def test_setpoint_and_load_responses_match_the_discrete_closed_loop_transfer_functions(self):
        import numpy as np
        cases = (dict(gain=2.0, tau=4.0, h=0.1, delay_steps=0, kp=0.5, ti=4.0),
                 dict(gain=1.5, tau=20.0, h=0.5, delay_steps=6, kp=0.6, ti=18.0),
                 dict(gain=-3.0, tau=8.0, h=0.2, delay_steps=10, kp=-0.2, ti=9.0),
                 dict(gain=0.4, tau=60.0, h=1.0, delay_steps=3, kp=2.0, ti=50.0))
        for case in cases:
            for setpoint, disturbance in ((1.0, 0.0), (0.0, 0.7), (1.0, -0.5)):
                with self.subTest(case=case, setpoint=setpoint, disturbance=disturbance):
                    mine, expected = self.run_case(duration=case["h"] * 400, setpoint=setpoint, disturbance=disturbance, event_steps=150, **case)
                    self.assertLess(float(np.max(np.abs(mine - expected))), 1e-9)

    def test_the_oracle_itself_detects_a_wrong_controller(self):                      # guard against an oracle that agrees with everything
        import numpy as np
        mine, expected = self.run_case(gain=2.0, tau=4.0, h=0.1, delay_steps=0, kp=0.5, ti=4.0, duration=40.0, setpoint=1.0, disturbance=0.0, event_steps=0)
        wrong = calculate(payload(imc=dict(kp=0.5, tiSeconds=3.0), simc=dict(kp=0.5, tiSeconds=3.0), durationSeconds=40.0))["imc"]["points"]
        self.assertGreater(float(np.max(np.abs(np.array([p["pv"] for p in wrong]) - expected))), 1e-3)


class NonlinearReference(unittest.TestCase):
    """Saturation, rate limit and conditional-integration anti-windup against a second implementation written from the documented
    convention only (clamp the raw output to the range and to the rate window around the previous output; freeze the integral when the
    raw output lies beyond the effective bound and the integral increment pushes it further), with the plant advanced through scipy's
    exact zero-order-hold discretisation instead of the module's own update. Plus invariants on random scenarios (audit F-10)."""

    @staticmethod
    def reference(gain, tau, h, delay_steps, kp, ti, n, setpoint, lower, upper, rate, event, disturbance):
        import numpy as np
        from scipy import signal
        a, b, c, d, _ = signal.cont2discrete((np.array([[-1.0 / tau]]), np.array([[gain / tau]]), np.array([[1.0]]), np.array([[0.0]])), h, method="zoh")
        a, b = float(a[0, 0]), float(b[0, 0])
        y, integral, previous = 0.0, 0.0, 0.0
        history, pv, mv = [], [], []
        for k in range(n + 1):
            pv.append(y)
            if k == n:
                break
            error = setpoint - y
            raw = kp * error + integral
            window_low, window_high = max(lower, previous - rate * h), min(upper, previous + rate * h)
            out = min(max(min(max(raw, lower), upper), window_low), window_high)
            increment = kp * h / ti * error
            if not ((raw >= window_high and increment > 0) or (raw <= window_low and increment < 0)):
                integral += increment
            previous = out
            mv.append(out)
            history.append(out + (disturbance if k >= event else 0.0))
            applied = history[k - delay_steps] if k - delay_steps >= 0 else 0.0
            y = a * y + b * applied
        return np.array(pv), np.array(mv)

    def check(self, **case):
        import numpy as np
        n = round(case["duration"] / case["h"])
        mine = calculate(payload(gain=case["gain"], tauSeconds=case["tau"], delaySeconds=case["delay_steps"] * case["h"], sampleSeconds=case["h"],
                                 durationSeconds=case["duration"], setpoint=case["setpoint"], minimumMv=case["lower"], maximumMv=case["upper"],
                                 disturbance=case["disturbance"], disturbanceAtSeconds=case["event"] * case["h"],
                                 imc=dict(kp=case["kp"], tiSeconds=case["ti"]), simc=dict(kp=case["kp"], tiSeconds=case["ti"]),
                                 **({} if case["rate"] == math.inf else dict(maximumMvRate=case["rate"]))))["imc"]
        pv, mv = self.reference(case["gain"], case["tau"], case["h"], case["delay_steps"], case["kp"], case["ti"], n, case["setpoint"],
                                case["lower"], case["upper"], case["rate"], case["event"], case["disturbance"])
        got_pv = np.array([p["pv"] for p in mine["points"]])
        got_mv = np.array([p["mv"] for p in mine["points"][:-1]])
        self.assertLess(float(np.max(np.abs(got_pv - pv))), 1e-9)
        self.assertLess(float(np.max(np.abs(got_mv - mv))), 1e-12)
        return mine, got_mv

    def test_saturated_and_rate_limited_loops_match_the_second_implementation(self):
        base = dict(gain=2.0, tau=6.0, h=0.2, delay_steps=5, kp=1.4, ti=5.0, duration=120.0, setpoint=3.0, lower=-1.0, upper=2.0, rate=math.inf, event=250, disturbance=0.0)
        for changes in (dict(), dict(rate=0.5), dict(rate=0.2, disturbance=-0.8, setpoint=1.0), dict(gain=-2.0, kp=-1.4, setpoint=-3.0, rate=0.3)):
            with self.subTest(changes=changes):
                mine, _ = self.check(**{**base, **changes})
                self.assertGreater(mine["saturationSeconds"] + mine["rateLimitedSeconds"], 0)       # the nonlinear branches really ran

    def test_random_scenarios_keep_the_actuator_invariants(self):
        import random
        rng = random.Random(7)
        for _ in range(40):
            case = dict(gain=rng.choice([-1, 1]) * rng.uniform(0.3, 4), tau=rng.uniform(2, 40), h=rng.choice([0.1, 0.25, 0.5]), delay_steps=rng.randint(0, 12),
                        kp=1.0, ti=rng.uniform(2, 40), duration=100.0, setpoint=rng.uniform(-5, 5), lower=-rng.uniform(0.2, 3), upper=rng.uniform(0.2, 3),
                        rate=rng.choice([math.inf, rng.uniform(0.05, 1.0)]), event=rng.randint(0, 100), disturbance=rng.uniform(-1, 1))
            case["kp"] = math.copysign(rng.uniform(0.05, 1.5) / abs(case["gain"]), case["gain"])
            case["duration"] = round(case["h"] * 300, 6)
            mine, mv = self.check(**case)
            self.assertLessEqual(float(mv.max()), case["upper"] + 1e-12)
            self.assertGreaterEqual(float(mv.min()), case["lower"] - 1e-12)
            if case["rate"] != math.inf:
                steps = [abs(b - a) for a, b in zip([0.0] + list(mv[:-1]), mv)]
                self.assertLessEqual(max(steps), case["rate"] * case["h"] + 1e-12)

