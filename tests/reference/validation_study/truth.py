"""Vérité de l'étude : comportement d'un réglage (kp, Ti, Td, Tf) sur le procédé VRAI. Indépendant de engine/ (protocole §2).

  * stabilité et ITAE d'un échelon de consigne : boucle fermée temporelle échantillonnée (période ts, maintien d'ordre zéro) sur le procédé vrai ;
  * Ms vrai : balayage fréquentiel de |1 / (1 + L)| avec L = C(jw) G(jw) exp(-jw ts/2) (le maintien d'ordre zéro ajoute en moyenne ts/2 de retard)."""
import math

import numpy as np

from plants import Stepper


def controller_response(w, kp, ti, td=0.0, tf=0.0):
    s = 1j * np.asarray(w)
    c = kp * (1 + 1 / (ti * s))
    if td > 0:
        c = c + kp * td * s / (1 + tf * s) if tf > 0 else c + kp * td * s
    return c


def true_ms(plant, ts, kp, ti, td=0.0, tf=0.0):
    w = np.logspace(-4, math.log10(math.pi / ts), 40000)
    loop = controller_response(w, kp, ti, td, tf) * plant.frequency_response(w) * np.exp(-1j * w * ts / 2)
    return float(np.max(np.abs(1 / (1 + loop))))


def closed_loop(plant, ts, kp, ti, td=0.0, tf=0.0, horizon=None, setpoint=1.0):
    """Échelon de consigne sur le procédé vrai. Retourne (stable, itae, y)."""
    horizon = horizon or 60 * (abs(plant.denominator[0] / max(plant.denominator[-1], 1e-12)) ** (1 / max(len(plant.denominator) - 1, 1)) + plant.delay)
    n = int(max(200, horizon / ts))
    stepper = Stepper(plant, ts)
    integral = previous_error = derivative = 0.0
    itae = 0.0
    y = np.empty(n)
    for k in range(n):
        measurement = stepper.output()
        y[k] = measurement
        error = setpoint - measurement
        if not math.isfinite(error) or abs(error) > 1e6:
            return False, math.inf, y[:k + 1]
        integral += kp * ts / ti * error
        if td > 0:
            derivative = (tf / (tf + ts)) * derivative + (kp * td / (tf + ts)) * (error - previous_error) if tf > 0 else kp * td * (error - previous_error) / ts
        u = kp * error + integral + derivative
        previous_error = error
        itae += (k * ts) * abs(error) * ts
        stepper.advance(u)
    tail = y[int(0.8 * n):]
    stable = bool(np.all(np.isfinite(y)) and abs(tail.mean() - setpoint) < 0.1 * abs(setpoint) and np.max(np.abs(tail)) < 5 * abs(setpoint))
    return stable, (itae if stable else math.inf), y
