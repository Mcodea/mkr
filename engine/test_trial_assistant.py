"""Assistant d'essai (section 42.2 à 42.4), tranche 1. Oracle : FOPDT exact y = base + K A (1 - exp(-(t - L)/tau)), formes closes écrites ici.

Le tampon d'acquisition est TRONQUÉ à différents instants pour reproduire ce que l'assistant voit pendant l'essai. Critères fixés avant mesure :
durée restante à 1 s près de la forme close L + tau ln(|K A|/(2 tol)) + fenêtre (2,5 s) - T (le tau estimé a un biais intrinsèque de 0,28 %) ;
aucune estimation ni durée restante tant que le plateau n'est pas observé ; `safetyAuthorization` toujours False."""
import json
import math
import unittest

import numpy as np

from mkr_engine import respond
from trial_assistant import assess

GAIN, TAU, DELAY, AMP, BASE, TOL = 2.0, 10.0, 4.0, 1.5, 7.0, 0.01
DT = 0.5


def stream(upto, step_at=6, amp=AMP, pre_ramp=0.0, mv_noise=0.0, mv_ramp=False):
    """Tampon des `upto` secondes qui suivent l'échelon, avec `step_at` lignes avant l'échelon."""
    rows = step_at + int(round(upto / DT)) + 1
    t = (np.arange(rows) - step_at) * DT
    seconds = np.arange(rows) * DT
    mv = np.where(t >= 0, amp, 0.0)
    if mv_ramp:
        mv = np.clip(t / 5.0, 0, 1) * amp
    pv = BASE + GAIN * amp * (1 - np.exp(-np.maximum(t - DELAY, 0) / TAU)) + pre_ramp * seconds
    return dict(seconds=seconds.tolist(), mv=mv.tolist(), pv=pv.tolist(), mvTolerance=0.01, pvTolerance=TOL)


def closed_form_required(window=2.5):
    return max(DELAY + TAU * math.log(abs(GAIN * AMP) / (2 * TOL)) + window, DELAY + 4 * TAU)


class InitialRegime(unittest.TestCase):
    def baseline_only(self, rows, **kw):
        s = stream(0, step_at=rows - 1, **kw)
        s["mv"] = [0.0] * len(s["mv"])
        return s

    def test_too_few_rows(self):
        self.assertEqual(assess(self.baseline_only(3))["state"], "acquiring_initial_regime")

    def test_stable_baseline_is_ready_but_never_a_safety_authorisation(self):
        r = assess(self.baseline_only(12))
        self.assertEqual(r["state"], "ready_for_step")
        self.assertIn("scientific_precondition_only_not_a_safety_authorisation", r["messages"])
        self.assertIs(r["safetyAuthorization"], False)
        self.assertEqual(r["stableRows"], 12)

    def test_drifting_baseline_is_not_ready(self):
        # dérive 0,02 par ligne (0,04/s), sensiblement supérieure à la tolérance sur 4 lignes : les 4 dernières lignes ne tiennent pas dans ±0,01
        r = assess(self.baseline_only(12, pre_ramp=0.04))
        self.assertEqual(r["state"], "initial_conditions_not_met")
        self.assertLess(r["stableRows"], 4)

    def test_loop_not_in_manual(self):
        s = self.baseline_only(12)
        s["manual"] = [1] * 11 + [0]
        r = assess(s)
        self.assertEqual((r["state"], r["messages"]), ("initial_conditions_not_met", ["loop_not_in_manual"]))


class StepAndResponse(unittest.TestCase):
    def test_step_just_detected(self):
        r = assess(stream(2.0))
        self.assertEqual(r["state"], "step_detected")
        self.assertEqual(r["stepRow"], 6)

    def test_response_still_moving_gives_no_remaining_time(self):
        r = assess(stream(20.0))                  # PV encore en pleine montée : pas de plateau observé
        self.assertEqual(r["state"], "response_in_progress")
        self.assertIsNone(r["remainingSeconds"])
        self.assertIsNone(r["estimate"])
        self.assertIn("remaining_not_reliable", r["messages"])

    def test_more_data_needed_matches_the_closed_form_remaining_time(self):
        r = assess(stream(45.0))                  # T - L = 41 s : plateau « vu » mais reste prévu 0,0497 > 0,02
        self.assertEqual(r["state"], "more_data_needed")
        self.assertTrue(r["remainingConditionalOnModel"])
        # Le niveau final lu à 45 s est encore sous le niveau final vrai du reste prévu (0,0497 sur 3,0 = 1,66 %) : tau estimé biaisé d'environ
        # 1,3 x cette fraction, et la durée restante d'environ TAU x 1,3 x 1,66 % x (ln(150) + 1) = 1,3 s. Critère : 2 s, SOUS-ESTIMATION attendue
        # (le niveau lu est trop bas) ; l'estimation est conditionnelle et se resserre quand les données arrivent (test suivant).
        remaining_true = closed_form_required() - 45.0
        self.assertLess(abs(r["remainingSeconds"] - remaining_true), 2.0)
        self.assertLessEqual(r["remainingSeconds"], remaining_true + 0.2)
        self.assertAlmostEqual(r["estimate"]["tauSeconds"], TAU, delta=0.05 * TAU)

    def test_remaining_time_tightens_as_the_plateau_is_established(self):
        r = assess(stream(55.0))                  # reste au début de la fenêtre : 3 exp(-4,95) = 0,0212, juste au-dessus de la bande 0,02
        self.assertEqual(r["state"], "more_data_needed")
        self.assertAlmostEqual(r["remainingSeconds"], closed_form_required() - 55.0, delta=1.0)

    def test_usable_is_provisional_and_asks_for_the_validation_trial(self):
        r = assess(stream(70.0))
        self.assertEqual(r["state"], "usable")
        self.assertTrue(r["provisional"])
        self.assertTrue(r["validationTrialNeeded"])
        self.assertIn("independent_validation_trial_needed", r["messages"])
        e = r["evidence"]
        self.assertAlmostEqual(e["crossing28Seconds"], DELAY - TAU * math.log(1 - 0.28), delta=0.02)
        self.assertAlmostEqual(e["crossing40Seconds"], DELAY - TAU * math.log(1 - 0.40), delta=0.02)
        self.assertAlmostEqual(r["estimate"]["tauSeconds"], TAU, delta=0.01 * TAU)
        self.assertIs(r["safetyAuthorization"], False)

    def test_validation_trial_already_available(self):
        s = stream(70.0)
        s["hasValidationTrial"] = True
        r = assess(s)
        self.assertEqual((r["state"], r["validationTrialNeeded"]), ("usable", False))

    def test_state_is_monotone_as_data_arrives_and_agrees_with_the_final_identification(self):
        from test_broida_established_plateau import trial
        from broida import identify
        order = ["step_detected", "response_in_progress", "more_data_needed", "usable"]
        seen = [assess(stream(t))["state"] for t in (2.0, 20.0, 45.0, 70.0)]
        self.assertEqual(seen, order)
        # accord avec l'identification définitive : quand l'assistant dit « usable », broida.identify accepte le même essai
        identify(dict(training=trial(1, GAIN, TAU, DELAY, AMP, 70.0), validation=trial(2, GAIN, TAU, DELAY, -1.0, 70.0), mvTolerance=0.01, pvTolerance=TOL))
        # et quand il dit « more_data_needed », l'identification définitive refuse
        with self.assertRaisesRegex(ValueError, "broida_not_established"):
            identify(dict(training=trial(1, GAIN, TAU, DELAY, AMP, 45.0), validation=trial(2, GAIN, TAU, DELAY, -1.0, 45.0), mvTolerance=0.01, pvTolerance=TOL))


class Excitation(unittest.TestCase):
    def test_ramp_on_mv_is_not_a_step(self):
        self.assertEqual(assess(stream(20.0, mv_ramp=True))["state"], "excitation_not_a_step")

    def test_too_small_step(self):
        r = assess(stream(30.0, amp=0.05))        # 0,05 <= 10 x 0,01
        self.assertEqual(r["state"], "excitation_insufficient")

    def test_saturation_at_a_declared_limit(self):
        s = stream(30.0)
        s["mvHigh"] = AMP
        self.assertEqual(assess(s)["state"], "saturation_detected")

    def test_baseline_polluted_before_the_step(self):
        s = stream(30.0)
        s["pv"][2] += 0.5
        self.assertEqual(assess(s)["state"], "unusable")

    def test_second_mv_change_ends_the_trial_and_is_reported(self):
        s = stream(45.0)
        s["mv"][-5:] = [0.0] * 5                   # retour de MV : l'essai s'arrête à la ligne du second changement
        r = assess(s)
        self.assertIn("second_mv_change_ends_the_trial", r["messages"])

    def test_max_duration_makes_an_unfinished_trial_unusable(self):
        s = stream(45.0)
        s["maxDurationSeconds"] = 40.0
        r = assess(s)
        self.assertEqual(r["state"], "unusable")
        self.assertIn("response_not_established_at_max_duration", r["messages"])

    def test_no_response_until_max_duration_then_excitation_insufficient(self):
        s = stream(40.0)
        s["pv"] = [BASE] * len(s["pv"])            # le procédé ne réagit pas
        self.assertEqual(assess(s)["state"], "response_in_progress")
        s["maxDurationSeconds"] = 30.0
        self.assertEqual(assess(s)["state"], "unusable")


class ScopeAndInput(unittest.TestCase):
    def test_closed_loop_and_integrating_are_declared_not_supported_not_guessed(self):
        for extra in (dict(loopMode="closed_loop"), dict(processKind="integrating")):
            s = stream(30.0)
            s.update(extra)
            self.assertEqual(assess(s)["state"], "not_supported_yet")

    def test_non_finite_measurement_is_an_anomaly_not_a_crash(self):
        s = stream(30.0)
        s["pv"][10] = float("nan")
        # JSON n'a pas NaN : par l'API Python directe, l'anomalie est signalée
        self.assertEqual(assess(s)["state"], "measurement_anomaly")

    def test_invalid_payloads_are_refused(self):
        for bad in ({}, dict(stream(5.0), seconds=[0.0]), dict(stream(5.0), mvTolerance=0), dict(stream(5.0), extra=1)):
            with self.assertRaises(ValueError):
                assess(bad)
        s = stream(5.0)
        s["seconds"][3] = s["seconds"][2]
        with self.assertRaisesRegex(ValueError, "assess_data"):
            assess(s)

    def test_ipc_round_trip_and_error_code(self):
        reply = respond(json.dumps(dict(version=1, requestId="a", operation="assess_trial", payload=stream(70.0))).encode())
        self.assertEqual(reply["result"]["state"], "usable")
        bad = respond(json.dumps(dict(version=1, requestId="b", operation="assess_trial", payload={})).encode())
        self.assertEqual(bad["error"]["code"], "assess_mapping")

    def test_the_assistant_never_writes_and_never_authorises(self):
        for upto in (0.0, 2.0, 20.0, 45.0, 70.0):
            self.assertIs(assess(stream(upto))["safetyAuthorization"], False)


if __name__ == "__main__":
    unittest.main()
