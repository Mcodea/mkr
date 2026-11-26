"""LOT 0b, P0-1 : couverture des erreurs-types publiées pour le FOPDT.

Oracle indépendant : processus de vérité connue (gain, constante de temps, retard exacts) simulé par une
expression analytique et un bruit gaussien tiré avec des graines fixes ; le moteur n'intervient que comme
estimateur. Critères fixés par la statistique, pas par le résultat observé :
  - rapport (erreur-type publiée moyenne) / (écart-type empirique des estimations) dans [0,8 ; 1,25]
    (l'écart-type empirique sur 80 tirages a une erreur relative d'environ 8 % : ±25 % ≈ 3 sigma) ;
  - couverture de la vérité par ±2 erreurs-types publiées >= 88 % (borne basse à 3 sigma de la loi
    binomiale pour 95,4 % attendu sur 80 tirages).
Contexte : l'incertitude de la ligne de base (moyenne de quelques échantillons bruités avant l'échelon) déplace
tout le plateau ; l'ignorer sous-estime l'erreur du gain (audit LOT 0, annexe A, P0-1).
"""
import math
import unittest

import numpy as np

import fopdt
from test_process_diversity import first_order, source

TRUTH = dict(gain=1.5, tauSeconds=20.0, delaySeconds=4.0)
DRAWS = range(300, 380)       # 80 graines fixes


def run(pre):
    estimates = {k: [] for k in TRUTH}
    errors = {k: [] for k in TRUTH}
    response = first_order(TRUTH["gain"], TRUTH["tauSeconds"], TRUTH["delaySeconds"])
    for seed in DRAWS:
        rng = np.random.default_rng(seed)
        payload = dict(training=source(1, response, 2.0, 1.0, 0.03, rng, pre=pre, post=120),
                       validation=source(2, response, -1.5, 1.0, 0.03, rng, pre=pre, post=120),
                       mvTolerance=0.01, pvTolerance=0.18)
        result = fopdt.identify(payload)
        if result["standardErrors"] is None:
            continue
        for key in TRUTH:
            estimates[key].append(result[key])
            errors[key].append(result["standardErrors"][key])
    return estimates, errors


class FopdtErrorCoverage(unittest.TestCase):
    def check(self, pre, keys):
        estimates, errors = run(pre)
        for key in keys:
            n = len(estimates[key])
            self.assertGreaterEqual(n, 70, f"{key}: trop de tirages sans erreur-type publiée ({n}/80)")
            empirical = float(np.std(estimates[key], ddof=1))
            ratio = float(np.mean(errors[key])) / empirical
            self.assertTrue(0.8 <= ratio <= 1.25, f"{key} (pre={pre}) : rapport erreur publiée / dispersion réelle = {ratio:.3f}")
            covered = np.mean([abs(e - TRUTH[key]) <= 2 * s for e, s in zip(estimates[key], errors[key])])
            self.assertGreaterEqual(float(covered), 0.88, f"{key} (pre={pre}) : couverture à ±2 erreurs-types = {covered:.3f}")

    def test_gain_and_time_constant_with_short_baseline(self):
        self.check(10, ("gain", "tauSeconds"))

    def test_gain_with_very_short_baseline(self):
        self.check(5, ("gain",))


if __name__ == "__main__":
    unittest.main()
