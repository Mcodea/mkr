"""End-to-end scientific campaign on synthetic reference processes with KNOWN true parameters.

Chain under test (the public path of the application): noisy open-loop step data -> suggest_criteria -> assess_model (independent
validation trial) -> recommend_tuning. The recommended controller is then judged on the TRUE process, never on MKR's own simulator:
by a dense frequency sweep and by an explicit time-domain simulation, both written in test_recommend.py.

Acceptance criteria, fixed BEFORE the runs (objective "balanced", Ms design limit 1.7):
  C1  the validation gate accepts the right structure; gain within 3 %, slow time constant within 10 % of the truth;
  C2  the recommender returns a tuning (it must not refuse well-identified, well-excited processes);
  C3  on the TRUE process the closed loop settles on the set-point (final error < 2 % of the step);
  C4  on the TRUE process the peak sensitivity stays below 2.0 (the loosest usual design ceiling, Astrom & Hagglund 2006)
      and does not exceed the worst case the recommender announced by more than 0.05 (the announced evidence must bound the truth).
Noise is seeded: results are reproducible. This is evidence on synthetic data only, not an industrial qualification.
"""
import unittest

import numpy as np

from recommend import calculate
from test_process_diversity import first_order, integrator, judge, second_order
from test_recommend import closed_loop_settles, sensitivity_peak

CASES = [
    ("flow", "FOPDT", first_order(0.8, 4, 1), dict(k=0.8, taus=[4], d=1), 0.23, 0.016),
    ("pressure, negative gain", "FOPDT", first_order(-1.2, 25, 4), dict(k=-1.2, taus=[25], d=4), 1.3, 0.024),
    ("temperature, slow", "FOPDT", first_order(1.5, 300, 40), dict(k=1.5, taus=[300], d=40), 17, 0.03),
    ("chemical dosing", "FOPDT", first_order(2.5, 60, 25), dict(k=2.5, taus=[60], d=25), 3.6, 0.05),
    ("chlorination, long dead time", "FOPDT", first_order(1.0, 120, 240), dict(k=1.0, taus=[120], d=240), 8.6, 0.02),
    ("noisy flow", "FOPDT", first_order(0.8, 4, 1), dict(k=0.8, taus=[4], d=1), 0.23, 0.04),
    ("level, integrating", "Integrateur", integrator(0.02, 5), dict(k=0.02, taus=[], d=5), 1.0, 0.01),
    ("temperature, second order", "SOPDT", second_order(2, 120, 30, 10), dict(k=2, taus=[120, 30], d=10), (10 + 6 * 120) / 110, 0.04),
]


def nonlinear_plant(kp, ti, sp_from, sp_to, theta, sampling, tau, horizon=1500.0):
    """Control valve with a rising characteristic: y' = (g(u(t - theta)) - y)/tau, g(u) = u (1 + 0.25 u), local gain 1 + 0.5 u (1 to 2 on u in [0, 2]).
    Written here, independent of the engine. Returns (final error, overshoot fraction) of a set-point step from sp_from to sp_to."""
    def g(u):
        return u * (1 + 0.25 * u)

    dt = 0.25
    buffer = [0.0] * (int(round((theta + sampling / 2) / dt)) + 1)
    y, uss, integral, peak = g(sp_from), sp_from, 0.0, 0.0
    for _ in range(int(horizon / dt)):
        error = g(sp_to) - y
        integral += error * dt
        u = min(max(uss + kp * (error + integral / ti), 0.0), 2.0)
        buffer.append(u)
        y += dt * (g(buffer.pop(0)) - y) / tau
        peak = max(peak, (y - g(sp_from)) / (g(sp_to) - g(sp_from)))
    return abs(g(sp_to) - y), peak - 1.0


class VariableDynamics(unittest.TestCase):
    """A process whose gain varies by +/- 33 % around the identified operating point (u = 1, local gain 1.5).
    Criteria fixed before the runs: with the variation DECLARED (35 %), the guided tuning settles at every operating point of a sweep of
    set-point steps over the whole range (final error < 1 % of the step) and its worst overshoot is not larger than the worst overshoot of
    the tuning computed WITHOUT the declaration."""

    def test_a_declared_process_variation_keeps_the_loop_settled_and_calmer_across_the_range(self):
        steps = ((0.2, 0.6), (0.6, 1.0), (1.0, 1.5), (1.4, 1.9), (1.9, 1.4), (1.5, 0.5))
        worst = {}
        for variation in (None, 35.0):
            tuning = calculate(dict(family="FOPDT", gain=1.5, tauSeconds=30.0, delaySeconds=6.0, secondTauSeconds=None, sampleSeconds=1.0,
                                    standardErrors=None, objective="balanced", derivativeFilterRatio=None, processVariationPercent=variation, optimise=False))
            c = tuning["recommendation"]["controller"]
            overshoots = []
            for start, end in steps:
                final_error, overshoot = nonlinear_plant(c["kp"], c["tiSeconds"], start, end, 6.0, 1.0, 30.0)
                step_size = abs(end * (1 + 0.25 * end) - start * (1 + 0.25 * start))
                self.assertLess(final_error / step_size, 0.01, (variation, start, end, final_error))
                overshoots.append(overshoot)
            worst[variation] = max(overshoots)
        self.assertLessEqual(worst[35.0], worst[None] + 1e-9)


class ReferenceCampaign(unittest.TestCase):
    def test_each_reference_process_is_identified_tuned_and_judged_on_the_true_process(self):
        for name, family, response, truth, cadence, sigma in CASES:
            with self.subTest(process=name):
                result = judge(family, response, cadence, sigma, post=250 if family == "SOPDT" else 120)
                report = result["report"]
                self.assertTrue(result["assessment"]["accepted"], result["assessment"]["reasons"])                      # C1
                self.assertAlmostEqual(report["gain"] / truth["k"], 1, delta=0.03)
                if family != "Integrateur":
                    self.assertAlmostEqual(report["tauSeconds"] / truth["taus"][0], 1, delta=0.10)

                interval = report.get("delayIntervalSeconds")
                payload = dict(family=family, gain=float(report["gain"]),
                               tauSeconds=None if family == "Integrateur" else float(report["tauSeconds"]),
                               delaySeconds=float(interval[1] if interval else report["delaySeconds"]),
                               secondTauSeconds=float(report["secondTauSeconds"]) if family == "SOPDT" else None,
                               sampleSeconds=cadence, objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False,
                               # the application's contract: gain, slow time constant and delay errors; no time constant for the integrator
                               standardErrors=None if not report.get("standardErrors") else dict(
                                   gain=float(report["standardErrors"]["gain"]),
                                   tauSeconds=0 if family == "Integrateur" else float(report["standardErrors"]["tauSeconds"]),
                                   delaySeconds=float(report["standardErrors"]["delaySeconds"])))
                recommendation = calculate(payload)
                self.assertEqual(recommendation["decision"], "recommended", recommendation.get("reasons"))             # C2
                rec = recommendation["recommendation"]
                c = rec["controller"]
                sampling = 0.5 * cadence                                          # the sampled loop adds half a period of delay
                if family == "Integrateur":
                    w = np.logspace(-4, 2, 30000)
                    s = 1j * w
                    loop = c["kp"] * (1 + 1 / (c["tiSeconds"] * s)) * truth["k"] * np.exp(-s * (truth["d"] + sampling)) / s
                    peak = float(np.max(1 / np.abs(1 + loop)))
                    horizon = 40 * (rec["responseSeconds"] + recommendation["effectiveDelaySeconds"])
                else:
                    peak = sensitivity_peak(abs(truth["k"]), truth["taus"], truth["d"] + sampling, abs(c["kp"]), c["tiSeconds"], c["tdSeconds"], c["filterSeconds"])
                    horizon = 12 * (rec["responseSeconds"] + recommendation["effectiveDelaySeconds"]) + 8 * max(truth["taus"])
                final_error, _ = closed_loop_settles(abs(truth["k"]), truth["taus"], truth["d"] + sampling, abs(c["kp"]), c["tiSeconds"], c["tdSeconds"], horizon, c["filterSeconds"])
                self.assertLess(final_error, 0.02, (name, final_error))                                               # C3
                self.assertLess(peak, 2.0, (name, peak))                                                              # C4
                self.assertLessEqual(peak, rec["worstPeakSensitivity"] + 0.05, (name, peak, rec["worstPeakSensitivity"]))

    def test_a_process_that_the_data_do_not_identify_gets_no_tuning(self):
        with self.assertRaises(ValueError):                       # noise hides the response: identification refuses, so nothing is tuned
            judge("FOPDT", first_order(1.5, 20, 4), 1.0, 0.45)


if __name__ == "__main__":
    unittest.main()
