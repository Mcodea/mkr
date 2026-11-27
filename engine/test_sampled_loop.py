"""LOT 0b, P0-3b : la fonction de transfert impulsionnelle exacte (ZOH + retard fractionnaire) est comparée à une référence indépendante.

Oracle indépendant : réponse indicielle ANALYTIQUE (premier et second ordre avec retard) échantillonnée ; la réponse impulsionnelle du ZOH vaut
g[k] = y[k] - y[k-1] ; G(e^{jw}) = sum_k g[k] e^{-jwk} (série tronquée, procédé stable). Aucun calcul par fractions partielles dans l'oracle.
Tolérance : 1e-6 en valeur absolue (troncature de la série contrôlée par un horizon de 60 constantes de temps).
"""
import math
import unittest

import numpy as np

from sampled_loop import peak_sensitivity, plant_pulse_response, worst_peak_sensitivity


def analytic_step(gain, taus, delay, t):
    t = np.asarray(t) - delay
    out = np.zeros_like(t, dtype=float)
    positive = t > 0
    tt = t[positive]
    if len(taus) == 1:
        out[positive] = gain * (1 - np.exp(-tt / taus[0]))
    else:
        t1, t2 = taus
        out[positive] = gain * (1 - (t1 * np.exp(-tt / t1) - t2 * np.exp(-tt / t2)) / (t1 - t2))
    return out


def reference_response(gain, taus, delay, h, w):
    horizon = int(60 * max(taus) / h) + int(delay / h) + 10
    step = analytic_step(gain, taus, delay, np.arange(horizon) * h)
    impulse = np.diff(np.concatenate(([0.0], step)))
    k = np.arange(horizon)
    return np.array([np.sum(impulse * np.exp(-1j * wi * k)) for wi in w])


class PulseTransferFunction(unittest.TestCase):
    def test_matches_analytic_step_response_with_fractional_delay(self):
        cases = [(1.0, (10.0,), 0.0, 1.0), (2.0, (10.0,), 3.3, 1.0), (1.5, (20.0,), 4.0, 2.0), (1.0, (10.0, 4.0), 5.0, 0.5),
                 (0.5, (8.0, 6.0), 7.3, 1.5), (1.0, (10.0,), 0.4, 1.0)]
        for gain, taus, delay, h in cases:
            with self.subTest(case=(gain, taus, delay, h)):
                w = np.array([0.01, 0.05, 0.1, 0.3, 0.6, 0.9, 0.99 * math.pi]) / h
                exact = plant_pulse_response(gain, taus, delay, h, np.exp(1j * w * h))
                reference = reference_response(gain, taus, delay, h, w * h)
                self.assertLess(float(np.max(np.abs(exact - reference))), 1e-6)

    def test_zero_frequency_gain_is_the_plant_gain(self):
        for gain, taus, delay in [(1.0, (10.0,), 2.7), (3.0, (10.0, 4.0), 1.1)]:
            value = plant_pulse_response(gain, taus, delay, 0.5, np.array([np.exp(1j * 1e-9)]))
            self.assertAlmostEqual(float(abs(value[0])), gain, places=5)

    def test_peak_sensitivity_is_finite_and_ordered_by_discretisation(self):
        controller = dict(kp=2.0, tiSeconds=10.0, tdSeconds=0.0, filterSeconds=0.0)
        worst, name = worst_peak_sensitivity(1.0, (10.0,), 0.0, 2.0, controller)
        forward = peak_sensitivity(1.0, (10.0,), 0.0, 2.0, 2.0, 10.0, 0.0, 0.0, "forward_euler_integral")
        backward = peak_sensitivity(1.0, (10.0,), 0.0, 2.0, 2.0, 10.0, 0.0, 0.0, "backward_euler_integral")
        self.assertEqual(worst, max(forward, backward))
        self.assertTrue(math.isfinite(worst) and worst > 1.0)


if __name__ == "__main__":
    unittest.main()
