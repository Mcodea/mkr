"""Guided tuning: equations against the published SIMC rules, robustness checked by an INDEPENDENT implementation
(dense frequency sweep and a time-domain simulation written in this file, not the module's own margin search),
minimality of the recommendation, refusals and hostile inputs."""
import math
import unittest

import numpy as np

import recommend
from recommend import COVERAGE, OBJECTIVES, calculate


def request(**changes):
    base = dict(family="FOPDT", gain=2.0, tauSeconds=40.0, delaySeconds=10.0, secondTauSeconds=None, sampleSeconds=1.0, derivativeFilterRatio=None, processVariationPercent=None, optimise=False,
                standardErrors=dict(gain=0.05, tauSeconds=2.0, delaySeconds=1.0), objective="balanced")
    base.update(changes)
    return base


def sensitivity_peak(gain, taus, delay, kp, ti, td, tf=0.0):
    """Max |1/(1+L)| on a dense logarithmic grid: written here, independent of stability_margins."""
    w = np.logspace(-4, 2, 30000)
    s = 1j * w
    plant = gain * np.exp(-s * delay)
    for tau in taus:
        plant = plant / (tau * s + 1)
    controller = kp * (1 + 1 / (ti * s) + td * s / (tf * s + 1))
    return float(np.max(1 / np.abs(1 + controller * plant)))


def closed_loop_settles(gain, taus, delay, kp, ti, td, horizon, tf=0.0):
    """Explicit time-domain simulation (Euler, delay buffer) of a unit set-point step; returns the final error and the overshoot."""
    dt = (min(min(taus), max(delay, 1e-3)) if taus else max(delay, 1e-3)) / 12
    if tf > 0:
        dt = min(dt, tf / 5)                                          # explicit Euler of the derivative filter needs dt well below Tf
    steps = int(horizon / dt)
    lag = int(round(delay / dt))
    buffer = [0.0] * (lag + 1)
    states = [0.0] * len(taus)
    integral, previous_error, output, filtered = 0.0, 1.0, 0.0, 0.0
    peak = 0.0
    for _ in range(steps):
        error = 1.0 - output
        integral += error * dt
        if tf > 0:                                                    # filtered derivative Td s / (Tf s + 1)
            filtered += dt * (error - filtered) / tf
            derivative = td * (error - filtered) / tf
        else:
            derivative = td * (error - previous_error) / dt
        u = kp * (error + integral / ti + derivative)
        previous_error = error
        buffer.append(u)
        delayed = buffer.pop(0)
        signal = gain * delayed
        for index, tau in enumerate(taus):             # cascade of first-order lags
            states[index] += dt * (signal - states[index]) / tau
            signal = states[index]
        output = signal if taus else output + dt * signal          # no lag at all: pure integrator PV' = k' u
        peak = max(peak, output)
        if not math.isfinite(output) or abs(output) > 1e6:
            return math.inf, math.inf
    return abs(1.0 - output), peak - 1.0


class Equations(unittest.TestCase):
    def test_pi_matches_the_published_simc_rule_at_the_returned_closed_loop_time_constant(self):
        result = calculate(request())
        controller, tauc = result["recommendation"]["controller"], result["recommendation"]["responseSeconds"]
        effective = 10.0 + 0.5                        # delay + half the sampling period (Skogestad 2003)
        self.assertEqual(result["structure"], "PI")
        self.assertAlmostEqual(controller["kp"], 40.0 / (2.0 * (tauc + effective)), places=12)
        self.assertAlmostEqual(controller["tiSeconds"], min(40.0, 4 * (tauc + effective)), places=12)
        self.assertEqual(controller["tdSeconds"], 0.0)

    def test_half_rule_for_a_second_pole_not_above_the_delay(self):
        result = calculate(request(family="SOPDT", secondTauSeconds=3.0))
        tauc = result["recommendation"]["responseSeconds"]
        tau1, theta = 40.0 + 1.5, 10.0 + 1.5 + 0.5
        self.assertEqual(result["structure"], "PI")
        self.assertAlmostEqual(result["recommendation"]["controller"]["kp"], tau1 / (2.0 * (tauc + theta)), places=12)

    def test_reduced_pi_is_assessed_on_the_identified_two_pole_plant(self):
        result = calculate(request(family="SOPDT", secondTauSeconds=8.0))
        chosen = result["recommendation"]
        controller = chosen["controller"]

        def peak(k, tau, delay):
            return sensitivity_peak(k, [tau, 8.0], delay + 0.5, controller["kp"], controller["tiSeconds"], 0.0)

        corners = [peak(k, tau, delay) for k in (1.9, 2.1) for tau in (36.0, 44.0) for delay in (8.0, 12.0)]
        nominal = peak(2.0, 40.0, 10.0)
        self.assertAlmostEqual(chosen["nominalMargins"]["peakSensitivity"], nominal, delta=0.002)
        self.assertAlmostEqual(chosen["worstPeakSensitivity"], max(nominal, *corners), delta=0.002)

    def test_optimised_controllers_on_a_negative_gain_reduced_sopdt(self):
        result = calculate(request(family="SOPDT", gain=-2.0, secondTauSeconds=8.0, optimise=True))
        self.assertEqual(result["decision"], "recommended")
        plants = [(-2.0, 40.0, 10.0)]
        plants.extend((gain, slow_tau, delay)
                      for gain in (-2.1, -1.9) for slow_tau in (36.0, 44.0) for delay in (8.0, 12.0))
        for key in ("optimized", "optimizedPid"):
            with self.subTest(controller=key):
                tuning = result[key]
                self.assertIsNotNone(tuning)
                c = tuning["controller"]
                self.assertLess(c["kp"], 0.0)
                peaks = [sensitivity_peak(gain, [slow_tau, 8.0], delay + 0.5, c["kp"],
                                          c["tiSeconds"], c["tdSeconds"], c["filterSeconds"])
                         for gain, slow_tau, delay in plants]
                self.assertAlmostEqual(tuning["worstPeakSensitivity"], max(peaks), delta=0.002)
                self.assertLessEqual(max(peaks), OBJECTIVES["balanced"] + 0.005)

    def test_second_pole_above_the_delay_gives_a_pid_whose_ideal_form_is_the_series_form(self):
        result = calculate(request(family="SOPDT", secondTauSeconds=15.0))
        controller, tauc = result["recommendation"]["controller"], result["recommendation"]["responseSeconds"]
        effective = 10.5
        kc, tau_i, tau_d = 40.0 / (2.0 * (tauc + effective)), min(40.0, 4 * (tauc + effective)), 15.0
        self.assertEqual(result["structure"], "PID")
        self.assertAlmostEqual(controller["kp"], kc * (tau_i + tau_d) / tau_i, places=10)
        self.assertAlmostEqual(controller["tiSeconds"], tau_i + tau_d, places=10)
        self.assertAlmostEqual(controller["tdSeconds"], tau_i * tau_d / (tau_i + tau_d), places=10)
        # series and ideal forms have the same transfer function: compare the controllers on a frequency grid
        w = np.logspace(-3, 1, 50)
        s = 1j * w
        series = kc * (1 + 1 / (tau_i * s)) * (1 + tau_d * s)
        ideal = controller["kp"] * (1 + 1 / (controller["tiSeconds"] * s) + controller["tdSeconds"] * s)
        self.assertLess(float(np.max(np.abs(series - ideal) / np.abs(series))), 1e-12)

    def test_the_pid_derivative_is_filtered_with_the_declared_ratio_and_the_filter_is_part_of_the_judged_loop(self):
        for ratio, expected in ((None, 10.0), (4.0, 4.0)):
            result = calculate(request(family="SOPDT", secondTauSeconds=15.0, derivativeFilterRatio=ratio))
            c = result["recommendation"]["controller"]
            self.assertAlmostEqual(c["filterSeconds"], c["tdSeconds"] / expected, places=12)
            self.assertEqual(result["derivativeFilterRatio"], expected)
            for gain_sign, tau_sign, delay_sign in ((-1, -1, 1), (1, 1, 1), (-1, 1, 1)):
                k, tau, delay = 2.0 + gain_sign * 0.1, 40.0 + tau_sign * 4.0, 10.5 + delay_sign * 2.0
                peak = sensitivity_peak(k, [tau, 15.0], delay, c["kp"], c["tiSeconds"], c["tdSeconds"], c["filterSeconds"])
                self.assertLessEqual(peak, OBJECTIVES["balanced"] + 2e-3)
        for bad in (1.0, 200.0, "10", True):
            with self.assertRaises(ValueError):
                calculate(request(derivativeFilterRatio=bad))


class Robustness(unittest.TestCase):
    def test_every_objective_keeps_the_peak_sensitivity_within_its_limit_on_the_nominal_model_and_the_corners(self):
        errors = dict(gain=0.05, tauSeconds=2.0, delaySeconds=1.0)
        for objective, limit in OBJECTIVES.items():
            result = calculate(request(objective=objective))
            c = result["recommendation"]["controller"]
            for g in (-1, 1):
                for t in (-1, 1):
                    for d in (-1, 1):
                        k = 2.0 + g * COVERAGE * errors["gain"]
                        tau = 40.0 + t * COVERAGE * errors["tauSeconds"]
                        delay = 10.0 + d * COVERAGE * errors["delaySeconds"] + 0.5
                        peak = sensitivity_peak(k, [tau], delay, c["kp"], c["tiSeconds"], 0.0)
                        self.assertLessEqual(peak, limit + 2e-3, (objective, g, t, d, peak))

    def test_the_recommendation_is_the_fastest_one_on_the_search_grid(self):
        result = calculate(request())
        chosen = result["recommendation"]
        slower_than_chosen = [a for a in result["alternatives"] if a["responseSeconds"] > chosen["responseSeconds"]]
        faster = [a for a in result["alternatives"] if a["responseSeconds"] < chosen["responseSeconds"]]
        self.assertTrue(faster)
        for alternative in faster:
            self.assertTrue(alternative["worstPeakSensitivity"] > OBJECTIVES["balanced"] or not alternative["stableOnAllCorners"])
        for alternative in slower_than_chosen:
            self.assertLessEqual(alternative["worstPeakSensitivity"], OBJECTIVES["balanced"] + 1e-9)

    def test_the_closed_loop_settles_on_the_nominal_model_and_on_the_worst_delay_corner(self):
        for objective in OBJECTIVES:
            result = calculate(request(objective=objective))
            rec = result["recommendation"]
            c = rec["controller"]
            horizon = 12 * (rec["responseSeconds"] + 10.5) + 8 * 40
            for k, tau, delay in ((2.0, 40.0, 10.5), (2.0 * 1.05, 40.0 - 4.0, 10.5 + 2.0)):
                final_error, overshoot = closed_loop_settles(k, [tau], delay, c["kp"], c["tiSeconds"], 0.0, horizon)
                self.assertLess(final_error, 0.02, (objective, k, tau, delay))
                self.assertLess(overshoot, 0.6)

    def test_a_larger_delay_uncertainty_never_gives_a_faster_tuning(self):
        small = calculate(request(objective="robust", standardErrors=dict(gain=0.05, tauSeconds=2.0, delaySeconds=0.5)))
        large = calculate(request(objective="robust", standardErrors=dict(gain=0.05, tauSeconds=2.0, delaySeconds=4.0)))
        self.assertGreaterEqual(large["recommendation"]["responseSeconds"], small["recommendation"]["responseSeconds"])

    def test_a_declared_process_variation_widens_the_uncertainty_box_and_is_met_on_the_true_corners(self):
        plain = calculate(request(standardErrors=None))
        varied = calculate(request(standardErrors=None, processVariationPercent=30.0, optimise=False))
        self.assertGreaterEqual(varied["recommendation"]["responseSeconds"], plain["recommendation"]["responseSeconds"])
        self.assertEqual(varied["processVariationPercent"], 30.0)
        self.assertTrue(varied["uncertaintyQuantified"])
        c = varied["recommendation"]["controller"]
        for g in (0.7, 1.3):
            for t in (0.7, 1.3):
                for d in (0.7, 1.3):
                    peak = sensitivity_peak(2.0 * g, [40.0 * t], 10.0 * d + 0.5, c["kp"], c["tiSeconds"], 0.0)
                    self.assertLessEqual(peak, OBJECTIVES["balanced"] + 2e-3, (g, t, d, peak))

    def test_the_largest_accepted_variation_still_gives_a_tuning(self):
        self.assertEqual(calculate(request(standardErrors=None, processVariationPercent=50.0, tauSeconds=40.0))["decision"], "recommended")
        refused = calculate(request(standardErrors=dict(gain=0.05, tauSeconds=2.0, delaySeconds=1.0), processVariationPercent=50.0, gain=2.0))
        self.assertEqual(refused["decision"], "recommended")        # +/-50 % keeps the sign and a positive time constant

    def test_the_integrator_takes_the_process_variation_too(self):
        plain = calculate(dict(family="Integrateur", gain=0.02, tauSeconds=None, delaySeconds=5.0, secondTauSeconds=None, sampleSeconds=1.0,
                               standardErrors=None, objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
        varied = calculate(dict(family="Integrateur", gain=0.02, tauSeconds=None, delaySeconds=5.0, secondTauSeconds=None, sampleSeconds=1.0,
                                standardErrors=None, objective="balanced", derivativeFilterRatio=None, processVariationPercent=30.0, optimise=False))
        self.assertIn("uncertainty_not_quantified", plain["warnings"])
        self.assertNotIn("uncertainty_not_quantified", varied["warnings"])
        self.assertGreaterEqual(varied["recommendation"]["responseSeconds"], plain["recommendation"]["responseSeconds"])

    def test_a_more_demanding_objective_never_gives_a_slower_tuning(self):
        times = [calculate(request(objective=name))["recommendation"]["responseSeconds"] for name in ("robust", "balanced", "performance")]
        self.assertGreaterEqual(times[0], times[1])
        self.assertGreaterEqual(times[1], times[2])


def worst_peak(family, plants, kp, ti, sampling):
    """Independent worst-case sensitivity peak over the corner plants (written here, not the module's search)."""
    peaks = []
    for k, tau, delay in plants:
        if family == "Integrateur":
            w = np.logspace(-5, 2, 30000)
            s = 1j * w
            loop = kp * (1 + 1 / (ti * s)) * k * np.exp(-s * (delay + sampling)) / s
            peaks.append(float(np.max(1 / np.abs(1 + loop))))
        else:
            peaks.append(sensitivity_peak(k, [tau], delay + sampling, kp, ti, 0.0))
    return max(peaks)


def settles(family, plants, kp, ti, sampling, horizon):
    return max(closed_loop_settles(k, [] if family == "Integrateur" else [tau], delay + sampling, kp, ti, 0.0, horizon)[0] for k, tau, delay in plants)


class Optimisation(unittest.TestCase):
    CASES = (
        ("FOPDT", dict(gain=2.0, tauSeconds=40.0, delaySeconds=10.0, standardErrors=dict(gain=0.05, tauSeconds=2.0, delaySeconds=1.0)),
         [(2.0 * g, 40.0 + t * 4.0, 10.0 + d * 2.0) for g in (0.95, 1.05) for t in (-1, 1) for d in (-1, 1)] + [(2.0, 40.0, 10.0)], 1.0, 2500.0),
        ("Integrateur", dict(gain=0.02, tauSeconds=None, delaySeconds=5.0, standardErrors=dict(gain=0.001, tauSeconds=0, delaySeconds=0.3)),
         [(0.02 + g * 0.002, 1.0, 5.0 + d * 0.6) for g in (-1, 1) for d in (-1, 1)] + [(0.02, 1.0, 5.0)], 1.0, 1500.0),
    )

    def run_case(self, family, kwargs):
        return calculate(request(family=family, objective="balanced", optimise=True, **kwargs))

    def test_the_optimum_respects_the_sensitivity_limit_on_the_corners_and_settles(self):
        for family, kwargs, plants, sample, horizon in self.CASES:
            with self.subTest(family=family):
                result = self.run_case(family, kwargs)
                opt = result["optimized"]
                self.assertIsNotNone(opt)
                kp, ti = abs(opt["controller"]["kp"]), opt["controller"]["tiSeconds"]
                self.assertLessEqual(worst_peak(family, plants, kp, ti, 0.5 * sample), OBJECTIVES["balanced"] + 5e-3)
                self.assertLess(settles(family, plants, kp, ti, 0.5 * sample, horizon), 0.02)

    def test_the_optimum_is_at_least_as_good_as_the_rule_based_tuning_and_says_what_it_costs(self):
        for family, kwargs, plants, sample, horizon in self.CASES:
            with self.subTest(family=family):
                result = self.run_case(family, kwargs)
                rec, opt = result["recommendation"]["controller"], result["optimized"]
                self.assertGreaterEqual(opt["integralGain"], abs(rec["kp"]) / rec["tiSeconds"] * 0.99)
                self.assertGreaterEqual(opt["integralGainRatio"], 0.99)
                # the cost is announced: the nominal set-point overshoot agrees with the independent time-domain simulation
                kp, ti = abs(opt["controller"]["kp"]), opt["controller"]["tiSeconds"]
                k, tau, delay = plants[-1]
                _, peak = closed_loop_settles(k, [] if family == "Integrateur" else [tau], delay + 0.5 * sample, kp, ti, 0.0, horizon)
                self.assertAlmostEqual(opt["nominalOvershootPercent"], max(0.0, peak * 100), delta=3.0)

    def test_the_optimum_is_on_the_constraint_boundary(self):
        for family, kwargs, plants, sample, horizon in self.CASES:
            with self.subTest(family=family):
                opt = self.run_case(family, kwargs)["optimized"]
                kp, ti = abs(opt["controller"]["kp"]), opt["controller"]["tiSeconds"]
                limit = OBJECTIVES["balanced"]
                for scaled_kp, scaled_ti, label in ((kp * 1.04, ti, "more gain"), (kp, ti / 1.04, "shorter Ti")):
                    violated = worst_peak(family, plants, scaled_kp, scaled_ti, 0.5 * sample) > limit or \
                        settles(family, plants, scaled_kp, scaled_ti, 0.5 * sample, horizon) > 0.02
                    self.assertTrue(violated, f"{label} must break the constraint if the integral gain is maximal")

    def test_the_optimisation_is_off_by_default_and_the_second_order_model_gets_a_pid_only(self):
        off = calculate(request())
        self.assertIsNone(off["optimized"])
        self.assertIsNone(off["optimizedPid"])
        self.assertIsNone(off["performance"])
        pid = calculate(request(family="SOPDT", secondTauSeconds=15.0, optimise=True))
        self.assertIsNone(pid["optimized"])                          # the PI search does not apply when the second pole is cancelled
        self.assertIsNotNone(pid["optimizedPid"])
        with self.assertRaises(ValueError):
            calculate(request(optimise="yes"))

    def test_the_pid_optimum_respects_the_limit_on_the_corners_and_settles_judged_by_independent_oracles(self):
        family, kwargs, plants, sample, horizon = self.CASES[0]
        result = self.run_case(family, kwargs)
        pid = result["optimizedPid"]
        self.assertIsNotNone(pid)
        c = pid["controller"]
        self.assertEqual(c["structure"], "PID")
        for gain, tau, delay in plants:
            peak = sensitivity_peak(gain, [tau], delay + 0.5 * sample, c["kp"], c["tiSeconds"], c["tdSeconds"], c["filterSeconds"])
            self.assertLessEqual(peak, OBJECTIVES["balanced"] + 5e-3)
            error, _ = closed_loop_settles(gain, [tau], delay + 0.5 * sample, c["kp"], c["tiSeconds"], c["tdSeconds"], horizon, c["filterSeconds"])
            self.assertLess(abs(error), 0.02)

    def test_a_pid_is_never_presumed_better_than_a_pi_and_the_claim_matches_the_numbers(self):
        for family, kwargs, plants, sample, horizon in self.CASES[:1]:
            result = self.run_case(family, kwargs)
            pid, pi = result["optimizedPid"], result["optimized"]
            self.assertEqual(pid["pidNotBetterThanPi"], pid["integralGain"] <= pi["integralGain"])
            self.assertAlmostEqual(pid["integralGainRatioToOptimalPi"], pid["integralGain"] / pi["integralGain"], places=12)
            hf = result["performance"]["optimizedPid"]["highFrequencyGain"]          # the price of the derivative term is published too
            self.assertGreater(hf, result["performance"]["optimizedPi"]["highFrequencyGain"])


class IntegratorPid(unittest.TestCase):
    CASE = dict(gain=0.02, tauSeconds=None, delaySeconds=5.0, standardErrors=dict(gain=0.001, tauSeconds=0, delaySeconds=0.3))
    PLANTS = [(0.02 + g * 0.002, 5.0 + d * 0.6) for g in (-1, 1) for d in (-1, 1)] + [(0.02, 5.0)]

    def result(self):
        return calculate(request(family="Integrateur", objective="balanced", optimise=True, **self.CASE))

    def test_the_integrator_pid_respects_the_limit_on_the_corners_and_settles_independent_oracles(self):
        c = self.result()["optimizedPid"]["controller"]
        self.assertEqual(c["structure"], "PID")
        w = np.logspace(-5, 2, 40000)
        s = 1j * w
        for k, delay in self.PLANTS:
            loop = c["kp"] * (1 + 1 / (c["tiSeconds"] * s) + c["tdSeconds"] * s / (c["filterSeconds"] * s + 1)) * k * np.exp(-s * (delay + 0.5)) / s
            self.assertLessEqual(float(np.max(1 / np.abs(1 + loop))), OBJECTIVES["balanced"] + 5e-3)
            error, _ = closed_loop_settles(k, [], delay + 0.5, c["kp"], c["tiSeconds"], c["tdSeconds"], 1500.0, c["filterSeconds"])
            self.assertLess(abs(error), 0.02)

    def test_the_pid_is_compared_with_the_pi_and_the_claim_matches_the_numbers(self):
        result = self.result()
        pid, pi = result["optimizedPid"], result["optimized"]
        self.assertEqual(pid["pidNotBetterThanPi"], pid["integralGain"] <= pi["integralGain"])
        self.assertGreater(result["performance"]["optimizedPid"]["highFrequencyGain"], result["performance"]["optimizedPi"]["highFrequencyGain"])
        import json
        json.dumps(result, allow_nan=False)

    def test_integrating_performance_criteria_match_scipy_on_the_closed_form_loop(self):
        """Delay-free integrator K/s with PI: y/r = (a s + a/Ti) / (s^2 + a s + a/Ti), a = Kp K. Oracle: scipy's step response, integrated."""
        from scipy import signal
        gain, kp, ti = 0.02, 50.0, 8.0                       # a = 1 per second, overdamped (zeta = 1.41)
        a = kp * gain
        t = np.linspace(0, 400, 400001)
        _, y = signal.step(signal.lti([a, a / ti], [1.0, a, a / ti]), T=t)
        _, load = signal.step(signal.lti([gain, 0.0], [1.0, a, a / ti]), T=t)          # transfer function y/d = K s / (s^2 + a s + a / Ti)
        expected_iae = float(np.trapezoid(np.abs(1 - y), t))
        result = recommend.closed_loop_performance(gain, 1.0, None, 0.0, dict(kp=kp, tiSeconds=ti, tdSeconds=0.0, filterSeconds=0.0), integrating=True)
        self.assertAlmostEqual(result["setpointStep"]["iae"], expected_iae, delta=0.015 * expected_iae)
        self.assertAlmostEqual(result["loadDisturbanceStep"]["iae"], float(np.trapezoid(np.abs(load), t)), delta=0.015 * ti / kp)
        self.assertAlmostEqual(result["loadDisturbanceStep"]["iae"], ti / kp, delta=0.015 * ti / kp)      # overdamped: IAE = Ti / Kp exactly
        self.assertAlmostEqual(result["setpointStep"]["overshootPercent"], 100 * (float(y.max()) - 1), delta=0.3)      # the PI zero makes the set-point response overshoot although the poles are real


class NoiseSensitivity(unittest.TestCase):
    """noise_to_control_gain against a Monte-Carlo time simulation written here: sampled loop with an exact zero-order-hold plant (scipy),
    forward-Euler integral, backward-Euler filtered derivative, white measurement noise of unit variance, set-point zero. The gain must
    equal the standard deviation of the controller output (the discrete loop adds a half-sample delay, handled by the effective delay)."""

    @staticmethod
    def monte_carlo(gain, tau, delay_steps, h, controller, steps=120000, seed=1):
        from test_support_plant import first_order, stepper
        kp, ti, td, tf = controller["kp"], controller["tiSeconds"], controller["tdSeconds"], controller["filterSeconds"]
        state, output, advance = stepper(first_order(gain, tau), h, delay_steps * h)
        rng = np.random.default_rng(seed)
        noise = rng.standard_normal(steps)
        decay, change = (tf / (tf + h), kp * td / (tf + h)) if td > 0 else (0.0, 0.0)
        integral = derivative = previous_error = 0.0
        history, outputs = [], []
        for k in range(steps):
            error = -(output(state) + noise[k])
            derivative = decay * derivative + change * (error - previous_error)
            previous_error = error
            u = kp * error + integral + derivative
            integral += kp * h / ti * error
            history.append(u)
            outputs.append(u)
            state = advance(state, k, history)
        return float(np.std(outputs[steps // 10:]))

    def test_the_gain_matches_the_simulated_output_standard_deviation(self):
        h, delay_steps, gain, tau = 0.05, 40, 2.0, 20.0
        for controller in (dict(kp=0.6, tiSeconds=18.0, tdSeconds=0.0, filterSeconds=0.0),
                           dict(kp=1.2, tiSeconds=15.0, tdSeconds=3.0, filterSeconds=0.3)):
            with self.subTest(controller=controller):
                predicted = recommend.noise_to_control_gain(gain, tau, None, delay_steps * h + h / 2, controller, h)
                simulated = self.monte_carlo(gain, tau, delay_steps, h, controller)
                # continuous-time band-limited formula versus a sampled loop: within 5 % for the PI, within 10 % with the derivative (backward-Euler
                # filter near the Nyquist frequency differs from the continuous one, the formula is conservative: 12.7 predicted for 11.9 simulated)
                self.assertAlmostEqual(predicted, simulated, delta=(0.10 if controller["tdSeconds"] > 0 else 0.05) * simulated)

    def test_a_static_plant_gives_the_closed_form_kp_over_one_plus_kp_k(self):
        gain, kp = 2.0, 1.5                          # plant lag far faster than the sampling band: C S = Kp / (1 + Kp K) up to a few percent
        predicted = recommend.noise_to_control_gain(gain, 1e-4, None, 0.0, dict(kp=kp, tiSeconds=1e9, tdSeconds=0.0, filterSeconds=0.0), 1.0)
        self.assertAlmostEqual(predicted, kp / (1 + kp * gain), delta=0.03 * kp / (1 + kp * gain))

    def test_the_derivative_term_costs_noise_and_the_report_says_so(self):
        p = dict(family="FOPDT", gain=2.0, tauSeconds=40.0, delaySeconds=10.0, secondTauSeconds=None, sampleSeconds=1.0, standardErrors=None, objective="balanced",
                 derivativeFilterRatio=None, processVariationPercent=None, optimise=True)
        performance = calculate(p)["performance"]
        self.assertGreater(performance["optimizedPid"]["noiseToControlGain"], 3 * performance["optimizedPi"]["noiseToControlGain"])
        for entry in performance.values():
            self.assertGreater(entry["setpointStep"]["peakMv"], 0)
            self.assertGreaterEqual(entry["setpointStep"]["peakMv"], entry["setpointStep"]["initialMvStep"] - 1e-9)


class Curves(unittest.TestCase):
    def test_the_response_curves_start_at_zero_end_at_the_setpoint_and_are_plain_floats(self):
        import json
        p = dict(family="FOPDT", gain=-1.5, tauSeconds=40.0, delaySeconds=15.0, secondTauSeconds=None, sampleSeconds=1.0, standardErrors=None,
                 objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=True)
        result = calculate(p)
        json.dumps(result, allow_nan=False)
        for name, entry in result["performance"].items():
            with self.subTest(controller=name):
                curve = entry["curves"]["setpoint"]
                self.assertLessEqual(len(curve["seconds"]), 300)
                self.assertEqual(len(curve["seconds"]), len(curve["pv"]))
                self.assertEqual(len(curve["seconds"]), len(curve["mv"]))
                self.assertEqual(curve["seconds"][0], 0.0)
                self.assertEqual(curve["pv"][0], 0.0)                              # the loop starts at rest
                self.assertAlmostEqual(curve["pv"][-1], 1.0, delta=0.02)           # unit set-point step: settled at the end of the displayed window
                load = entry["curves"]["load"]
                self.assertAlmostEqual(load["pv"][-1], 0.0, delta=0.02)            # a load step is rejected by the integral action
                self.assertGreater(max(abs(v) for v in load["pv"]), 0)


class Serialisation(unittest.TestCase):
    def test_every_result_is_plain_json_the_bridge_can_carry(self):        # numpy scalars once surfaced as "engine_non_finite" through the bridge
        import json
        cases = (dict(), dict(family="SOPDT", secondTauSeconds=15.0), dict(family="Integrateur", tauSeconds=None, gain=0.02, delaySeconds=5.0, standardErrors=None))
        for extra in cases:
            with self.subTest(extra=extra):
                result = calculate(request(optimise=True, **extra))
                json.dumps(result, allow_nan=False)
                self.assertNotIn("numpy", repr(result))


class ClosedLoopPerformance(unittest.TestCase):
    """Oracle: PI with Ti = tau on a dead-time-free first-order plant is an integrator loop L = a / (tau s), a = Kp K:
    set-point error e(t) = exp(-t / T), T = tau / a  ->  IAE = T, ISE = T / 2, ITAE = T^2, no overshoot, 2 % settling at 3.912 T;
    load step at the plant input: y(t) = K / (a - 1) (exp(-t / tau) - exp(-a t / tau))  ->  IAE = K tau / a."""

    def test_settling_is_unobserved_when_the_window_ends_before_band_entry(self):
        # Ti = tau, Kp K = 1: analytical entry at tau * log(50), beyond one tau.
        result = recommend.closed_loop_performance(
            1.0, 10.0, None, 0.0, dict(kp=1.0, tiSeconds=10.0, tdSeconds=0.0, filterSeconds=0.0),
            horizon_multiple=1.0)
        self.assertIsNone(result["setpointStep"]["settlingSeconds"])
        self.assertEqual(result["horizonSeconds"], 10.0)
        self.assertGreater(len(result["curves"]["setpoint"]["seconds"]), 1)

    def test_closed_form_values(self):
        gain, tau, a = 2.0, 30.0, 3.0
        kp = a / gain
        result = recommend.closed_loop_performance(gain, tau, None, 0.0, dict(kp=kp, tiSeconds=tau, tdSeconds=0.0, filterSeconds=0.0))
        T = tau / a
        step, load = result["setpointStep"], result["loadDisturbanceStep"]
        self.assertAlmostEqual(step["iae"], T, delta=0.015 * T)          # explicit Euler: first-order accurate, see the convergence test
        self.assertAlmostEqual(step["ise"], T / 2, delta=0.015 * T / 2)
        self.assertAlmostEqual(step["itae"], T * T, delta=0.015 * T * T)
        self.assertLess(step["overshootPercent"], 0.05)
        self.assertAlmostEqual(step["settlingSeconds"], T * math.log(50), delta=0.03 * T * math.log(50))
        self.assertAlmostEqual(load["iae"], gain * tau / a, delta=0.01 * gain * tau / a)
        self.assertEqual(result["highFrequencyGain"], kp)

    def test_the_error_against_the_closed_form_halves_when_the_step_is_halved(self):
        gain, tau, a = 2.0, 30.0, 3.0
        T = tau / a
        errors = []
        for resolution in (400.0, 800.0, 1600.0):
            result = recommend.closed_loop_performance(gain, tau, None, 0.0, dict(kp=a / gain, tiSeconds=tau, tdSeconds=0.0, filterSeconds=0.0), resolution=resolution)
            errors.append(abs(result["setpointStep"]["iae"] - T))
        self.assertAlmostEqual(errors[0] / errors[1], 2.0, delta=0.15)
        self.assertAlmostEqual(errors[1] / errors[2], 2.0, delta=0.15)
        self.assertLess(errors[2], 0.003 * T)

    def test_a_negative_gain_gives_the_same_criteria(self):
        positive = recommend.closed_loop_performance(2.0, 30.0, None, 0.0, dict(kp=1.5, tiSeconds=30.0, tdSeconds=0.0, filterSeconds=0.0))
        negative = recommend.closed_loop_performance(-2.0, 30.0, None, 0.0, dict(kp=-1.5, tiSeconds=30.0, tdSeconds=0.0, filterSeconds=0.0))
        self.assertAlmostEqual(positive["setpointStep"]["iae"], negative["setpointStep"]["iae"], places=9)

    def test_an_unstable_loop_is_reported_not_returned(self):
        with self.assertRaisesRegex(ValueError, "recommend_simulation_diverged"):
            recommend.closed_loop_performance(1.0, 10.0, None, 20.0, dict(kp=50.0, tiSeconds=10.0, tdSeconds=0.0, filterSeconds=0.0))


class Refusals(unittest.TestCase):
    def test_an_uncertainty_that_excludes_the_gain_sign_is_refused_with_actions(self):
        result = calculate(request(standardErrors=dict(gain=1.1, tauSeconds=2.0, delaySeconds=1.0)))
        self.assertEqual(result["decision"], "refused")
        self.assertIsNone(result["recommendation"])
        self.assertIn("parameter_uncertainty_excludes_the_model", result["reasons"])
        self.assertTrue(result["actions"])

    def test_an_uncertainty_that_excludes_a_positive_time_constant_is_refused(self):
        self.assertEqual(calculate(request(standardErrors=dict(gain=0.05, tauSeconds=25.0, delaySeconds=1.0)))["decision"], "refused")

    def test_unquantified_uncertainty_is_flagged_not_hidden(self):
        result = calculate(request(standardErrors=None))
        self.assertIn("uncertainty_not_quantified", result["warnings"])
        self.assertIsNone(result["uncertaintyCoverageSigma"])

    def test_a_closed_loop_slower_than_the_process_is_warned(self):
        result = calculate(request(objective="robust", standardErrors=dict(gain=0.05, tauSeconds=2.0, delaySeconds=20.0)))
        self.assertEqual(result["decision"], "recommended")
        self.assertIn("closed_loop_slower_than_the_process", result["warnings"])

    def test_declared_variation_includes_the_fast_lag_of_the_identified_plant(self):
        for nominal_fast in (8.0, 15.0):
            with self.subTest(second_tau=nominal_fast):
                result = calculate(request(family="SOPDT", secondTauSeconds=nominal_fast,
                                           standardErrors=None, processVariationPercent=20.0))
                self.assertEqual(result["decision"], "recommended")
                self.assertIsNone(result["uncertaintyCoverageSigma"])
                self.assertNotIn("second_pole_uncertainty_not_propagated", result["warnings"])
                chosen = result["recommendation"]
                c = chosen["controller"]
                plants = [(2.0, 40.0, 10.0, nominal_fast)]
                plants.extend((k, slow, delay, fast) for k in (1.6, 2.4) for slow in (32.0, 48.0)
                              for delay in (8.0, 12.0) for fast in (0.8 * nominal_fast, 1.2 * nominal_fast))
                peaks = [sensitivity_peak(k, [slow, fast], delay + 0.5, c["kp"], c["tiSeconds"],
                                          c["tdSeconds"], c["filterSeconds"]) for k, slow, delay, fast in plants]
                self.assertAlmostEqual(chosen["worstPeakSensitivity"], max(peaks), delta=0.002)
                self.assertLessEqual(max(peaks), OBJECTIVES["balanced"] + 0.005)

    def test_second_order_uncertainty_is_flagged_as_partly_propagated(self):
        result = calculate(request(family="SOPDT", secondTauSeconds=3.0))
        self.assertIn("second_pole_uncertainty_not_propagated", result["warnings"])

    def test_integrator_uncertainty_uses_slope_and_delay_corners(self):
        result = calculate(dict(family="Integrateur", gain=0.02, tauSeconds=None, delaySeconds=5.0, secondTauSeconds=None, sampleSeconds=1.0,
                                standardErrors=dict(gain=0.001, tauSeconds=0, delaySeconds=0.3), objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
        self.assertEqual(result["decision"], "recommended")
        self.assertTrue(result["uncertaintyQuantified"])
        self.assertLessEqual(result["recommendation"]["worstPeakSensitivity"], 1.7 + 1e-9)

    def test_the_result_is_always_marked_unqualified(self):
        self.assertEqual(calculate(request())["qualification"], "model_based_unqualified")

    def test_invalid_guided_inputs_return_a_specific_ipc_error(self):
        import json
        from mkr_engine import serve

        for changes in ({"objective": []}, {"gain": 10 ** 400},
                        {"family": "Integrateur", "tauSeconds": None, "standardErrors": []}):
            with self.subTest(fields=tuple(changes)):
                message = dict(version=1, requestId="guided-check", operation="recommend_tuning",
                               payload=request(**changes))
                reply = json.loads(serve(json.dumps(message).encode()))
                self.assertEqual(reply["requestId"], "guided-check")
                self.assertEqual(reply["error"]["code"], "recommend_input")
                self.assertNotIn("result", reply)

    def test_hostile_inputs_are_rejected(self):
        for changes in ({"derivativeFilterRatio": 0.5}, {"processVariationPercent": 0}, {"processVariationPercent": 60}, {"processVariationPercent": "30"}, {"gain": 0}, {"gain": float("nan")}, {"tauSeconds": -1.0}, {"delaySeconds": -1.0}, {"sampleSeconds": 0},
                        {"objective": "fast"}, {"family": "ARX"}, {"family": "SOPDT"}, {"secondTauSeconds": 3.0},
                        {"family": "SOPDT", "secondTauSeconds": 80.0}, {"gain": "2"}, {"gain": True},
                        {"standardErrors": {"gain": -1, "tauSeconds": 1, "delaySeconds": 1}}, {"standardErrors": {"gain": 1}}):
            with self.assertRaises(ValueError, msg=str(changes)):
                calculate(request(**changes))
        with self.assertRaises(ValueError):
            calculate({"gain": 1})


if __name__ == "__main__":
    unittest.main()
