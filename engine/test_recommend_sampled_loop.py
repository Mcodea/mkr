"""LOT 0b, P0-3b/c : le pic de sensibilité de la boucle ÉCHANTILLONNÉE respecte l'objectif annoncé, quel que soit le schéma d'intégration du bloc.

Oracle indépendant : procédé discrétisé par scipy.signal.cont2discrete (maintien d'ordre zéro), retard = multiple entier de la période (z^-m),
régulateur PI en Euler avant ET en Euler arrière (les deux schémas rencontrés dans les blocs) ; Ms = max |1 / (1 + C G)| sur (0, pi/h].
Critère fixé a priori : Ms échantillonné <= limite de l'objectif (tolérance numérique 1e-3), pour les deux schémas. Un refus est un résultat correct.
Avant correction (LOT 0, annexe B) : theta = 0, h = 1 à 3 s, objectif « équilibré » (limite 1,7) donnait 1,76 à 1,81 en Euler avant, 2,4 en Euler arrière.
"""
import math
import unittest

import numpy as np
from scipy.signal import cont2discrete

from recommend import OBJECTIVES, calculate

CASES = [(1.0, 10.0, 0.0, 1.0), (1.0, 10.0, 0.0, 2.0), (1.0, 10.0, 0.0, 3.0), (1.0, 10.0, 2.0, 1.0), (1.0, 10.0, 4.0, 2.0), (2.0, 25.0, 3.0, 3.0)]   # gain, tau, delay, h


def independent_sampled_ms(gain, tau, delay, h, kp, ti, scheme):
    num, den, _ = cont2discrete(([gain], [tau, 1.0]), h, method="zoh")
    num, den = np.atleast_1d(num.squeeze()), np.atleast_1d(den.squeeze())
    m = int(round(delay / h))
    w = np.linspace(1e-4, math.pi / h, 60000)
    z = np.exp(1j * w * h)
    plant = np.polyval(num, z) / np.polyval(den, z) * z ** (-m)
    integral = (h / ti) / (z - 1) if scheme == "forward" else (h / ti) * z / (z - 1)
    return float(np.max(1 / np.abs(1 + kp * (1 + integral) * plant)))


class SampledLoopObjective(unittest.TestCase):
    def test_objective_holds_on_the_sampled_loop_for_both_euler_schemes(self):
        for objective, limit in OBJECTIVES.items():
            for gain, tau, delay, h in CASES:
                with self.subTest(objective=objective, case=(gain, tau, delay, h)):
                    result = calculate(dict(family="FOPDT", gain=gain, tauSeconds=tau, delaySeconds=delay, secondTauSeconds=None, sampleSeconds=h,
                                            standardErrors=None, objective=objective, derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
                    if result.get("recommendation") is None:
                        continue
                    controller = result["recommendation"]["controller"]
                    for scheme in ("forward", "backward"):
                        ms = independent_sampled_ms(gain, tau, delay, h, controller["kp"], controller["tiSeconds"], scheme)
                        self.assertLessEqual(ms, limit + 1e-3, f"Ms échantillonné ({scheme} Euler) = {ms:.4f} > {limit}")

    def test_controller_period_distinct_from_data_cadence_is_used_and_reported(self):
        base = dict(family="FOPDT", gain=1.0, tauSeconds=10.0, delaySeconds=2.0, secondTauSeconds=None, sampleSeconds=0.1, standardErrors=None,
                    objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False)
        fast = calculate(dict(base))
        slow = calculate(dict(base, controllerPeriodSeconds=2.0))
        self.assertIn("controller_period_assumed_equal_to_data_cadence", fast["warnings"])
        self.assertNotIn("controller_period_assumed_equal_to_data_cadence", slow["warnings"])
        self.assertEqual(slow["samplePeriodSeconds"], 2.0)
        if slow.get("recommendation") is not None and fast.get("recommendation") is not None:
            self.assertGreaterEqual(slow["recommendation"]["responseSeconds"], fast["recommendation"]["responseSeconds"] - 1e-9)

    def test_invalid_controller_period_is_refused(self):
        base = dict(family="FOPDT", gain=1.0, tauSeconds=10.0, delaySeconds=2.0, secondTauSeconds=None, sampleSeconds=0.1, standardErrors=None,
                    objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False)
        for bad in (0, -1.0, float("nan"), "1"):
            with self.assertRaises(ValueError):
                calculate(dict(base, controllerPeriodSeconds=bad))


if __name__ == "__main__":
    unittest.main()
