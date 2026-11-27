"""Peak sensitivity of the SAMPLED-DATA loop: exact zero-order-hold plant, fractional delay, discrete PI/PID.

The continuous analysis of recommend.py represents the sampling by an extra delay h/2 (Skogestad 2003). That approximation is
optimistic when the sampling period is not small against the closed-loop dynamics (audit LOT 0, annex B; LOT 0b P0-3b). This module
evaluates the loop that is really executed:

  plant   G(s) = K exp(-theta s) / prod(tau_i s + 1), real poles, driven through a zero-order hold sampled every h seconds. With
          theta = m h + f (0 <= f < h) and the partial fractions of G(s)/s (poles s_0 = 0 and s_i = -1/tau_i, residues r_i), the pulse transfer
          function is exact:  G(z) = z^-m (1 - z^-1) sum_i r_i exp(-s_i f) exp(s_i h) z^-1 / (1 - exp(s_i h) z^-1)
          (the step response samples are y(kh) = sum_i r_i exp(s_i (kh - f)) for k >= 1: derived here, checked against the analytic step
          response in test_sampled_loop.py).
  controller  ideal PI or PID with a filtered derivative, in discrete form. The integral action is discretised by forward Euler
          (h / (z - 1)) OR backward Euler (h z / (z - 1)); the derivative by backward Euler. Which one a given vendor block uses is not
          known here, so callers judge the WORST of the two (conservative, never optimistic).
Ms = max over w in (0, pi / h] of 1 / |1 + C(e^{jwh}) G(e^{jwh})|.
Limits: real distinct poles (equal poles are separated by a relative 1e-6), stable plant, no actuator limits. The result is a frequency-domain
measure on the loop; the stability of the sampled loop is NOT proved here (the continuous margins and Ms bounded are necessary conditions).
"""
import math

import numpy as np

VERSION = "1.0.0"
FREQUENCY_POINTS = 6000
DISCRETISATIONS = ("forward_euler_integral", "backward_euler_integral")


def _poles_and_residues(gain, taus):
    taus = list(taus)
    for i in range(1, len(taus)):                      # equal time constants: separate them (relative 1e-6) so the partial fractions exist
        if abs(taus[i] - taus[i - 1]) <= 1e-9 * taus[i - 1]:
            taus[i] = taus[i - 1] * (1 + 1e-6)
    poles = [0.0] + [-1.0 / t for t in taus]
    lead = gain / math.prod(taus)
    residues = []
    for i, s_i in enumerate(poles):
        denominator = 1.0
        for j, s_j in enumerate(poles):
            if j != i:
                denominator *= (s_i - s_j)
        residues.append(lead / denominator)
    return poles, residues


def plant_pulse_response(gain, taus, delay, h, z):
    """Exact ZOH pulse transfer function G(z) of K exp(-delay s) / prod(tau_i s + 1) at the complex points z."""
    m = int(math.floor(delay / h + 1e-12))
    f = max(delay - m * h, 0.0)
    poles, residues = _poles_and_residues(gain, taus)
    zi = 1.0 / z
    total = np.zeros_like(z, dtype=complex)
    for s_i, r_i in zip(poles, residues):
        a = math.exp(s_i * h)
        total += r_i * math.exp(-s_i * f) * a * zi / (1.0 - a * zi)
    return (zi ** m) * (1.0 - zi) * total


def controller_response(kp, ti, td, tf, h, z, discretisation):
    integral_term = (h / ti) / (z - 1.0) if discretisation == "forward_euler_integral" else (h / ti) * z / (z - 1.0)
    response = kp * (1.0 + integral_term)
    if td > 0:
        filter_time = tf if tf > 0 else 0.0
        response = response + kp * td * (z - 1.0) / ((filter_time + h) * z - filter_time)
    return response


def peak_sensitivity(gain, taus, delay, h, kp, ti, td, tf, discretisation):
    w = np.linspace(math.pi / h / FREQUENCY_POINTS, math.pi / h, FREQUENCY_POINTS)
    z = np.exp(1j * w * h)
    loop = controller_response(kp, ti, td, tf, h, z, discretisation) * plant_pulse_response(gain, taus, delay, h, z)
    return float(np.max(1.0 / np.abs(1.0 + loop)))


def worst_peak_sensitivity(gain, taus, delay, h, controller):
    """Worst Ms over the two integral discretisations (the vendor block's own scheme is unknown)."""
    values = {d: peak_sensitivity(gain, taus, delay, h, controller["kp"], controller["tiSeconds"], controller["tdSeconds"],
                                  controller["filterSeconds"], d) for d in DISCRETISATIONS}
    worst = max(values, key=values.get)
    return values[worst], worst
