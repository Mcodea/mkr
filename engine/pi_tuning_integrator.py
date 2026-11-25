"""Theoretical PI settings for an integrating process with dead time (tank level, position), MKR-SPEC-001 §8.

Process: PV' = k' MV(t - theta), k' in PV units per second per MV unit (the 'gain' of MKR's integrator model).
Rule: SIMC for integrating processes, Skogestad, "Simple analytic rules for model reduction and PID controller tuning",
Journal of Process Control 13 (2003) 291-309, with a first-order model time constant tau1 -> infinity:
    Kc = 1 / (k' (tau_c + theta)),   Ti = 4 (tau_c + theta).
tau_c is the response time the user chooses. Kp has the sign of k' (u = Kp (SP - PV)). The ideal form Kp (1 + 1/(Ti s)) is the
one MKR uses for the other families. The margins below are those of the NOMINAL loop only.
"""
import math

import numpy as np

VERSION = "1.0.0"
LOW_PHASE_MARGIN_DEGREES = 30.0
LOW_GAIN_MARGIN = 2.0
HIGH_SENSITIVITY = 2.0


def loop_margins(slope, delay, kp, ti):
    """Gain margin, phase margin (degrees) and peak sensitivity of L(s) = Kp (1 + 1/(Ti s)) k' exp(-theta s) / s."""
    k = abs(kp * slope)                       # kp and k' have the same sign: only the product matters
    scale = max(ti, delay, 1e-9)
    w = np.logspace(math.log10(1e-4 / scale), math.log10(1e3 / max(min(ti, delay if delay > 0 else ti), 1e-9)), 40000)
    s = 1j * w
    loop = k * (1 + 1 / (ti * s)) / s * np.exp(-s * delay)
    magnitude = np.abs(loop)
    phase = np.unwrap(np.angle(loop))
    crossing = np.flatnonzero((magnitude[:-1] - 1) * (magnitude[1:] - 1) <= 0)
    phase_margin = None
    if len(crossing):
        i = int(crossing[0])
        phase_margin = float(np.degrees(phase[i] + math.pi))
    gain_margin = None
    flips = np.flatnonzero((phase[:-1] + math.pi) * (phase[1:] + math.pi) <= 0)
    flips = flips[magnitude[flips] > 0]
    if len(flips):
        gain_margin = float(1 / magnitude[int(flips[0])])
    sensitivity = float(np.max(1 / np.abs(1 + loop)))
    return gain_margin, phase_margin, sensitivity


def calculate(payload):
    if not isinstance(payload, dict) or payload.keys() != {"gain", "delaySeconds", "responseSeconds"}:
        raise ValueError("pi_input")
    slope, delay, response = (payload[key] for key in ("gain", "delaySeconds", "responseSeconds"))
    if any(type(x) not in (int, float) or not -1e100 <= x <= 1e100 for x in (slope, delay, response)):
        raise ValueError("pi_input")
    if slope == 0 or delay < 0 or response <= 0:
        raise ValueError("pi_domain")
    effective = response + delay
    kp = 1 / (slope * effective)
    ti = 4 * effective
    if not all(math.isfinite(x) and x != 0 for x in (kp, ti, kp / ti)):
        raise ValueError("pi_range")
    warnings = ["exploratory_model", "actuator_limits_unassessed", "integrating_process_simc"]
    if response < delay:
        warnings.append("tight_response")
    gain_margin, phase_margin, sensitivity = loop_margins(slope, delay, kp, ti)
    if phase_margin is not None and phase_margin < LOW_PHASE_MARGIN_DEGREES:
        warnings.append("low_phase_margin")
    if gain_margin is not None and gain_margin < LOW_GAIN_MARGIN:
        warnings.append("low_gain_margin")
    if sensitivity > HIGH_SENSITIVITY:
        warnings.append("high_sensitivity")
    return dict(algorithmVersion=VERSION, rule="SIMC_integrating", responseSeconds=response, warnings=warnings,
                kp=kp, tiSeconds=ti, parallelKi=kp / ti,
                margins=dict(gainMargin=gain_margin, phaseMarginDegrees=phase_margin, peakSensitivity=sensitivity))
