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


def nyquist_stable(plant, ts, kp, ti, td=0.0, tf=0.0, points=200000):
    """Stabilité de la boucle fermée par le critère de NYQUIST (indépendant de la simulation temporelle). Retourne (stable | None, zeros_estimés).

    F = 1 + L, L = C(jw) G(jw) exp(-jw ts/2). Procédé stable en boucle ouverte ; un seul pôle de L à l'origine (intégrateur du PI), contourné par une indentation à DROITE de l'origine.
    Contour de Nyquist parcouru dans le sens horaire : la variation totale de l'argument de F vaut 2 D1 - pi (D1 = variation sur l'axe imaginaire positif de eps à W ; -pi pour
    l'indentation, L ~ c/s) et le nombre de zéros de F dans le demi-plan droit est Z = 1/2 - D1/pi. Stable <=> Z = 0. Si Z n'est pas proche d'un entier (courbe trop près de -1 ou
    grille insuffisante), on renvoie None (indécidable) : jamais une réponse devinée."""
    w = np.logspace(-6, math.log10(max(2000.0 / max(plant.delay + ts / 2, 1e-3), 200.0)), points)
    loop = controller_response(w, kp, ti, td, tf) * plant.frequency_response(w) * np.exp(-1j * w * ts / 2)
    if not np.all(np.isfinite(loop)) or abs(loop[-1]) > 0.05:
        return None, float("nan")
    phase = np.unwrap(np.angle(1 + loop))
    zeros = 0.5 - (phase[-1] - phase[0]) / math.pi
    nearest = round(zeros)
    if abs(zeros - nearest) > 0.25 or nearest < 0:
        return None, float(zeros)
    return bool(nearest == 0), float(zeros)
