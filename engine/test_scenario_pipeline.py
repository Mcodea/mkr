"""Parcours complet du moteur sur le scénario SYNTHÉTIQUE pression / pompe / variateur (cahier directeur, LOT 9 : tranche moteur).

DONNÉES SYNTHÉTIQUES : jamais présentées comme des mesures. Le générateur (tests/reference/scenario_pression_pompe/generate.py) n'importe rien de engine/ :
ses paramètres vrais (ajustement FOPDT sans bruit du procédé non linéaire d'ordre 4) et sa boucle fermée simulée sont des oracles indépendants.

Chaîne exercée À TRAVERS LE CONTRAT IPC (respond) : suggestion de critères -> identification FOPDT -> porte de validation -> comparaison de modèles ->
réglage -> conversion vers les blocs Schneider -> simulation PI -> comparaison à la boucle fermée du procédé vrai.

Critères d'acceptation fixés AVANT les assertions (ordres de grandeur : bruit 0,003 bar, échelon de consigne 0,3 bar, échelon MV 5 %) :
  * paramètres identifiés : gain 2 %, tau 3 %, retard 5 % des valeurs vraies ;
  * réglage : kp et Ti égaux à la formule SIMC recalculée à la main ; Ms mesuré par un balayage fréquentiel INDÉPENDANT sur le procédé vrai <= 1,7 ;
  * boucle fermée prédite contre observée (60 à 120 s, avant la perturbation de vanne) : écart quadratique <= 10 % de l'échelon de consigne (0,03 bar).
Constat F-LOT9-1 documenté plus bas : avec les critères PAR DÉFAUT de la porte, aucun modèle n'est validé sur ce scénario ; la suite de la chaîne n'a lieu
qu'avec un critère déclaré par l'humain, jamais le défaut."""
import base64
import csv
import json
import math
import unittest
from datetime import datetime
from pathlib import Path

import numpy as np

from mkr_engine import respond

DATA = Path(__file__).resolve().parent.parent / "tests" / "reference" / "scenario_pression_pompe" / "data"
TRUE = json.loads((DATA / "ol_nominal.manifest.json").read_text(encoding="utf-8"))["trueParameters"]
CLOSED_LOOP = json.loads((DATA / "cl_pi.manifest.json").read_text(encoding="utf-8"))["controller"]
HUMAN_CRITERIA = dict(maximumRmse=0.006, maximumResidualLagOne=0.5)       # déclaré par l'humain (jamais le défaut) : voir F-LOT9-1


def source(name):
    return dict(raw=base64.b64encode((DATA / f"{name}.csv").read_bytes()).decode(), delimiter=",", timeColumn="timestamp", mvColumn="MV", pvColumn="PV")


def call(operation, payload):
    reply = respond(json.dumps(dict(version=1, requestId="pipeline", operation=operation, payload=payload)).encode())
    if "error" in reply:
        raise AssertionError(f"{operation} : {reply['error']}")
    return reply["result"]


TRIALS = dict(training=source("ol_nominal"), validation=source("ol_validation"), mvTolerance=0.01, pvTolerance=0.01)


class ScenarioPipeline(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.criteria = call("suggest_criteria", TRIALS)
        cls.accepted = call("assess_model", dict(family="FOPDT", trials=TRIALS, **HUMAN_CRITERIA))
        cls.model = cls.accepted["report"]

    def test_identification_recovers_the_true_parameters(self):
        m = self.model
        self.assertLessEqual(abs(m["gain"] / TRUE["gain_bar_per_pct"] - 1), 0.02)
        self.assertLessEqual(abs(m["tauSeconds"] / TRUE["tau_s"] - 1), 0.03)
        self.assertLessEqual(abs(m["delaySeconds"] / TRUE["delay_s"] - 1), 0.05)

    def test_F_LOT9_1_default_criteria_validate_neither_model_on_the_nominal_scenario(self):
        """CONSTAT (pas une approbation) : l'erreur est proche du bruit (0,0044 bar pour 0,003) mais les résidus restent autocorrélés parce que le procédé
        vrai est d'ordre 4 et non linéaire ; la borne de blancheur (Ljung, 99 %) est dépassée par le FOPDT (0,42) et le SOPDT (0,18) pour une borne de 0,105.
        Le manifeste du scénario annonce `intent: accepted` : cet écart entre l'intention du scénario et le verdict de la porte est à arbitrer au LOT 4."""
        default = dict(maximumRmse=self.criteria["maximumRmse"], maximumResidualLagOne=self.criteria["maximumResidualLagOne"])
        verdict = call("assess_model", dict(family="FOPDT", trials=TRIALS, **default))["assessment"]
        self.assertFalse(verdict["accepted"])
        self.assertEqual(verdict["reasons"], ["validation_residuals"])
        comparison = call("compare_models", dict(trials=TRIALS, **default))
        self.assertIsNone(comparison["selected"])
        for family in ("FOPDT", "SOPDT"):
            self.assertEqual(comparison["candidates"][family]["rejectionReasons"], ["validation_residuals"])

    def test_the_human_declared_criterion_accepts_the_model_and_the_engine_still_refuses_without_proof(self):
        self.assertTrue(self.accepted["assessment"]["accepted"])
        m = self.model
        request = dict(family="FOPDT", gain=m["gain"], tauSeconds=m["tauSeconds"], delaySeconds=m["delayIntervalSeconds"][1], secondTauSeconds=None, sampleSeconds=0.1,
                       standardErrors=m["standardErrors"], objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False)
        refused = respond(json.dumps(dict(version=1, requestId="r", operation="recommend_tuning", payload=request)).encode())
        self.assertEqual(refused["error"]["code"], "recommend_model_not_validated")

    def tuning(self):
        m = self.model
        return call("recommend_tuning", dict(
            family="FOPDT", gain=m["gain"], tauSeconds=m["tauSeconds"], delaySeconds=m["delayIntervalSeconds"][1], secondTauSeconds=None, sampleSeconds=0.1,
            standardErrors=m["standardErrors"], objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False,
            modelValidation=dict(accepted=True, drift=False)))

    def test_tuning_matches_the_hand_computed_simc_rule_and_is_robust_on_the_true_process(self):
        m, plan = self.model, self.tuning()
        controller = plan["recommendation"]["controller"]
        self.assertEqual((plan["decision"], controller["structure"]), ("recommended", "PI"))
        # SIMC recalculé à la main : kp = tau / (K (tau_c + theta_eff)), Ti = min(tau, 4 (tau_c + theta_eff)), theta_eff = retard conservateur + Ts/2
        tau_c, theta_eff = plan["recommendation"]["responseSeconds"], m["delayIntervalSeconds"][1] + 0.05
        self.assertAlmostEqual(controller["kp"], m["tauSeconds"] / (m["gain"] * (tau_c + theta_eff)), delta=1e-3 * controller["kp"])
        self.assertAlmostEqual(controller["tiSeconds"], min(m["tauSeconds"], 4 * (tau_c + theta_eff)), delta=1e-6)
        # Ms mesuré par un balayage fréquentiel indépendant sur le procédé VRAI (retard vrai + demi-période de maintien)
        w = np.logspace(-3, 2, 20000)
        s = 1j * w
        loop = controller["kp"] * (1 + 1 / (controller["tiSeconds"] * s)) * TRUE["gain_bar_per_pct"] * np.exp(-(TRUE["delay_s"] + 0.05) * s) / (TRUE["tau_s"] * s + 1)
        self.assertLessEqual(float(np.max(np.abs(1 / (1 + loop)))), 1.7)

    def test_simulated_closed_loop_predicts_the_closed_loop_of_the_true_process(self):
        m = self.model
        rows = list(csv.DictReader((DATA / "cl_pi.csv").read_text(encoding="utf-8").splitlines()))
        origin = datetime.fromisoformat(rows[0]["timestamp"])
        t = np.array([(datetime.fromisoformat(r["timestamp"]) - origin).total_seconds() for r in rows])
        pv = np.array([float(r["PV"]) for r in rows])
        settings = dict(kp=CLOSED_LOOP["Kc_pct_per_bar"], tiSeconds=CLOSED_LOOP["Ti_s"])      # le régulateur RÉELLEMENT appliqué dans le jeu
        simulation = call("simulate_pi", dict(gain=m["gain"], tauSeconds=m["tauSeconds"], delaySeconds=m["delayIntervalSeconds"][0], imc=settings, simc=settings,
                                              sampleSeconds=0.1, durationSeconds=60.0, setpoint=0.3, minimumMv=-55.0, maximumMv=15.0,
                                              disturbance=0.0, disturbanceAtSeconds=0.0))
        points = simulation["simc"]["points"]
        window = (t >= 60.0) & (t <= 120.0)                                                   # échelon de consigne 4,0 -> 4,3 à 60 s ; vanne perturbée à 120 s
        predicted = np.interp(t[window] - 60.0, [p["seconds"] for p in points], [p["pv"] for p in points])
        rmse = math.sqrt(float(np.mean((pv[window] - 4.0 - predicted) ** 2)))
        self.assertLessEqual(rmse, 0.03)

    def test_block_conversions_stay_documentary_and_publish_their_limits(self):
        controller = self.tuning()["recommendation"]["controller"]
        parameters = dict(kp=controller["kp"], tiSeconds=controller["tiSeconds"], tdSeconds=0.0)
        pidff = call("convert_block_settings", dict(name="PIDFF", target={}, parameters=parameters))
        self.assertTrue(pidff["converted"])
        self.assertEqual((pidff["conversion"]["qualification"], pidff["conversion"]["readyToLoad"]), ("non_qualified", False))
        pid_int = call("convert_block_settings", dict(name="PID_INT", target={}, parameters=dict(parameters, tsSeconds=0.1, pvSpan=10.0, mvSpan=100.0)))
        self.assertTrue(pid_int["converted"])
        registers = pid_int["conversion"]["parameters"]
        self.assertEqual(registers["KP"], round(controller["kp"] * 10.0 / 100.0 * 100))        # KP = kp x pvSpan / mvSpan, x100
        self.assertEqual(registers["TI"], round(controller["tiSeconds"] * 10))                 # dixièmes de seconde
        self.assertIn("integer_dead_zone", pid_int["conversion"]["warnings"])
        # zone morte de la troncature entière en bar : 1000 TI / (KP TS) comptes sur une échelle de 10 bar / 10000 comptes
        dead_zone_bar = pid_int["conversion"]["parameters"]["integerDeadZoneCounts"] * 10.0 / 10000.0
        self.assertGreater(dead_zone_bar, 0.003)           # supérieure au bruit de mesure (0,003 bar) : à AFFICHER à l'utilisateur


if __name__ == "__main__":
    unittest.main()
