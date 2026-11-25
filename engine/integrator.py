"""Integrating process with dead time, fitted to an open-loop step."""
import math

import numpy as np

from fopdt import delay_interval, delay_warnings, diagnostics, read_trials, residual_lag_bound


def profile_delay(times, observed):
    """Maximize explained sum of squares over every active-sample interval."""
    def suffix(values):
        return np.cumsum(values[::-1])[::-1]

    # The final time is 1, so at least one sample is active on the whole search domain.
    edges = np.concatenate(([0.0], times[(times > 0) & (times < 0.5)], [0.5]))
    starts = np.searchsorted(times, edges[:-1], side="right")
    a, b = suffix(times * observed)[starts], suffix(observed)[starts]
    c, d = suffix(times * times)[starts], suffix(times)[starts]
    n = len(times) - starts
    divisor = b * d - a * n
    critical = np.divide(b * c - a * d, divisor, out=edges[:-1].copy(), where=divisor != 0)
    critical = np.clip(critical, edges[:-1], edges[1:])
    candidates = np.column_stack((edges[:-1], edges[1:], critical))
    numerators = a[:, None] - b[:, None] * candidates
    denominators = c[:, None] - 2 * d[:, None] * candidates + n[:, None] * candidates**2
    i, j = np.unravel_index(np.argmax(numerators**2 / denominators), candidates.shape)
    return float(candidates[i, j]), float(numerators[i, j] / denominators[i, j])


def estimate(step):
    """Fit one trial: (gain, delay, delay fraction of the horizon)."""
    horizon = float(step.times[-1])
    y = step.observations - step.baseline
    scale = float(np.max(np.abs(y)))
    fraction, normalized_gain = profile_delay(step.times / horizon, y / scale)
    delay = fraction * horizon
    gain = normalized_gain * scale / horizon / step.amplitude
    if not math.isfinite(gain) or gain == 0:
        raise ValueError("fit_response")
    return gain, delay, fraction


def identify(payload):
    training, validation = read_trials(payload)
    horizon = float(training.times[-1])
    gain, delay, fraction = estimate(training)

    def tail_change(step):
        n = len(step.observations)
        window = max(2, n // 8)
        first = float(np.mean(step.observations[-2 * window:-window]))
        last = float(np.mean(step.observations[-window:]))
        return last - first

    expected_sign = math.copysign(1, gain * training.amplitude)
    if expected_sign * tail_change(training) <= 4 * payload["pvTolerance"]:
        raise ValueError("fit_integrator_tail")
    warnings = []
    if fraction >= 0.49:
        warnings.append("parameter_bound")
    warnings.extend(delay_warnings(delay, training))
    if math.copysign(1, gain * validation.amplitude) * tail_change(validation) <= 4 * payload["pvTolerance"]:
        warnings.append("integrator_validation_tail")
    train_fit = diagnostics(training, gain, np.maximum(training.times - delay, 0))
    if train_fit["residualLagOne"] is not None and abs(train_fit["residualLagOne"]) > residual_lag_bound(len(training.times)):
        warnings.append("correlated_residuals")
    errors = None
    if set(warnings) - {"delay_onset_uncertainty"} == set() and train_fit["residualLagOne"] is not None:
        from model_drift import numerical_errors
        errors = numerical_errors(training, ("gain", "delaySeconds"), dict(gain=gain, delaySeconds=delay),
                                  lambda v: np.maximum(training.times - v[1], 0))
    return dict(algorithmVersion="1.1.0", gain=gain, delaySeconds=delay, delayIntervalSeconds=delay_interval(delay, training),
                standardErrors=errors, delayUpperSeconds=horizon / 2, warnings=warnings,
                training=train_fit, validation=diagnostics(validation, gain, np.maximum(validation.times - delay, 0)))
