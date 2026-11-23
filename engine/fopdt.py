"""Candidate FOPDT identification from two disjoint, documented open-loop steps."""
from dataclasses import dataclass
from datetime import datetime, timezone
import math

import numpy as np
from scipy.optimize import least_squares

from csv_quality import NUMBER, TIMESTAMP, read_table

VERSION = "1.3.0"


@dataclass
class Step:
    times: np.ndarray
    observations: np.ndarray
    baseline: float
    amplitude: float
    onset_interval: float
    first_time: datetime
    last_time: datetime
    # Sample standard deviation of PV on the pre-step plateau: measured noise, independent of any fit.
    baseline_noise: float = 0.0


def number(cell):
    cell = cell.strip()
    value = float(cell) if NUMBER.fullmatch(cell) else math.nan
    if not math.isfinite(value) or abs(value) > 1e100:
        raise ValueError("fit_data")
    return value


def read_step(source, input_tolerance, output_tolerance):
    required = {"raw", "delimiter", "timeColumn", "mvColumn", "pvColumn"}
    if not isinstance(source, dict) or (
            source.keys() != required and source.keys() != required | {"rowStart", "rowEnd"}):
        raise ValueError("fit_mapping")
    headers, rows = read_table(source)
    if "rowStart" in source:
        first, last = source["rowStart"], source["rowEnd"]
        if (type(first) is not int or type(last) is not int
                or not 0 <= first < last < len(rows)):
            raise ValueError("fit_window")
        # Inclusive indices refer to original CSV data rows, excluding header.
        # The source is retained intact; only the fitting view is sliced.
        rows = rows[first:last + 1]
    columns = [source[field] for field in ("timeColumn", "mvColumn", "pvColumn")]
    if any(not isinstance(column, str) or column not in headers for column in columns) or len(set(columns)) != 3:
        raise ValueError("fit_mapping")
    ti, ui, yi = [headers.index(column) for column in columns]
    dates = []
    for row in rows:
        try:
            if not TIMESTAMP.fullmatch(row[ti]):
                raise ValueError()
            dates.append(datetime.fromisoformat(row[ti]).astimezone(timezone.utc))
        except (OverflowError, ValueError) as error:
            raise ValueError("fit_time") from error
    times = np.array([(date - dates[0]).total_seconds() for date in dates])
    if np.any(np.diff(times) <= 0):
        raise ValueError("fit_time")
    inputs = np.array([number(row[ui]) for row in rows])
    outputs = np.array([number(row[yi]) for row in rows])
    candidates = np.flatnonzero(np.abs(inputs - inputs[0]) > 2 * input_tolerance)
    if not len(candidates):
        raise ValueError("fit_step")
    i = int(candidates[0])
    if i < 4 or len(rows) - i < 12:
        raise ValueError("fit_step")
    low, high = float(np.mean(inputs[:i])), float(np.mean(inputs[i:]))
    delta = high - low
    if (abs(delta) <= max(10 * input_tolerance, 1e-14 * max(abs(low), abs(high)))
            or np.max(np.abs(inputs[:i] - low)) > input_tolerance
            or np.max(np.abs(inputs[i:] - high)) > input_tolerance):
        raise ValueError("fit_step")
    baseline = float(np.mean(outputs[:i]))
    if np.max(np.abs(outputs[:i] - baseline)) > output_tolerance:
        raise ValueError("fit_baseline")
    if np.max(np.abs(outputs[i:] - baseline)) <= max(4 * output_tolerance, 1e-14 * abs(baseline), 1e-100):
        raise ValueError("fit_response")
    onset = float(times[i])
    # Calculate relative time directly from timestamps to retain sub-second resolution.
    relative = np.array([(date - dates[i]).total_seconds() for date in dates[i:]])
    if np.any(np.diff(relative) <= 0):
        raise ValueError("fit_time")
    return Step(relative, outputs[i:], baseline, delta, onset - float(times[i - 1]), dates[0], dates[-1],
                float(np.std(outputs[:i], ddof=1)))


# Above this ratio of onset uncertainty to delay, the delay is reported as an interval (audit NC-03).
ONSET_UNCERTAINTY_RATIO = 0.1


def delay_warnings(delay, step):
    """The MV step happened in (t[i-1], t[i]]: the true delay lies in [delay, delay + onset interval]."""
    if delay <= step.onset_interval:
        return ["delay_resolution"]
    if step.onset_interval > ONSET_UNCERTAINTY_RATIO * delay:
        return ["delay_onset_uncertainty"]
    return []


def delay_interval(delay, step):
    return [delay, delay + step.onset_interval]


def basis(times, tau, delay):
    return -np.expm1(-np.maximum(times - delay, 0) / tau)


def estimate(step):
    horizon = float(step.times[-1])
    cadence = float(np.median(np.diff(step.times)))
    tau_low, tau_high = cadence / 4, horizon
    delay_high = horizon / 2
    y = step.observations - step.baseline
    scale = float(np.max(np.abs(y)))
    normalized = y / scale
    bounds = ([math.log(tau_low), 0], [math.log(tau_high), 0.5])

    def profile(parameters):
        log_tau, delay_fraction = parameters
        shape = basis(step.times, math.exp(log_tau), delay_fraction * horizon)
        amplitude = np.dot(shape, normalized) / np.dot(shape, shape)
        return amplitude * shape - normalized, float(amplitude)

    candidates = []
    for fraction in (0.05, 0.2, 0.6):
        for delay in (0, 0.08, 0.24, 0.45):
            tau = np.clip(horizon * fraction, tau_low, tau_high)
            result = least_squares(lambda p: profile(p)[0], [math.log(tau), delay],
                                   bounds=bounds, max_nfev=150, ftol=1e-10, xtol=1e-10, gtol=1e-10)
            if result.success:
                candidates.append(result)
    if not candidates:
        raise ValueError("fit_solver")
    best = min(candidates, key=lambda result: result.cost)
    tau, delay = math.exp(best.x[0]), float(best.x[1] * horizon)
    if delay < max(1e-9 * horizon, 1e-12):
        delay = 0.0
    gain = profile([best.x[0], delay / horizon])[1] * scale / step.amplitude
    if not math.isfinite(gain) or gain == 0:
        raise ValueError("fit_response")
    return gain, tau, delay, tau_low, tau_high, delay_high


def diagnostics(step, gain, response):
    predicted = step.baseline + gain * step.amplitude * response
    residual = step.observations - predicted
    centered = residual - np.mean(residual)
    variance = float(np.dot(centered, centered))
    total = float(np.sum((step.observations - np.mean(step.observations)) ** 2))
    sse = float(np.dot(residual, residual))
    observed_range = float(np.ptp(step.observations))
    rmse = math.sqrt(sse / len(predicted))
    ratio = sse / total if total else math.inf
    # Residuals at floating-point round-off carry no information about model structure:
    # their correlation is reported as undetermined instead of raising a false warning (audit NC-10).
    round_off = rmse <= 32 * math.ulp(float(np.max(np.abs(step.observations))))
    indices = np.unique(np.linspace(0, len(predicted) - 1, min(500, len(predicted)), dtype=int))
    return dict(
        samples=len(predicted), inputStep=step.amplitude, baseline=step.baseline, baselineNoise=step.baseline_noise,
        onsetIntervalSeconds=step.onset_interval, horizonSeconds=float(step.times[-1]),
        rmse=rmse, rangeNormalizedRmse=rmse / observed_range if observed_range > 0 else None,
        mae=float(np.mean(np.abs(residual))),
        rSquared=1 - ratio if math.isfinite(ratio) else None,
        residualLagOne=float(np.dot(centered[1:], centered[:-1]) / variance) if variance and not round_off else None,
        points=[dict(seconds=float(step.times[i]), observed=float(step.observations[i]),
                     predicted=float(predicted[i])) for i in indices],
    )


def conditional_errors(step, gain, tau, delay, diagnostics, warnings):
    """Local iid residual error, conditional on the measured baselines and input."""
    correlation = diagnostics["residualLagOne"]
    if correlation is not None and abs(correlation) > 0.3:
        warnings.append("correlated_residuals")
    # The onset interval is an exogenous uncertainty published as delayIntervalSeconds; it does not
    # invalidate the local residual errors, which stay conditional on the measured step instant.
    if set(warnings) - {"delay_onset_uncertainty"} or correlation is None:
        return None

    amplitude = gain * step.amplitude
    scale = float(np.max(np.abs(step.observations - step.baseline)))
    if diagnostics["rmse"] <= scale * 1e-12:
        return None
    # A sample at the estimated delay makes the delay derivative non-unique.
    if np.min(np.abs(step.times - delay)) <= 1e-8 * tau:
        warnings.append("delay_derivative")
        return None

    active = step.times > delay
    v = np.maximum(step.times - delay, 0) / tau
    exp = np.exp(-v)
    scaled_amplitude = amplitude / scale
    jacobian = np.column_stack((basis(step.times, tau, delay),
                               -scaled_amplitude * v * exp,
                               -scaled_amplitude * exp * active))
    _, singular, right = np.linalg.svd(jacobian, full_matrices=False)
    if singular[-1] <= 1e-6 * singular[0]:
        warnings.append("ill_conditioned")
        return None
    degrees = len(step.times) - jacobian.shape[1]
    sigma = diagnostics["rmse"] / scale * math.sqrt(len(step.times) / degrees)
    standard = sigma * np.sqrt(np.sum((right.T / singular) ** 2, axis=1))
    return dict(gain=float(standard[0]) * abs(scale / step.amplitude),
                tauSeconds=float(standard[1]) * tau, delaySeconds=float(standard[2]) * tau)


def read_trials(payload):
    if payload.keys() != {"training", "validation", "mvTolerance", "pvTolerance"}:
        raise ValueError("fit_mapping")
    tolerances = [payload["mvTolerance"], payload["pvTolerance"]]
    if any(type(value) not in (float, int) or not 1e-100 <= value <= 1e100 for value in tolerances):
        raise ValueError("fit_tolerance")
    training = read_step(payload["training"], *tolerances)
    validation = read_step(payload["validation"], *tolerances)
    if not (training.last_time < validation.first_time or validation.last_time < training.first_time):
        raise ValueError("fit_independence")
    if not 0.5 <= abs(validation.amplitude / training.amplitude) <= 2:
        raise ValueError("fit_amplitude")
    return training, validation


def identify(payload):
    training, validation = read_trials(payload)
    gain, tau, delay, tau_low, tau_high, delay_high = estimate(training)
    warnings = []
    if tau <= 1.02 * tau_low or tau >= 0.98 * tau_high or delay >= 0.98 * delay_high:
        warnings.append("parameter_bound")
    warnings.extend(delay_warnings(delay, training))
    if min(training.times[-1], validation.times[-1]) - delay < 4 * tau:
        warnings.append("short_horizon")
    training_diagnostics = diagnostics(training, gain, basis(training.times, tau, delay))
    errors = conditional_errors(training, gain, tau, delay, training_diagnostics, warnings)
    return dict(algorithmVersion=VERSION, gain=gain, tauSeconds=tau, delaySeconds=delay,
                delayIntervalSeconds=delay_interval(delay, training), tauLowerSeconds=tau_low, tauUpperSeconds=tau_high, delayUpperSeconds=delay_high,
                warnings=warnings, standardErrors=errors, training=training_diagnostics,
                validation=diagnostics(validation, gain, basis(validation.times, tau, delay)))
