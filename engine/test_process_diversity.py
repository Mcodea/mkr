"""Universality checks: MKR must not be tuned to one bench.

Synthetic processes of very different time scales, delay ratios, gain signs and structures are identified
through the same public path as the application (suggest_criteria -> assess_model). Acceptance criteria are fixed by the
principle, not by the outcome: the right structure is accepted and recovered, a wrong structure is refused.
Noise is seeded, so the results are reproducible.
"""
import base64
import itertools
import math
import unittest
from datetime import datetime, timedelta, timezone

import numpy as np

from validation_gate import assess, suggest


def source(day, response, amplitude, cadence, sigma, rng, pre=10, post=120, base=50.0):
    start = datetime(2026, 3, day, tzinfo=timezone.utc)
    lines = ["timestamp,mv,pv"]
    for i in range(pre + post):
        t = (i - pre) * cadence
        value = base + amplitude * response(t) + rng.normal(0, sigma)
        lines.append(f"{(start + timedelta(seconds=i * cadence)).isoformat()},{amplitude if t >= 0 else 0},{value}")
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",",
                timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


def first_order(gain, tau, delay):
    return lambda t: gain * (1 - math.exp(-max(t - delay, 0) / tau))


def integrator(gain, delay):
    return lambda t: gain * max(t - delay, 0)


def second_order(gain, slow, fast, delay):
    return lambda t: gain * (1 - (slow * math.exp(-(t - delay) / slow) - fast * math.exp(-(t - delay) / fast)) / (slow - fast)) if t > delay else 0.0


def judge(family, response, cadence, sigma, post=120, tolerance=None):
    rng = np.random.default_rng(3)
    trials = dict(training=source(1, response, 2.0, cadence, sigma, rng, post=post),
                  validation=source(2, response, -1.5, cadence, sigma, rng, post=post),
                  mvTolerance=0.01, pvTolerance=tolerance or 6 * sigma)
    criteria = suggest(trials)
    return assess(dict(family=family, trials=trials, maximumRmse=criteria["maximumRmse"],
                       maximumResidualLagOne=criteria["maximumResidualLagOne"]))


class FirstOrderAcrossScales(unittest.TestCase):
    def test_parameters_are_recovered_from_seconds_to_hours_and_for_both_gain_signs(self):
        for tau, ratio, gain in itertools.product((2.0, 60.0, 3600.0), (0.05, 0.5, 2.0), (0.8, -3.0)):
            delay = ratio * tau
            cadence = (delay + 6 * tau) / 110          # the same relative sampling, whatever the time scale
            sigma = 0.01 * abs(gain * 2.0)
            with self.subTest(tau=tau, delay_ratio=ratio, gain=gain):
                result = judge("FOPDT", first_order(gain, tau, delay), cadence, sigma)
                report = result["report"]
                self.assertTrue(result["assessment"]["accepted"], result["assessment"]["reasons"])
                self.assertAlmostEqual(report["gain"] / gain, 1, delta=0.02)
                self.assertAlmostEqual(report["tauSeconds"] / tau, 1, delta=0.05)
                self.assertLessEqual(abs(report["delaySeconds"] - delay), max(0.15 * delay, 2 * cadence))

    def test_delay_dominant_and_coarsely_sampled_processes_are_still_accepted_with_their_warning(self):
        dominant = judge("FOPDT", first_order(1.5, 5, 40), 1.0, 0.02)
        self.assertTrue(dominant["assessment"]["accepted"])
        coarse = judge("FOPDT", first_order(1.5, 20, 4), 20.0, 0.015)             # one sample per time constant
        self.assertTrue(coarse["assessment"]["accepted"])
        self.assertIn("delay_resolution", coarse["report"]["warnings"])           # the loss of resolution is announced


class StructuresAreTold(unittest.TestCase):
    def test_a_level_process_is_refused_as_first_order_and_accepted_as_integrator(self):
        level = integrator(0.05, 3)
        self.assertFalse(judge("FOPDT", level, 1.0, 0.02)["assessment"]["accepted"])
        accepted = judge("Integrateur", level, 1.0, 0.02)
        self.assertTrue(accepted["assessment"]["accepted"])
        self.assertAlmostEqual(accepted["report"]["gain"], 0.05, delta=0.002)

    def test_a_first_order_process_is_not_accepted_as_integrator_nor_as_second_order(self):
        response = first_order(1.5, 20, 4)
        with self.assertRaisesRegex(ValueError, "fit_integrator_tail"):
            judge("Integrateur", response, 1.0, 0.015)
        self.assertFalse(judge("SOPDT", response, 1.0, 0.015)["assessment"]["accepted"])   # a second pole is not supported by the data

    def test_a_second_order_process_is_refused_as_first_order_once_the_data_can_tell_the_difference(self):
        response = second_order(2, 30, 6, 2)
        # With enough resolution the first-order holdout prediction fails and the second-order model is accepted.
        for sigma in (0.005, 0.001):
            with self.subTest(sigma=sigma):
                refused = judge("FOPDT", response, 1.0, sigma, post=250)
                self.assertFalse(refused["assessment"]["accepted"])
                self.assertIn("validation_residuals", refused["assessment"]["reasons"])
                self.assertTrue(judge("SOPDT", response, 1.0, sigma, post=250)["assessment"]["accepted"])
        # With the sample-size dependent whiteness bound (Ljung, 99 %), 250 samples are enough to see the unmodelled
        # second pole even under heavier noise (1 % of the response): the first-order model is refused, the second-order one accepted.
        heavy = judge("FOPDT", response, 1.0, 0.02, post=250)
        self.assertFalse(heavy["assessment"]["accepted"])
        self.assertIn("validation_residuals", heavy["assessment"]["reasons"])
        self.assertTrue(judge("SOPDT", response, 1.0, 0.02, post=250)["assessment"]["accepted"])

    def test_noise_that_hides_the_response_is_refused_not_fitted(self):
        with self.assertRaisesRegex(ValueError, "fit_response"):
            judge("FOPDT", first_order(1.5, 20, 4), 1.0, 0.45)


def field_source(day, amplitude, sigma, rng, drift=0.0, quantum=0.0, disturbance=None, mv_after=None, pre=10, post=120):
    """First-order process (K 1.5, tau 20 s, delay 4 s) with a field defect."""
    start = datetime(2026, 3, day, tzinfo=timezone.utc)
    response = first_order(1.5, 20, 4)
    lines = ["timestamp,mv,pv"]
    for i in range(pre + post):
        t = float(i - pre)
        value = 50 + amplitude * response(t) + rng.normal(0, sigma) + drift * t
        if disturbance and t >= disturbance[0]:
            value += disturbance[1]
        if quantum:
            value = round(value / quantum) * quantum
        mv = amplitude if t >= 0 else 0
        if mv_after and t >= mv_after[0]:
            mv = mv_after[1](mv, t)
        lines.append(f"{(start + timedelta(seconds=i)).isoformat()},{mv},{value}")
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",",
                timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


def field_judge(tolerance=0.09, **defect):
    rng = np.random.default_rng(3)
    trials = dict(training=field_source(1, 2.0, 0.015, rng, **defect), validation=field_source(2, -1.5, 0.015, rng, **defect),
                  mvTolerance=0.01, pvTolerance=tolerance)
    criteria = suggest(trials)
    return assess(dict(family="FOPDT", trials=trials, maximumRmse=criteria["maximumRmse"],
                       maximumResidualLagOne=criteria["maximumResidualLagOne"]))


class FieldDefectsAreNotFittedInSilence(unittest.TestCase):
    def test_the_clean_reference_is_accepted(self):
        self.assertTrue(field_judge()["assessment"]["accepted"])

    def test_a_slow_drift_a_load_disturbance_or_a_late_step_in_the_process_are_refused(self):
        for name, defect in (("drift 0.002/s", dict(drift=0.002)),
                             ("disturbance +0.3 during the response", dict(disturbance=(40, 0.3))),
                             ("disturbance +1.0 during the response", dict(disturbance=(25, 1.0)))):
            with self.subTest(defect=name):
                result = field_judge(**defect)
                self.assertFalse(result["assessment"]["accepted"])
                self.assertIn("validation_residuals", result["assessment"]["reasons"])
                self.assertIn("correlated_residuals", result["report"]["warnings"])    # the cause is announced, not hidden

    def test_a_drift_larger_than_the_response_is_refused_as_not_a_step_response(self):
        with self.assertRaisesRegex(ValueError, "fit_response"):
            field_judge(tolerance=0.5, drift=0.01)

    def test_a_fine_sensor_resolution_is_harmless_and_a_coarse_one_is_refused(self):
        self.assertTrue(field_judge(quantum=0.05)["assessment"]["accepted"])
        with self.assertRaisesRegex(ValueError, "fit_response"):
            field_judge(tolerance=1.0, quantum=0.5)

    def test_an_output_that_saturates_or_drifts_after_the_step_is_not_an_identification_trial(self):
        # MV leaves its plateau (saturation, a manual correction, a controller back in automatic): the step is not clean.
        for name, change in (("saturation 30 s after the step", (30, lambda mv, t: 0.8 * mv)),
                             ("slow output drift", (20, lambda mv, t: mv + 0.01 * (t - 20)))):
            with self.subTest(defect=name):
                with self.assertRaisesRegex(ValueError, "fit_step"):
                    field_judge(mv_after=change)


if __name__ == "__main__":
    unittest.main()
