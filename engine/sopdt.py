"""SOPDT step estimator with ordered, positive, real time constants."""
import math

import numpy as np
from scipy.optimize import least_squares

from fopdt import delay_interval, delay_warnings, diagnostics, read_trials


def basis(times, tau_slow, tau_fast, delay):
    z = np.maximum(times - delay, 0) / tau_slow
    w = z * ((tau_slow - tau_fast) / tau_fast)
    phi = np.divide(-np.expm1(-w), w, out=np.ones_like(w), where=w != 0)
    return np.maximum(0, -np.expm1(-z) - np.exp(-z) * z * phi)


def identify(payload):
    training, validation = read_trials(payload)
    horizon = float(training.times[-1])
    cadence = float(np.median(np.diff(training.times)))
    low = cadence / 4
    y = training.observations - training.baseline
    scale = float(np.max(np.abs(y)))
    normalized = y / scale

    def parameters(p):
        slow = min(horizon, max(low, math.exp(p[0])))
        return slow, low + (slow - low) * p[1], p[2] * horizon

    def profile(p):
        curve = basis(training.times, *parameters(p))
        amplitude = float(np.dot(curve, normalized) / np.dot(curve, curve))
        return amplitude * curve - normalized, amplitude

    bounds = ([math.log(low), 0, 0], [math.log(horizon), 1, 0.5])
    candidates = []
    for slow in (0.08, 0.25, 0.65):
        for pole_ratio in (0.2, 0.55, 0.9):
            for delay in (0, 0.15, 0.4):
                x = [math.log(min(horizon, max(low, slow * horizon))), pole_ratio, delay]
                result = least_squares(lambda p: profile(p)[0], x, bounds=bounds,
                                       max_nfev=200, ftol=1e-10, xtol=1e-10, gtol=1e-10)
                if result.success:
                    candidates.append(result)
    if not candidates:
        raise ValueError("fit_solver")
    best = min(candidates, key=lambda result: result.cost)
    slow, fast, delay = parameters(best.x)
    gain = profile(best.x)[1] * scale / training.amplitude
    if not math.isfinite(gain) or gain == 0:
        raise ValueError("fit_response")

    warnings = []
    if fast <= 1.02 * low or slow >= 0.98 * horizon or delay >= 0.49 * horizon:
        warnings.append("parameter_bound")
    warnings.extend(delay_warnings(delay, training))
    if min(training.times[-1], validation.times[-1]) - delay < 5 * slow:
        warnings.append("short_horizon")
    if fast < cadence:
        warnings.append("fast_pole_resolution")
    if fast / slow >= 0.95:
        warnings.append("close_poles")
    train_fit = diagnostics(training, gain, basis(training.times, slow, fast, delay))
    if train_fit["residualLagOne"] is not None and abs(train_fit["residualLagOne"]) > 0.3:
        warnings.append("correlated_residuals")
    return dict(algorithmVersion="1.1.0", gain=gain, tauSeconds=slow, secondTauSeconds=fast,
                delaySeconds=delay, delayIntervalSeconds=delay_interval(delay, training), tauLowerSeconds=low, tauUpperSeconds=horizon,
                delayUpperSeconds=horizon / 2, warnings=warnings, training=train_fit,
                validation=diagnostics(validation, gain, basis(validation.times, slow, fast, delay)))
