"""Broïda two-point FOPDT identification from an independent open-loop step.

28%/40% crossings are measured relative to the detected MV step and an
observed *stable* tail plateau. No model optimization, no smoothing, and
no use of validation data for parameter estimation.
"""
import math

import numpy as np

from fopdt import basis, diagnostics, read_trials

VERSION = "1.0.0"
FRACTIONS = (0.28, 0.40)
TAIL_SIZE = 6


def crossing(times, normalized, fraction):
    """First threshold crossing, linearly interpolated between true samples."""
    indices = np.flatnonzero(normalized >= fraction)
    if not len(indices):
        raise ValueError("broida_crossing")
    index = int(indices[0])
    if index == 0:
        raise ValueError("broida_crossing")
    lower = float(normalized[index - 1])
    upper = float(normalized[index])
    if not upper > lower:
        raise ValueError("broida_crossing")
    weight = (fraction - lower) / (upper - lower)
    if weight < 0 or weight > 1:
        raise ValueError("broida_crossing")
    return float(times[index - 1] + weight * (times[index] - times[index - 1]))


def estimate(step, tolerance):
    times, pv = step.times, step.observations
    if len(pv) < TAIL_SIZE + 12:
        raise ValueError("broida_horizon")
    tail = pv[-TAIL_SIZE:]
    tail_level = float(np.mean(tail))
    if np.ptp(tail) > 2 * tolerance:
        raise ValueError("broida_plateau")
    step_response = tail_level - step.baseline
    if abs(step_response) <= max(10 * tolerance, 1e-12 * max(abs(tail_level), abs(step.baseline))):
        raise ValueError("broida_response")
    normalized = (pv - step.baseline) / step_response
    # Disallow non-monotone data beyond the declared PV resolution. This is
    # intentionally conservative: noisy trials require an explicit, audited
    # preprocessing policy, not hidden smoothing.
    resolution = 2 * tolerance / abs(step_response)
    if (np.min(normalized) < -resolution or
            np.max(normalized) > 1 + resolution or
            np.min(np.diff(normalized)) < -resolution):
        raise ValueError("broida_shape")
    t28 = crossing(times, normalized, FRACTIONS[0])
    t40 = crossing(times, normalized, FRACTIONS[1])
    if not 0 < t28 < t40 < float(times[-1]):
        raise ValueError("broida_crossing")
    tau = 5.5 * (t40 - t28)
    delay = 2.8 * t28 - 1.8 * t40
    # A negative dead time invalidates the classical approximation.
    # Never silently clip it to 0.
    if not (math.isfinite(tau) and math.isfinite(delay)) or tau <= 0 or delay < 0:
        raise ValueError("broida_domain")
    gain = step_response / step.amplitude
    if not math.isfinite(gain) or gain == 0:
        raise ValueError("broida_response")
    if float(times[-1]) - delay < 4 * tau:
        raise ValueError("broida_horizon")
    return gain, tau, delay, t28, t40, tail_level


def identify(payload):
    training, validation = read_trials(payload)
    tolerance = payload["pvTolerance"]
    gain, tau, delay, t28, t40, plateau = estimate(training, tolerance)
    warnings = ["two_point_estimator", "measured_tail_plateau", "interpolated_crossings"]
    if delay <= training.onset_interval:
        warnings.append("delay_resolution")
    # Estimate is independent of validation: use only pre-existing parameters.
    fit_train = diagnostics(training, gain, basis(training.times, tau, delay))
    fit_validation = diagnostics(validation, gain, basis(validation.times, tau, delay))
    return dict(
        algorithmVersion=VERSION,
        gain=gain, tauSeconds=tau, delaySeconds=delay,
        tauLowerSeconds=None, tauUpperSeconds=None,
        delayUpperSeconds=float(training.times[-1]),
        standardErrors=None, secondTauSeconds=None, warnings=warnings,
        training=fit_train, validation=fit_validation,
        broidaEvidence=dict(
            crossing28Seconds=t28, crossing40Seconds=t40,
            terminalPv=plateau, terminalSamples=TAIL_SIZE,
            timeOrigin="mv_step_first_changed_sample",
            thresholdFractions=list(FRACTIONS),
        ),
    )
