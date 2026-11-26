"""LOT 0b, P0-2 : l'équivalent « premier ordre » de l'ARX ne doit pas être publié quand le pôle est indiscernable de 1.

Un ARX(1,1) dont le pôle discret p tend vers 1 est un intégrateur ; l'équivalent premier ordre (tau = -Ts / ln p, K = b / (1 - p))
diverge (tau et K -> infini) et n'est pas identifiable : les données ne déterminent que K / tau (la pente).
Critère fixé par la théorie (pas par le résultat) : l'équivalent n'est publié que si le pôle est distinct de 1 au niveau de confiance
99 % déjà employé dans arx.py (p + 2,576 · erreur-type(a1) < 1).
Oracles indépendants : processus à vérité connue simulés par récurrence exacte (intégrateur cumulé ; premier ordre discrétisé exactement
par maintien d'ordre zéro), excitation pseudo-aléatoire (PRBS) et bruit gaussien à graines fixes ; l'estimateur n'est pas utilisé pour générer.
"""
import base64
import math
import unittest
from datetime import datetime, timedelta, timezone

import numpy as np

import arx


def make(day, u, y, ts):
    start = datetime(2026, 6, day, tzinfo=timezone.utc)
    lines = ["timestamp,mv,pv"] + [f"{(start + timedelta(seconds=i * ts)).isoformat()},{float(u[i])!r},{float(y[i])!r}" for i in range(len(u))]
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",", timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


def prbs(n, rng, hold=5):
    return np.repeat(rng.choice([-1.0, 1.0], size=n // hold + 1), hold)[:n]


def integrator_response(u, gain, delay, noise, rng):
    y = np.cumsum(np.concatenate(([0.0], gain * u[:-1])))
    y = np.roll(y, delay)
    y[:delay] = 0
    return y + noise * rng.standard_normal(len(u))


def first_order_response(u, tau, gain, delay, ts, noise, rng):
    p = math.exp(-ts / tau)
    y = np.zeros(len(u))
    for k in range(1, len(u)):
        y[k] = p * y[k - 1] + gain * (1 - p) * (u[k - 1 - delay] if k - 1 - delay >= 0 else 0)
    return y + noise * rng.standard_normal(len(u))


def payload(train, validate, limit=0.2):
    return dict(training=train, validation=validate, mvTolerance=1e-6, maximumRmse=limit, loop="open")


class ArxFirstOrderEquivalentGuard(unittest.TestCase):
    def test_integrator_never_published_as_first_order(self):
        for seed in range(1, 9):
            rng = np.random.default_rng(seed)
            u, v = prbs(800, rng), prbs(800, rng)
            result = arx.identify(payload(make(1, u, integrator_response(u, 0.05, 3, 0.02, rng), 2),
                                          make(2, v, integrator_response(v, 0.05, 3, 0.02, rng), 2)))
            selected = result["selected"]
            if selected is None:
                continue                                        # refusing is a correct outcome
            first = selected.get("firstOrder")
            self.assertIsNone(first, f"graine {seed} : un intégrateur est publié comme premier ordre {first}")

    def test_true_first_order_is_still_published_and_close_to_the_truth(self):
        published = 0
        for seed in range(31, 39):
            rng = np.random.default_rng(seed)
            u, v = prbs(800, rng), prbs(800, rng)
            result = arx.identify(payload(make(1, u, first_order_response(u, 20.0, 1.5, 2, 2, 0.02, rng), 2),
                                          make(2, v, first_order_response(v, 20.0, 1.5, 2, 2, 0.02, rng), 2)))
            selected = result["selected"]
            self.assertIsNotNone(selected, f"graine {seed} : un vrai premier ordre bien excité doit être accepté")
            first = selected.get("firstOrder")
            if first is None:
                continue
            published += 1
            self.assertAlmostEqual(first["gain"], 1.5, delta=max(5 * first["standardErrors"]["gain"], 0.1))
            self.assertAlmostEqual(first["tauSeconds"], 20.0, delta=max(5 * first["standardErrors"]["tauSeconds"], 2.0))
        self.assertGreaterEqual(published, 7, "le garde-fou ne doit pas supprimer l'équivalent d'un vrai premier ordre")


if __name__ == "__main__":
    unittest.main()
