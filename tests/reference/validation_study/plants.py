"""Procédés VRAIS de l'étude de validation (protocole docs/science/lot4/protocole-validation-critere.md). DONNÉES SYNTHÉTIQUES.

Écrit pour l'étude, SANS importer engine/ : simulation temporelle exacte (discrétisation à maintien d'ordre zéro sur un pas fin) et réponse fréquentielle
analytique. Un procédé = K N(s) exp(-theta s) / D(s), avec N, D en coefficients décroissants."""
import math
from dataclasses import dataclass

import numpy as np
from scipy import signal


@dataclass(frozen=True)
class Plant:
    gain: float
    numerator: tuple          # coefficients de N(s), degrés décroissants (par défaut (1,))
    denominator: tuple        # coefficients de D(s), degrés décroissants ; D(0) = 1 pour que gain = gain statique
    delay: float
    kind: str = "linear"
    kappa: float = 0.0        # Hammerstein : entrée effective f(u) = u + kappa u^2 / scale (kind == "hammerstein")
    scale: float = 1.0

    def frequency_response(self, w):
        s = 1j * np.asarray(w)
        return self.gain * np.polyval(self.numerator, s) / np.polyval(self.denominator, s) * np.exp(-self.delay * s)

    def effective_input(self, u):
        if self.kind == "hammerstein":
            return u + self.kappa * u * u / self.scale
        return u

    def simulate(self, u, ts, fine=20):
        """Sortie (non bruitée, écart à l'équilibre) aux instants k*ts pour une entrée constante par morceau sur chaque période ts. Retard arrondi au pas fin."""
        h = ts / fine
        num = self.gain * np.array(self.numerator, dtype=float)
        sys = signal.cont2discrete(signal.tf2ss(num, np.array(self.denominator, dtype=float)), h, method="zoh")
        a, b, c, d = sys[0], sys[1], sys[2], sys[3]
        shift = int(round(self.delay / h))
        n = len(u)
        fine_input = np.repeat(self.effective_input(np.asarray(u, dtype=float)), fine)
        delayed = np.concatenate([np.zeros(shift), fine_input])[:len(fine_input)]
        x = np.zeros((a.shape[0], 1))
        out = np.empty(n)
        for k in range(n):
            out[k] = float((c @ x + d * delayed[k * fine]).item())
            for j in range(fine):
                x = a @ x + b * delayed[k * fine + j]
        return out


def fopdt(gain, tau, delay):
    return Plant(gain, (1.0,), (tau, 1.0), delay)


def sopdt(gain, tau1, tau2, delay):
    return Plant(gain, (1.0,), (tau1 * tau2, tau1 + tau2, 1.0), delay)


def lag_chain(gain, tau, n, delay):
    """n retards égaux de constante tau/n chacun (somme des constantes = tau)."""
    t = tau / n
    return Plant(gain, (1.0,), tuple(np.poly([-1.0 / t] * n) * t ** n), delay)


def inverse_response(gain, tau1, tau2, beta, delay):
    """K (1 - beta s) / ((tau1 s + 1)(tau2 s + 1)) exp(-delay s) : zéro à droite en 1/beta."""
    return Plant(gain, (-beta, 1.0), (tau1 * tau2, tau1 + tau2, 1.0), delay)


def hammerstein(gain, tau, delay, kappa, scale):
    return Plant(gain, (1.0,), (tau, 1.0), delay, kind="hammerstein", kappa=kappa, scale=scale)


class Stepper:
    """Simulation pas à pas d'un procédé (état conservé), pour les boucles fermées : output() puis advance(u) fait avancer d'une période ts."""

    def __init__(self, plant, ts, fine=20):
        h = ts / fine
        num = plant.gain * np.array(plant.numerator, dtype=float)
        sys = signal.cont2discrete(signal.tf2ss(num, np.array(plant.denominator, dtype=float)), h, method="zoh")
        self.a, self.b, self.c, self.d = sys[0], sys[1], sys[2], sys[3]
        self.plant, self.fine = plant, fine
        self.queue = [0.0] * int(round(plant.delay / h))
        self.x = np.zeros((self.a.shape[0], 1))
        self.last = 0.0

    def output(self):
        return float((self.c @ self.x + self.d * (self.queue[0] if self.queue else self.last)).item())

    def advance(self, u):
        effective = float(self.plant.effective_input(u))
        for _ in range(self.fine):
            self.queue.append(effective)
            delayed = self.queue.pop(0)
            self.x = self.a @ self.x + self.b * delayed
            self.last = delayed
