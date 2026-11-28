"""Reproductibilité de l'étude de validation (protocole docs/science/lot4/protocole-validation-critere.md, règle d'adoption §7 v) et tests unitaires de la logique d'analyse.

Test de REPRODUCTIBILITÉ (pas un oracle) : cinq cas de l'étude, régénérés par leur graine, redonnent les mêmes valeurs qu'à la rédaction (tolérance relative 1e-6, pour
absorber les écarts de bibliothèque entre plateformes). Les valeurs attendues sont des sorties du code, figées ici pour détecter toute dérive involontaire de l'étude ;
elles ne prouvent pas que ces valeurs sont justes (l'exactitude des outils est vérifiée par test_validation_study_tools : formes closes et accord avec stability_margins).
Les tests de l'analyse utilisent des lignes écrites à la main dont le résultat est connu par construction."""
import json
import math
import sys
import unittest
from pathlib import Path

import numpy as np

STUDY = Path(__file__).resolve().parent.parent / "tests" / "reference" / "validation_study"
sys.path.insert(0, str(STUDY))

import analysis  # noqa: E402
import evaluate  # noqa: E402
import plants  # noqa: E402
import truth  # noqa: E402

EXPECTED = {
    "P1/200/0": {
        "gain": -1.2826673735979512,
        "tau": 5.624229004601675,
        "accepted": True,
        "ms": 1.4691794263227942,
        "stable": True
    },
    "P2/200/0": {
        "gain": -0.5155612372338093,
        "tau": 69.42708348485469,
        "accepted": True,
        "ms": 1.5983495951465758,
        "stable": True
    },
    "P3/200/1": {
        "gain": 1.1899803971893912,
        "tau": 67.14611345943466,
        "accepted": False,
        "ms": 1.6339095623464184,
        "stable": True
    },
    "P4/200/0": {
        "gain": -9.455572456889765,
        "tau": 9.40963753877634,
        "accepted": False,
        "ms": 1.6170407271197584,
        "stable": True
    },
    "P6/200/1": {
        "gain": -0.26653018576252663,
        "tau": 7.514366554528865,
        "accepted": True,
        "ms": 1.5992663222019552,
        "stable": True
    }
}


class Reproducibility(unittest.TestCase):
    def test_five_cases_are_reproduced_from_their_seed(self):
        for key, expected in EXPECTED.items():
            population, snr, index = key.split("/")
            with self.subTest(case=key):
                row = evaluate.run_case(population, int(snr), int(index))
                self.assertAlmostEqual(row["model"]["gain"] / expected["gain"], 1.0, delta=1e-6)
                self.assertAlmostEqual(row["model"]["tau"] / expected["tau"], 1.0, delta=1e-6)
                self.assertEqual(row["gate"]["accepted"], expected["accepted"])
                self.assertEqual(row["truth"]["stable"], expected["stable"])
                self.assertAlmostEqual(row["truth"]["ms"] / expected["ms"], 1.0, delta=1e-6)

    def test_a_case_the_engine_cannot_identify_is_recorded_not_dropped(self):
        row = evaluate.run_case("P3", 200, 0)
        self.assertEqual(row["identification_error"], "fit_baseline")
        self.assertNotIn("gate", row)

    def test_the_same_case_is_identical_twice(self):
        a, b = evaluate.run_case("P2", 200, 0), evaluate.run_case("P2", 200, 0)
        self.assertEqual(a["model"], b["model"])
        self.assertEqual(a["truth"], b["truth"])

    def test_calibration_and_evaluation_seeds_are_disjoint(self):
        # indices < 1000 : calibration ; >= 1000 : évaluation ; la graine dépend de l'indice
        self.assertNotEqual(evaluate.case_rng("P1", 200, 5).bit_generator.state["state"]["state"], evaluate.case_rng("P1", 200, 1005).bit_generator.state["state"]["state"])


class StudyTools(unittest.TestCase):
    """Exactitude des outils de l'étude, indépendamment du moteur : forme close et accord avec stability_margins."""

    def test_simulator_matches_the_closed_form(self):
        ts, n = 0.5, 400
        t = np.arange(n) * ts
        y = plants.fopdt(2.0, 10.0, 3.0).simulate(np.ones(n), ts)
        self.assertLess(float(np.max(np.abs(y - 2.0 * (1 - np.exp(-np.maximum(t - 3.0, 0) / 10.0)) * (t > 3.0)))), 1e-9)

    def test_true_ms_agrees_with_the_engine_margins(self):
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        import stability_margins
        for gain, tau, delay, kp, ti, ts in ((2.0, 10.0, 3.0, 1.0, 10.0, 0.2), (-1.5, 40.0, 8.0, -4.0, 40.0, 0.8), (0.5, 5.0, 0.5, 6.0, 5.0, 0.1)):
            mine = truth.true_ms(plants.fopdt(gain, tau, delay), ts, kp, ti)
            engine = stability_margins.analyze(gain, [tau], delay + ts / 2, kp, ti, 0.0)["peakSensitivity"]
            self.assertAlmostEqual(mine / engine, 1.0, delta=1e-5)

    def test_nyquist_agrees_with_the_time_domain_on_stable_and_unstable_loops_of_several_plant_types(self):
        cases = ((plants.fopdt(2.0, 10.0, 3.0), 0.2, 1.0, 10.0), (plants.fopdt(2.0, 10.0, 3.0), 0.2, 3.5, 10.0), (plants.sopdt(1.0, 20.0, 4.0, 2.0), 0.4, 2.0, 20.0),
                 (plants.lag_chain(1.0, 30.0, 4, 3.0), 0.6, 4.0, 30.0), (plants.inverse_response(1.0, 20.0, 4.0, 3.0, 2.0), 0.4, 3.0, 20.0))
        seen = set()
        for plant, ts, kp, ti in cases:
            time_stable = truth.closed_loop(plant, ts, kp, ti)[0]
            nyquist, zeros = truth.nyquist_stable(plant, ts, kp, ti)
            self.assertIsNotNone(nyquist, f"indécidable : zéros {zeros}")
            self.assertEqual(nyquist, time_stable)
            seen.add(time_stable)
        self.assertEqual(seen, {True, False})                  # le jeu contient des boucles stables ET instables

    def test_an_unstable_loop_is_detected_in_time_domain_even_when_ms_is_finite(self):
        plant = plants.fopdt(2.0, 10.0, 3.0)
        stable, _, _ = truth.closed_loop(plant, 0.2, 1.0, 10.0)
        unstable, itae, _ = truth.closed_loop(plant, 0.2, 3.5, 10.0)
        self.assertTrue(stable)
        self.assertFalse(unstable)
        self.assertTrue(math.isinf(itae))


def behaviour(ms=1.5, time_stable=True, nyquist_stable=True):
    return dict(time_stable=time_stable, nyquist_stable=nyquist_stable, nyquist_zeros=0.0, ms=ms, agree=(time_stable == nyquist_stable))


def row(ms, stable=True, accepted=True, nse=0.0, lb_p=0.5, cross=0.0, bound=0.1, reasons=(), e_ms=1.5, c7_ms=1.5, population="Px", index=1001, nyquist=None, alt=None, robust=None):
    return dict(info=dict(population=population), snr=100, index=index,
                gate=dict(accepted=accepted, reasons=list(reasons), rmse=0.01, lag1=0.0, max_rmse=0.02, max_lag1=0.1),
                stats=dict(nse=nse, ljung_box_p=lb_p, cross_max=cross, cross_bound=bound),
                truth=dict(stable=stable, ms=ms, itae=1.0), reference=dict(stable=True, ms=1.6, itae=1.0),
                stability=behaviour(ms, stable, stable if nyquist is None else nyquist), E=dict(worst_ms=e_ms), C7=dict(worst_ms=c7_ms), alt_sopdt=alt, robust=robust)


THRESHOLDS = dict(D=0.01, E=1.8, C7=1.8)


class AnalysisLogic(unittest.TestCase):
    def test_labels_need_stability_by_both_methods_and_never_rest_on_ms_alone(self):
        self.assertTrue(analysis.label(row(1.9), 2.0))
        self.assertFalse(analysis.label(row(2.2), 2.0))
        self.assertTrue(analysis.label(row(2.2), 2.5))
        self.assertFalse(analysis.label(row(1.5, stable=False), 3.0))                    # Ms correct mais instable : inapte
        self.assertFalse(analysis.label(row(1.5, stable=True, nyquist=False), 3.0))      # les deux méthodes divergent : prudence, inapte
        self.assertIsNone(analysis.label(dict(info={}, snr=1, index=1), 2.0))            # pas de vérité : exclu, pas compté

    def test_clopper_pearson_and_the_one_sided_upper_bound_match_closed_forms(self):
        # k = 0 sur n : borne unilatérale exacte 1 - 0,05^(1/n)
        self.assertAlmostEqual(analysis.upper_bound(0, 100), 1 - 0.05 ** (1 / 100), places=12)
        lo, hi = analysis.clopper_pearson(0, 100)
        self.assertEqual(lo, 0.0)
        self.assertAlmostEqual(hi, 1 - 0.025 ** (1 / 100), places=12)
        lo, hi = analysis.clopper_pearson(5, 50)
        self.assertLess(lo, 0.1)
        self.assertGreater(hi, 0.1)
        self.assertEqual(analysis.upper_bound(10, 10), 1.0)
        self.assertIsNone(analysis.clopper_pearson(0, 0))

    def test_inaptes_needed_to_demonstrate_an_objective_with_zero_false_positive(self):
        self.assertEqual(analysis.inaptes_needed(0.05), 59)
        self.assertEqual(analysis.inaptes_needed(0.01), 299)              # démontrer 1 % exige ~300 inaptes (v1.3 §3)
        self.assertLessEqual(analysis.upper_bound(0, 299), 0.01)
        self.assertGreater(analysis.upper_bound(0, 298), 0.01)

    def test_an_observed_rate_below_the_objective_is_not_a_demonstration(self):
        rows = [row(3.0, accepted=False, population="P6") for _ in range(30)] + [row(1.5, accepted=True, population="P1") for _ in range(30)]
        ev = analysis.evaluate(rows, THRESHOLDS, 2.0)
        fp = ev["by"]["A"]["false_positive"]
        self.assertEqual((fp["k"], fp["n"], fp["rate"]), (0, 30, 0.0))     # taux observé 0 %
        self.assertGreater(fp["upper95_onesided"], 0.05)                    # mais borne supérieure ~9,5 % : objectif de 5 % NON démontré
        self.assertFalse(ev["by"]["A"]["demonstrated"]["0.05"])
        many = [row(3.0, accepted=False, population="P6") for _ in range(60)] + [row(1.5, population="P1") for _ in range(10)]
        self.assertTrue(analysis.evaluate(many, THRESHOLDS, 2.0)["by"]["A"]["demonstrated"]["0.05"])      # 60 inaptes refusés : borne 4,9 %
        self.assertFalse(analysis.evaluate(many, THRESHOLDS, 2.0)["by"]["A"]["demonstrated"]["0.01"])     # 1 % exige 299

    def test_decisions_follow_the_declared_rules(self):
        d = analysis.decisions(row(1.5, accepted=False, nse=0.001, reasons=["validation_residuals"]), THRESHOLDS)
        self.assertFalse(d["A"])
        self.assertTrue(d["A1"] and not d["A2"])
        self.assertTrue(d["D"] and d["E"] and d["C7"])
        self.assertTrue(d["A_or_D"] and d["A_or_E"] and d["A_or_C7"])
        self.assertFalse(d["A_and_E"])
        blocked = analysis.decisions(row(1.5, accepted=False, reasons=["parameter_bound"]), dict(D=1.0, E=9.0, C7=9.0))
        self.assertFalse(blocked["A1"] or blocked["D"] or blocked["E"] or blocked["C7"])
        self.assertFalse(analysis.decisions(row(1.5), dict(D=None, E=None, C7=None))["D"])
        worse = analysis.decisions(row(1.5, e_ms=2.5, c7_ms=2.5), THRESHOLDS)
        self.assertFalse(worse["E"] or worse["C7"])

    def test_calibration_gives_the_stated_unsafe_acceptance_for_each_alpha_and_refuses_when_data_is_thin(self):
        rows = [row(3.0, nse=0.01 * (i + 1), e_ms=1.0 + 0.01 * i) for i in range(100)]
        s05, n = analysis.calibrate(rows, "D", 0.05)
        s01, _ = analysis.calibrate(rows, "D", 0.01)
        self.assertEqual(n, 100)
        self.assertEqual(sum(r["stats"]["nse"] <= s05 for r in rows), 5)
        self.assertEqual(sum(r["stats"]["nse"] <= s01 for r in rows), 1)
        self.assertLess(s01, s05)
        self.assertEqual(analysis.calibrate(rows[:10], "D", 0.05)[0], None)

    def test_frozen_thresholds_cannot_be_altered_and_calibration_never_reads_the_evaluation_cases(self):
        calibration = [row(3.0, nse=0.01 * (i + 1), index=i) for i in range(100)]
        evaluation = [row(3.0, nse=0.001, index=1000 + i) for i in range(100)]
        only = analysis.phase_calibration(calibration)
        both = analysis.phase_calibration(calibration + evaluation)
        self.assertEqual(only["sha256"], both["sha256"])                  # les cas d'évaluation ne changent rien aux seuils
        analysis.check_thresholds(only)                                    # intact : accepté
        tampered = json.loads(json.dumps(only))
        tampered["body"]["alphas"]["0.05"]["thresholds"]["D"] = 9.99
        with self.assertRaises(SystemExit):
            analysis.check_thresholds(tampered)                            # altéré : refusé

    def test_unstable_tuning_is_never_counted_as_fit_and_is_reported_among_accepted(self):
        rows = [row(1.5, stable=True, population="P2") for _ in range(30)] + [row(1.5, stable=False, population="P6") for _ in range(10)] + \
               [row(1.5, stable=True, nyquist=False, population="P6") for _ in range(5)]
        ev = analysis.evaluate(rows, THRESHOLDS, 2.0)
        self.assertEqual((ev["n_fit"], ev["n_unfit"]), (30, 15))
        self.assertEqual((ev["n_unstable"], ev["n_stability_uncertain"]), (10, 5))
        self.assertEqual(ev["stability"]["A"], dict(accepted=45, accepted_unstable_or_uncertain=15))       # A accepte tout : 15 réglages douteux acceptés, signalés
        self.assertEqual(ev["by"]["A"]["false_positive"]["k"], 15)

    def test_imperfect_model_with_a_fit_tuning_is_reported_apart_from_an_unfit_tuning(self):
        rows = ([row(1.6, population="P2") for _ in range(20)] + [row(2.8, population="P6") for _ in range(10)] + [row(1.6, population="P1") for _ in range(20)])
        cross = analysis.evaluate(rows, THRESHOLDS, 2.0)["cross"]["A"]
        self.assertEqual((cross["imparfait_reglage_apte_accepte"], cross["imparfait_reglage_apte_total"]), (20, 20))
        self.assertEqual((cross["imparfait_reglage_inapte_accepte"], cross["imparfait_reglage_inapte_total"]), (10, 10))
        self.assertEqual(cross["exact_reglage_inapte_total"], 0)

    def test_auc_is_one_for_perfect_separation_and_one_half_without_discrimination(self):
        self.assertEqual(analysis.auc([5, 6, 7], [1, 2, 3]), 1.0)
        self.assertAlmostEqual(analysis.auc([1, 2, 3], [1, 2, 3]), 0.5)          # 3 paires gagnantes sur 9 + la moitié des 3 égalités = 4,5 / 9
        self.assertAlmostEqual(analysis.auc([1, 1], [1, 1]), 0.5)
        self.assertEqual(analysis.auc([1, 2], [5, 6]), 0.0)
        self.assertIsNone(analysis.auc([], [1]))

    def test_ladder_counts_what_the_alternative_model_and_the_robust_tuning_recover(self):
        recovered = row(2.8, population="P6", accepted=False, robust=behaviour(1.5), alt=None)
        alt_only = row(2.8, population="P6", accepted=False, robust=behaviour(2.9), alt=behaviour(1.6))
        lost = row(2.8, population="P6", accepted=False, robust=None, alt=None)
        out = analysis.ladder([recovered, alt_only, lost], 2.0)["unfit_tuning"]
        self.assertEqual((out["n"], out["robust_fit"], out["alternative_fit"], out["either_fit"], out["alternative_available"]), (3, 1, 1, 2, 1))

    def test_adoption_never_happens_without_real_data_even_when_the_synthetic_evidence_is_strong(self):
        def table(unsafe_diff, rej_hi, upper=0.03):
            by = {name: dict(false_positive=dict(n=100, k=0, rate=0.0, upper95_onesided=upper), diff_false_positive=dict(est=unsafe_diff, ci=(0, 0)),
                             diff_false_negative=dict(est=-0.2, ci=(-0.3, rej_hi))) for name in analysis.CANDIDATES}
            return dict(by=by)
        good = {k: table(0.0, -0.05) for k in ("M<=1.7", "M<=2.0", "M<=2.5", "M<=3.0")}
        verdict = analysis.adopt(good)["D"]
        self.assertTrue(verdict["demonstrated_on_synthetic_only"])
        self.assertFalse(verdict["adopted"])
        self.assertIn("iv", verdict["reason_not_adopted"])
        self.assertFalse(analysis.adopt(dict(good, **{"M<=2.0": table(0.0, -0.05, upper=0.07)}))["D"]["demonstrated_on_synthetic_only"])     # borne supérieure > 5 % : refusé
        self.assertFalse(analysis.adopt(dict(good, **{"M<=2.0": table(0.05, -0.05)}))["D"]["demonstrated_on_synthetic_only"])                # plus de faux positifs que A : refusé
        self.assertFalse(analysis.adopt(dict(good, **{"M<=2.0": table(0.0, 0.02)}))["D"]["demonstrated_on_synthetic_only"])                   # gain non démontré : refusé
        fragile = {k: table(0.0, 0.02) for k in ("M<=1.7", "M<=2.5", "M<=3.0")}
        fragile["M<=2.0"] = table(0.0, -0.05)
        self.assertFalse(analysis.adopt(fragile)["D"]["demonstrated_on_synthetic_only"])


if __name__ == "__main__":
    unittest.main()
