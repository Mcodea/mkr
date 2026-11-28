"""Reproductibilité de l'étude de validation (protocole docs/science/lot4/protocole-validation-critere.md, règle d'adoption §7 v) et tests unitaires de la logique d'analyse.

Test de REPRODUCTIBILITÉ (pas un oracle) : cinq cas de l'étude, régénérés par leur graine, redonnent les mêmes valeurs qu'à la rédaction (tolérance relative 1e-6, pour
absorber les écarts de bibliothèque entre plateformes). Les valeurs attendues sont des sorties du code, figées ici pour détecter toute dérive involontaire de l'étude ;
elles ne prouvent pas que ces valeurs sont justes (l'exactitude des outils est vérifiée par test_validation_study_tools : formes closes et accord avec stability_margins).
Les tests de l'analyse utilisent des lignes écrites à la main dont le résultat est connu par construction."""
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

    def test_an_unstable_loop_is_detected_in_time_domain_even_when_ms_is_finite(self):
        plant = plants.fopdt(2.0, 10.0, 3.0)
        stable, _, _ = truth.closed_loop(plant, 0.2, 1.0, 10.0)
        unstable, itae, _ = truth.closed_loop(plant, 0.2, 3.5, 10.0)
        self.assertTrue(stable)
        self.assertFalse(unstable)
        self.assertTrue(math.isinf(itae))


def row(ms, stable=True, accepted=True, nse=0.0, lb_p=0.5, cross=0.0, bound=0.1, reasons=(), e_ms=1.5, c7_ms=1.5, population="Px", index=1001):
    return dict(info=dict(population=population), snr=100, index=index,
                gate=dict(accepted=accepted, reasons=list(reasons), rmse=0.01, lag1=0.0, max_rmse=0.02, max_lag1=0.1),
                stats=dict(nse=nse, ljung_box_p=lb_p, cross_max=cross, cross_bound=bound),
                truth=dict(stable=stable, ms=ms, itae=1.0), reference=dict(stable=True, ms=1.6, itae=1.0),
                E=dict(worst_ms=e_ms), C7=dict(worst_ms=c7_ms))


THRESHOLDS = dict(D=0.01, E=1.8, C7=1.8)


class AnalysisLogic(unittest.TestCase):
    def test_labels_depend_on_the_declared_definition_and_on_stability(self):
        self.assertTrue(analysis.label(row(1.9), 2.0))
        self.assertFalse(analysis.label(row(2.2), 2.0))
        self.assertTrue(analysis.label(row(2.2), 2.5))
        self.assertFalse(analysis.label(row(1.5, stable=False), 3.0))          # un réglage instable n'est jamais « apte »
        self.assertIsNone(analysis.label(dict(info={}, snr=1, index=1), 2.0))     # pas de vérité : exclu, pas compté

    def test_rates_on_a_hand_built_population(self):
        accept = np.array([True, True, False, False, True])
        fit = np.array([True, True, True, False, False])
        unsafe, false_rejection = analysis.rates(accept, fit)
        self.assertAlmostEqual(unsafe, 0.5)             # 1 accepté sur 2 inaptes
        self.assertAlmostEqual(false_rejection, 1 / 3)  # 1 refusé sur 3 aptes

    def test_decisions_follow_the_declared_rules(self):
        d = analysis.decisions(row(1.5, accepted=False, nse=0.001, reasons=["validation_residuals"]), THRESHOLDS)
        self.assertFalse(d["A"])
        self.assertTrue(d["A1"])                         # erreur quadratique correcte
        self.assertFalse(d["A2"])
        self.assertTrue(d["D"])                          # erreur de structure relative sous le seuil calibré
        self.assertTrue(d["E"] and d["C7"])              # pire Ms de l'ensemble / des coins <= seuil calibré
        self.assertTrue(d["A_or_D"] and d["A_or_E"] and d["A_or_C7"])
        self.assertFalse(d["A_and_E"])                   # A refuse : l'intersection refuse
        blocked = analysis.decisions(row(1.5, accepted=False, reasons=["parameter_bound"]), dict(D=1.0, E=9.0, C7=9.0))
        self.assertFalse(blocked["A1"] or blocked["D"] or blocked["E"] or blocked["C7"])   # un avertissement d'estimation bloquant refuse tous les candidats
        self.assertFalse(analysis.decisions(row(1.5), dict(D=None, E=None, C7=None))["D"])    # non calibré : jamais accepté
        worse = analysis.decisions(row(1.5, e_ms=2.5, c7_ms=2.5), THRESHOLDS)
        self.assertFalse(worse["E"] or worse["C7"])      # robustesse insuffisante sur l'ensemble : refusé même si l'erreur quadratique est bonne

    def test_calibration_gives_the_stated_unsafe_acceptance_for_each_alpha_and_refuses_when_data_is_thin(self):
        rows = [row(3.0, nse=0.01 * (i + 1), e_ms=1.0 + 0.01 * i) for i in range(100)]      # 100 réglages inaptes ; NSE croissante 0,01 ... 1,00
        s05, n = analysis.calibrate(rows, "D", 0.05)
        s01, _ = analysis.calibrate(rows, "D", 0.01)
        self.assertEqual(n, 100)
        self.assertAlmostEqual(s05, 0.05)                 # au plus 5 inaptes sur 100 acceptés
        self.assertEqual(sum(r["stats"]["nse"] <= s05 for r in rows), 5)
        self.assertEqual(sum(r["stats"]["nse"] <= s01 for r in rows), 1)       # 1 % : au plus 1 sur 100
        self.assertLess(s01, s05)                          # un risque plus bas donne un seuil plus strict
        self.assertEqual(analysis.calibrate(rows[:10], "D", 0.05)[0], None)    # trop peu d'inaptes : on ne calibre pas
        self.assertIsNotNone(analysis.calibrate(rows, "E", 0.05)[0])

    def test_unstable_tuning_is_never_counted_as_fit_and_is_reported_among_accepted(self):
        rows = [row(1.5, stable=True, population="P2") for _ in range(30)] + [row(1.5, stable=False, population="P6") for _ in range(10)]
        ev = analysis.evaluate(rows, THRESHOLDS, 2.0)
        self.assertEqual((ev["n_fit"], ev["n_unfit"], ev["n_unstable"]), (30, 10, 10))
        self.assertEqual(ev["stability"]["A"], dict(accepted=40, accepted_unstable=10))      # A accepte tout : 10 réglages instables acceptés, signalés
        self.assertAlmostEqual(ev["by"]["A"]["unsafe_acceptance"], 1.0)

    def test_imperfect_model_with_a_fit_tuning_is_reported_apart_from_an_unfit_tuning(self):
        rows = ([row(1.6, population="P2", accepted=True) for _ in range(20)] +            # modèle imparfait, réglage apte, accepté : bénin
                [row(2.8, population="P6", accepted=True) for _ in range(10)] +            # modèle imparfait, réglage inapte, accepté : GRAVE
                [row(1.6, population="P1", accepted=True) for _ in range(20)])
        cross = analysis.evaluate(rows, THRESHOLDS, 2.0)["cross"]["A"]
        self.assertEqual((cross["imparfait_reglage_apte_accepte"], cross["imparfait_reglage_apte_total"]), (20, 20))
        self.assertEqual((cross["imparfait_reglage_inapte_accepte"], cross["imparfait_reglage_inapte_total"]), (10, 10))
        self.assertEqual(cross["exact_reglage_inapte_total"], 0)

    def test_adoption_never_happens_without_real_data_even_when_the_synthetic_evidence_is_strong(self):
        def table(unsafe_diff, rej_hi, unsafe=0.02):
            by = {name: dict(unsafe_acceptance=unsafe, false_rejection=0.5, diff_unsafe=dict(est=unsafe_diff, ci=(0, 0)),
                             diff_false_rejection=dict(est=-0.2, ci=(-0.3, rej_hi))) for name in analysis.CANDIDATES}
            return dict(by=by)
        good = {k: table(0.0, -0.05) for k in ("M<=1.7", "M<=2.0", "M<=2.5", "M<=3.0")}
        verdict = analysis.adopt(good)["D"]
        self.assertTrue(verdict["demonstrated_on_synthetic_only"])
        self.assertFalse(verdict["adopted"])               # la règle iv (données réelles) n'est pas satisfaite : la porte actuelle est conservée
        self.assertIn("iv", verdict["reason_not_adopted"])
        unsafe = dict(good, **{"M<=2.0": table(0.05, -0.05, unsafe=0.07)})
        self.assertFalse(analysis.adopt(unsafe)["D"]["demonstrated_on_synthetic_only"])       # plus d'acceptations de réglages inaptes : refusé
        no_gain = dict(good, **{"M<=2.0": table(0.0, 0.02)})
        self.assertFalse(analysis.adopt(no_gain)["D"]["demonstrated_on_synthetic_only"])      # IC du gain n'exclut pas 0 : refusé
        fragile = {k: table(0.0, 0.02) for k in ("M<=1.7", "M<=2.5", "M<=3.0")}
        fragile["M<=2.0"] = table(0.0, -0.05)
        self.assertFalse(analysis.adopt(fragile)["D"]["demonstrated_on_synthetic_only"])      # ne tient que sous la définition primaire : refusé


if __name__ == "__main__":
    unittest.main()
