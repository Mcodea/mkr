"""Guided tuning: equations against the published SIMC rules, robustness checked by an INDEPENDENT implementation
(dense frequency sweep and a time-domain simulation written in this file, not the module's own margin search),
minimality of the recommendation, refusals and hostile inputs."""
import math
import unittest

import numpy as np

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

    def test_the_optimisation_is_off_by_default_and_only_for_pi(self):
        self.assertIsNone(calculate(request())["optimized"])
        pid = calculate(request(family="SOPDT", secondTauSeconds=15.0, optimise=True))
        self.assertIsNone(pid["optimized"])
        self.assertIn("optimisation_covers_pi_only", pid["warnings"])
        with self.assertRaises(ValueError):
            calculate(request(optimise="yes"))


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
