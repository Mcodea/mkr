import unittest
import json

from mkr_engine import respond

from pid_simulation import calculate
from pi_simulation import calculate as pi_calculate
from test_pi_simulation import payload as pi_payload


def case(**changes):
    result = dict(
        gain=2, tauSeconds=4, delaySeconds=0.25,
        controller=dict(kp=0.5, tiSeconds=4, tdSeconds=0.4, filterSeconds=0.15),
        sampleSeconds=0.05, durationSeconds=8, setpoint=1,
        minimumMv=-10, maximumMv=10,
        disturbance=0, disturbanceAtSeconds=0,
    )
    return result | changes


class FilteredPidTests(unittest.TestCase):
    def test_zero_derivative_is_exact_existing_pi_baseline(self):
        c = case(controller=dict(kp=0.5, tiSeconds=4, tdSeconds=0, filterSeconds=0))
        output = calculate(c)
        reference = pi_calculate(pi_payload(
            gain=c["gain"], tauSeconds=c["tauSeconds"], delaySeconds=c["delaySeconds"],
            imc={"kp": 0.5, "tiSeconds": 4},
            simc={"kp": 0.5, "tiSeconds": 4},
            sampleSeconds=c["sampleSeconds"], durationSeconds=c["durationSeconds"],
            setpoint=c["setpoint"], minimumMv=c["minimumMv"],
            maximumMv=c["maximumMv"], disturbance=c["disturbance"],
            disturbanceAtSeconds=c["disturbanceAtSeconds"],
        ))["imc"]
        for metric in ("iae", "ise", "finalError", "saturationSeconds",
                       "rateLimitedSeconds", "frozenSteps", "setpointResponse"):
            self.assertEqual(output[metric], reference[metric])
        for point, expected in zip(output["points"], reference["points"]):
            for field in ("seconds", "pv", "cv", "mv", "disturbance"):
                self.assertEqual(point[field], expected[field])
            self.assertEqual(point["derivative"], 0 if point["derivative"] is not None else None)

    def test_filtered_derivative_is_on_pv_not_setpoint(self):
        c = case()
        result = calculate(c)
        points = result["points"]
        self.assertEqual(points[0]["pv"], 0)
        self.assertEqual(points[0]["derivative"], 0)
        self.assertGreater(points[0]["cv"], 0)
        self.assertTrue(any(point["derivative"] < 0 for point in points[:-1]))
        self.assertEqual(points[-1]["derivative"], None)
        # Independent backward-Euler filter oracle for every time step.
        previous = 0.0
        prev_pv = 0.0
        kp = c["controller"]["kp"]
        td = c["controller"]["tdSeconds"]
        tf = c["controller"]["filterSeconds"]
        h = c["sampleSeconds"]
        for point in points[:-1]:
            expected = tf / (tf + h) * previous - kp * td / (tf + h) * (point["pv"] - prev_pv)
            self.assertAlmostEqual(point["derivative"], expected, delta=1e-12)
            previous, prev_pv = expected, point["pv"]

    def test_derivative_converges_to_pi_as_td_decreases(self):
        pi = calculate(case(controller=dict(kp=0.5, tiSeconds=4, tdSeconds=0,
                                            filterSeconds=0.15)))
        tiny = calculate(case(controller=dict(kp=0.5, tiSeconds=4, tdSeconds=1e-9,
                                              filterSeconds=0.15)))
        self.assertAlmostEqual(tiny["finalError"], pi["finalError"], delta=1e-8)
        self.assertAlmostEqual(tiny["iae"], pi["iae"], delta=1e-8)

    def test_saturation_and_rate_limit_still_hold_with_derivative(self):
        output = calculate(case(
            setpoint=100, minimumMv=-0.2, maximumMv=0.2,
            maximumMvRate=0.4, durationSeconds=1))
        previous = 0.0
        for point in output["points"][:-1]:
            self.assertGreaterEqual(point["mv"], -0.2)
            self.assertLessEqual(point["mv"], 0.2)
            self.assertLessEqual(abs(point["mv"] - previous), 0.4 * 0.05 + 1e-12)
            previous = point["mv"]
        self.assertGreater(output["frozenSteps"], 0)
        self.assertGreater(output["rateLimitedSeconds"], 0)

    def test_reverse_process_and_signed_controller(self):
        c = case(gain=-2, controller=dict(kp=-0.5, tiSeconds=4,
                                          tdSeconds=0.4, filterSeconds=0.15))
        result = calculate(c)
        self.assertTrue(all(point["mv"] is None or point["mv"] <= 10 for point in result["points"]))
        self.assertLess(result["points"][0]["cv"], 0)
        self.assertTrue(any(point["derivative"] > 0 for point in result["points"][:-1]))

    def test_ipc_is_versioned_and_rejects_invalid_filter(self):
        request = {"version": 1, "requestId": "pid-prototype",
                   "operation": "simulate_pid_experimental", "payload": case()}
        answer = respond(json.dumps(request).encode())
        self.assertNotIn("error", answer)
        self.assertEqual(answer["requestId"], "pid-prototype")
        self.assertEqual(answer["result"]["algorithmVersion"], "0.2.0")
        self.assertEqual(answer["result"]["controllerForm"],
                         "ideal_dependent_pid_derivative_on_pv")
        self.assertEqual(answer["result"]["points"][-1]["cv"], None)
        request["payload"]["controller"]["filterSeconds"] = 0
        invalid = respond(json.dumps(request).encode())
        self.assertEqual(invalid["error"]["code"], "pid_domain")

    def test_parameter_limits_and_grid(self):
        for changes, error in (
            (dict(controller=dict(kp=0.5, tiSeconds=4, tdSeconds=1, filterSeconds=0)), "pid_domain"),
            (dict(controller=dict(kp=0.5, tiSeconds=4, tdSeconds=-1, filterSeconds=0.2)), "pid_domain"),
            (dict(controller=dict(kp=-0.5, tiSeconds=4, tdSeconds=.1, filterSeconds=.1)), "pid_domain"),
            (dict(durationSeconds=0.07), "sim_grid"),
            (dict(sampleSeconds=0), "pid_domain"),
            (dict(setpoint=float("nan")), "sim_input"),
        ):
            with self.subTest(changes=changes), self.assertRaisesRegex(ValueError, error):
                calculate(case(**changes))

    def test_time_refinement_converges_to_independent_fine_reference(self):
        # Reuse the same underlying continuous FOPDT and controller parameters,
        # compare endpoint error under refined sample times (no exported PLC assumptions).
        reference = calculate(case(sampleSeconds=0.005))["points"][-1]["pv"]
        coarse = calculate(case(sampleSeconds=0.1))["points"][-1]["pv"]
        fine = calculate(case(sampleSeconds=0.025))["points"][-1]["pv"]
        self.assertLess(abs(fine - reference), abs(coarse - reference))

    def test_filtered_pid_has_no_initial_setpoint_derivative_kick(self):
        positive = calculate(case(setpoint=1))["points"]
        negative = calculate(case(setpoint=-1))["points"]
        zero = calculate(case(setpoint=0))["points"]
        for series in (positive, negative, zero):
            self.assertEqual(series[0]["derivative"], 0)
        self.assertGreater(positive[0]["cv"], 0)
        self.assertLess(negative[0]["cv"], 0)
        self.assertEqual(zero[0]["cv"], 0)

    def test_derivative_filter_warning_is_explicit_when_undersampled(self):
        result = calculate(case(sampleSeconds=0.2, durationSeconds=8,
                                controller=dict(kp=0.5, tiSeconds=4,
                                                tdSeconds=0.4, filterSeconds=0.05)))
        self.assertIn("coarse_derivative_filter", result["warnings"])

    def test_disturbance_and_fractional_delay(self):
        r = calculate(case(setpoint=0, disturbance=1, disturbanceAtSeconds=0.5))
        self.assertEqual(r["points"][10]["disturbance"], 1)
        self.assertEqual(r["setpointResponse"]["windowEndSeconds"], 0.5)
        self.assertEqual(r["points"][0]["pv"], 0)
        self.assertGreater(r["points"][16]["pv"], 0)


if __name__ == "__main__":
    unittest.main()


class IndependentOracle(unittest.TestCase):
    """Unsaturated filtered PID (P and I on the error, backward-Euler filtered D on the measurement): the loop is linear, so the
    set-point and load responses are those of the discrete transfer functions evaluated by scipy's dlsim from polynomial algebra
    written in this test (audit F-10). u = C_pi (r - y) - Cd y + d, C_pi = kp (z - 1 + h/Ti) / (z - 1), Cd = g (z - 1) / (z - a),
    a = Tf / (Tf + h), g = kp Td / (Tf + h); plant G(z) = K (1 - p) z^-m / (z - p)."""

    def responses(self, gain, tau, h, delay_steps, kp, ti, td, tf, n, setpoint, disturbance, event):
        import math
        import numpy as np
        from scipy import signal
        p, a, g = math.exp(-h / tau), tf / (tf + h), kp * td / (tf + h)
        gn, gd = np.array([gain * (1 - p)]), np.polymul([1.0, -p], np.r_[1.0, np.zeros(delay_steps)])
        n1, d1, nd, dd = kp * np.array([1.0, -1.0 + h / ti]), np.array([1.0, -1.0]), g * np.array([1.0, -1.0]), np.array([1.0, -a])
        char = np.polyadd(np.polymul(np.polymul(d1, dd), gd), np.polymul(np.polyadd(np.polymul(n1, dd), np.polymul(nd, d1)), gn))
        expected = np.zeros(n + 1)
        if setpoint:
            _, y = signal.dlsim(signal.dlti(np.polymul(np.polymul(n1, dd), gn), char, dt=h), setpoint * np.ones(n + 1))
            expected += y[:, 0]
        if disturbance:
            load = np.where(np.arange(n + 1) >= event, disturbance, 0.0)
            _, y = signal.dlsim(signal.dlti(np.polymul(np.polymul(d1, dd), gn), char, dt=h), load)
            expected += y[:, 0]
        mine = calculate(case(gain=gain, tauSeconds=tau, delaySeconds=delay_steps * h, sampleSeconds=h, durationSeconds=n * h,
                              controller=dict(kp=kp, tiSeconds=ti, tdSeconds=td, filterSeconds=tf), setpoint=setpoint,
                              disturbance=disturbance, disturbanceAtSeconds=event * h, minimumMv=-1e6, maximumMv=1e6))
        return np.array([point["pv"] for point in mine["points"]]), expected

    def test_pid_responses_match_the_discrete_closed_loop_transfer_functions(self):
        import numpy as np
        cases = (dict(gain=2.0, tau=4.0, h=0.05, delay_steps=5, kp=0.5, ti=4.0, td=0.4, tf=0.15),
                 dict(gain=1.5, tau=30.0, h=0.5, delay_steps=8, kp=1.2, ti=14.0, td=3.0, tf=0.6),
                 dict(gain=-2.0, tau=10.0, h=0.2, delay_steps=4, kp=-0.8, ti=9.0, td=1.5, tf=0.3))
        for parameters in cases:
            for setpoint, disturbance in ((1.0, 0.0), (0.0, 0.6), (1.0, -0.4)):
                with self.subTest(parameters=parameters, setpoint=setpoint, disturbance=disturbance):
                    mine, expected = self.responses(n=300, setpoint=setpoint, disturbance=disturbance, event=120, **parameters)
                    self.assertLess(float(np.max(np.abs(mine - expected))), 1e-8)

    def test_the_oracle_detects_a_wrong_derivative_time(self):
        import numpy as np
        mine, expected = self.responses(gain=2.0, tau=4.0, h=0.05, delay_steps=5, kp=0.5, ti=4.0, td=0.4, tf=0.15, n=300, setpoint=1.0, disturbance=0.0, event=0)
        _, other = self.responses(gain=2.0, tau=4.0, h=0.05, delay_steps=5, kp=0.5, ti=4.0, td=0.8, tf=0.15, n=300, setpoint=1.0, disturbance=0.0, event=0)
        self.assertGreater(float(np.max(np.abs(mine - other))), 1e-4)

