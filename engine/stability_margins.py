"""Frequency-domain margins of the nominal loop L(s) = C(s) G(s), error → PV.

C is the ideal PID Kp (1 + 1/(Ti s) + Td s / (Tf s + 1)) (Tf = 0: unfiltered derivative; Ti = inf: no integral); G is a stable
FOPDT or SOPDT process K exp(-theta s) / prod(tau_i s + 1). The sign of Kp equals the sign of K, so
only |Kp K| matters for L.

Margins describe the NOMINAL model only. They guarantee nothing for the real process, whose model error
is exactly what they are meant to bound: read them with the sensitivity analysis, not instead of it.
"""
import math

import numpy as np
from scipy.optimize import brentq, minimize_scalar

VERSION = "1.0.0"
# Practical guidance (Åström & Murray, Feedback Systems, ch. 12 ; Skogestad & Postlethwaite, Multivariable Feedback Control, ch. 2): Ms ≲ 2,
# phase margin ≳ 30–60°, gain margin ≳ 2. Warnings, never acceptance criteria.
LOW_PHASE_MARGIN_DEGREES = 30.0
LOW_GAIN_MARGIN = 2.0
HIGH_SENSITIVITY = 2.0
GRID_POINTS = 24000
MAXIMUM_TIME_RATIO = 1e8      # largest / smallest time scale of the loop that the frequency grid resolves


def _rational(w, gain, taus, kp, ti, td, tf):
    """L(jw) without the dead time (rational part)."""
    s = 1j * w
    integral = 0 if math.isinf(ti) else 1 / (ti * s)           # ti = inf: no integral action (P or PD)
    controller = kp * (1 + integral + (td * s / (tf * s + 1) if td > 0 else 0))
    plant = gain
    for tau in taus:
        plant = plant / (tau * s + 1)
    return controller * plant


def _grid(taus, delay, ti, td, tf):
    slow = max([*taus, *([] if math.isinf(ti) else [ti]), delay, 1e-9])
    fast = min([x for x in (*taus, delay, td, tf) if x > 0] or [slow])
    return np.logspace(math.log10(1e-4 / slow), math.log10(1e3 / fast), GRID_POINTS)


def analyze(gain, taus, delay, kp, ti, td, tf=0.0):
    if (not all(math.isfinite(x) for x in (gain, kp, td, tf, delay, *taus)) or math.isnan(ti) or gain == 0 or kp == 0 or ti <= 0 or
            td < 0 or tf < 0 or delay < 0 or not taus or any(t <= 0 for t in taus) or (td > 0 and tf < 0)):
        raise ValueError("margins_domain")
    # Hostile or absurd time scales (1e-300 s next to 5 s) made the frequency search run until the caller's deadline killed it.
    # Margins are meaningful only when every time scale sits within a range that the grid can resolve: refuse the rest at once.
    scales = [t for t in (*taus, delay, td, tf) if t > 0] + ([] if math.isinf(ti) else [ti])
    if scales and (max(scales) / min(scales) > MAXIMUM_TIME_RATIO or max(scales) > 1e12 or min(scales) < 1e-9):
        raise ValueError("margins_domain")
    if kp * gain <= 0:
        raise ValueError("margins_sign")        # opposite signs: positive feedback, not a loop to qualify
    w = _grid(taus, delay, ti, td, tf)
    rational = _rational(w, gain, taus, kp, ti, td, tf)
    magnitude = np.abs(rational)
    # Rational phase is continuous on this dense grid; the dead time is subtracted exactly afterwards.
    rational_phase = np.unwrap(np.angle(rational))
    phase = rational_phase - w * delay

    def loop(freq):
        return _rational(freq, gain, taus, kp, ti, td, tf) * np.exp(-1j * freq * delay)

    def magnitude_error(freq):
        return abs(loop(freq)) - 1.0

    # Gain crossovers: sign changes of |L| - 1.
    signs = np.sign(magnitude - 1)
    crossings = np.flatnonzero(signs[:-1] * signs[1:] < 0)
    crossovers = [brentq(magnitude_error, w[i], w[i + 1], xtol=1e-14, rtol=1e-12) for i in crossings]

    def phase_at(freq):
        """Total phase at any frequency: unwrapped rational phase from the nearest grid point, minus the dead time."""
        index = int(np.clip(np.searchsorted(w, freq), 1, len(w) - 1))
        reference = _rational(w[index - 1], gain, taus, kp, ti, td, tf)
        local = np.angle(_rational(freq, gain, taus, kp, ti, td, tf) / reference)
        return float(rational_phase[index - 1] + local - freq * delay)

    warnings = []
    margins = [(math.pi + phase_at(c), c) for c in crossovers]
    phase_margin = omega_c = None
    if margins:
        phase_margin, omega_c = min(margins)
        if len(crossovers) > 1:
            warnings.append("multiple_gain_crossovers")
    else:
        warnings.append("no_gain_crossover")

    # Phase crossovers: phase = -pi (mod 2 pi); gain margin is the smallest 1/|L| there.
    shifted = phase + math.pi
    levels = []
    for k in range(0, int(math.ceil(-float(phase.min()) / (2 * math.pi))) + 2):
        wrapped = shifted + 2 * math.pi * k
        for i in np.flatnonzero(wrapped[:-1] * wrapped[1:] < 0):
            level = -math.pi - 2 * math.pi * k
            freq = brentq(lambda f: phase_at(f) - level, w[i], w[i + 1], xtol=1e-14, rtol=1e-12)
            levels.append((1 / abs(loop(freq)), freq))
    gain_margin = omega_180 = None
    if levels:
        gain_margin, omega_180 = min(levels)

    # Peak of the sensitivity |1/(1+L)|: grid maximum refined locally.
    sensitivity = 1 / np.abs(1 + rational * np.exp(-1j * w * delay))
    peak_index = int(np.argmax(sensitivity))
    low, high = w[max(peak_index - 1, 0)], w[min(peak_index + 1, len(w) - 1)]
    refined = minimize_scalar(lambda f: -1 / abs(1 + loop(f)), bounds=(low, high), method="bounded",
                              options={"xatol": 1e-12 * high})
    ms, ms_frequency = max((float(sensitivity[peak_index]), float(w[peak_index])), (float(-refined.fun), float(refined.x)))

    if gain_margin is not None and gain_margin < 1 or phase_margin is not None and phase_margin < 0:
        warnings.append("nominal_unstable")
    else:
        if phase_margin is not None and math.degrees(phase_margin) < LOW_PHASE_MARGIN_DEGREES:
            warnings.append("low_phase_margin")
        if gain_margin is not None and gain_margin < LOW_GAIN_MARGIN:
            warnings.append("low_gain_margin")
        if ms > HIGH_SENSITIVITY:
            warnings.append("high_sensitivity")
    def number(value):
        return None if value is None else float(value)       # plain floats: JSON and the simulators' type checks

    return dict(
        algorithmVersion=VERSION,
        gainCrossoverRadPerSecond=number(omega_c),
        phaseMarginDegrees=None if phase_margin is None else math.degrees(phase_margin),
        delayMarginSeconds=None if phase_margin is None or phase_margin <= 0 else float(phase_margin / omega_c),
        phaseCrossoverRadPerSecond=number(omega_180),
        gainMargin=number(gain_margin),
        gainMarginDb=None if gain_margin is None else 20 * math.log10(gain_margin),
        peakSensitivity=float(ms), peakSensitivityRadPerSecond=float(ms_frequency),
        warnings=warnings,
    )
