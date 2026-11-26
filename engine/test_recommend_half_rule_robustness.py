"""LOT 0b, P0-3a : la robustesse annoncée doit être celle de la boucle réelle (SOPDT identifié), pas du modèle réduit.

Pour un SOPDT avec tau2 <= theta, la règle de la moitié (Skogestad 2003) sert à CALCULER le régulateur PI. La promesse de l'objectif
(pic de sensibilité Ms <= limite) doit en revanche être tenue sur le procédé identifié : sinon un Ms annoncé en dessous de la limite peut
être dépassé par le procédé vrai (audit LOT 0, annexe B).
Oracle indépendant écrit ici : balayage fréquentiel de |1 / (1 + L(jw))| avec L = C(jw) K e^{-(theta + h/2) s} / ((tau1 s + 1)(tau2 s + 1)),
C = kp (1 + 1/(Ti s)) ; aucun module de marges du moteur n'est utilisé.
Critères fixés a priori : (1) Ms vrai <= limite de l'objectif (tolérance numérique 1e-3), (2) Ms annoncé >= Ms vrai - 1e-3 (jamais optimiste).
"""
import unittest

import numpy as np

from recommend import OBJECTIVES, calculate

CASES = [(1.0, 10.0, 4.0, 5.0), (1.0, 10.0, 5.0, 5.0), (1.0, 20.0, 8.0, 10.0), (1.0, 5.0, 4.9, 5.0), (2.0, 30.0, 12.0, 15.0), (0.5, 8.0, 6.0, 7.0)]   # gain, tau, tau2, delay


def true_ms(gain, tau1, tau2, delay, sample, kp, ti):
    w = np.logspace(-4, 2, 400000) / max(tau2, 1e-3)
    s = 1j * w
    plant = gain * np.exp(-s * (delay + sample / 2)) / ((tau1 * s + 1) * (tau2 * s + 1))
    controller = kp * (1 + 1 / (ti * s))
    return float(np.max(1 / np.abs(1 + controller * plant)))


class HalfRuleRobustness(unittest.TestCase):
    def test_objective_is_met_on_the_identified_second_order_process(self):
        for objective, limit in OBJECTIVES.items():
            for gain, tau, second, delay in CASES:
                with self.subTest(objective=objective, case=(gain, tau, second, delay)):
                    result = calculate(dict(family="SOPDT", gain=gain, tauSeconds=tau, delaySeconds=delay, secondTauSeconds=second, sampleSeconds=0.2,
                                            standardErrors=None, objective=objective, derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
                    if result.get("recommendation") is None:
                        continue                                   # a refusal is a correct outcome; an optimistic number is not
                    recommended = result["recommendation"]
                    controller = recommended["controller"]
                    self.assertEqual(controller["structure"], "PI")
                    ms = true_ms(gain, tau, second, delay, 0.2, controller["kp"], controller["tiSeconds"])
                    self.assertLessEqual(ms, limit + 1e-3, f"Ms réel {ms:.4f} > limite {limit}")
                    self.assertGreaterEqual(recommended["worstPeakSensitivity"], ms - 1e-3, f"annoncé {recommended['worstPeakSensitivity']:.4f} < réel {ms:.4f}")


if __name__ == "__main__":
    unittest.main()
