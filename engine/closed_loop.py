"""EXPERIMENTAL identification of a first-order-plus-dead-time process from CLOSED-LOOP data (ADR 0006).

The controller stays in automatic. The excitation is the setpoint, changed by the user; MKR never writes to the PLC.
Two-stage method (Van den Hof and Schrama, Automatica 1993): (1) the part of MV that the setpoint explains is estimated by a
finite-impulse-response regression of MV on the setpoint history; (2) a FOPDT model is fitted from that projected MV to PV.
No assumption is made about the controller structure or parameters, so it does not depend on the PLC or its PID block.

Every doubtful condition is a refusal, never a silent fit: sampling not uniform, MV at a declared limit (saturation), no or weak
setpoint excitation, a row taken in manual (MANUAL column), windows that overlap, no quiet baseline to measure the noise.
The result is labelled experimental: it is validated on simulations only (ADR 0006) and carries its estimation dispersion.
"""
import math

import numpy as np
from scipy.optimize import least_squares
from scipy.signal import lfilter

from csv_format import from_payload
from csv_quality import read_table
from fopdt import number

VERSION = "0.1.0"
BASELINE_ROWS = 8                # quiet rows needed before the first setpoint change to measure the noise
MAXIMUM_TAPS = 150
EXCITATION_NOISE_RATIO = 20      # setpoint range must exceed this multiple of the measured noise
SATURATION_MARGIN = 0.005        # fraction of the MV span around a declared limit
UNIFORMITY = 0.01
BOOTSTRAP = 30
BLOCK_FRACTION = 20          # block length = window / 20 (at least 10): residuals are coloured by the load disturbance
MAXIMUM_RELATIVE_DISPERSION = dict(gain=0.3, tau=0.5)
RESIDUAL_LAG_ONE = 0.5
MANUAL_COLUMN = "MANUAL"


def _read(payload):
    required = {"source", "training", "validation", "mvLower", "mvUpper", "spTolerance", "pvTolerance"}
    if not isinstance(payload, dict) or payload.keys() != required:
        raise ValueError("cl_mapping")
    for key in ("mvLower", "mvUpper", "spTolerance", "pvTolerance"):
        if type(payload[key]) not in (int, float) or not math.isfinite(payload[key]):
            raise ValueError("cl_mapping")
    if payload["mvUpper"] <= payload["mvLower"] or payload["spTolerance"] <= 0 or payload["pvTolerance"] <= 0:
        raise ValueError("cl_mapping")
    source = payload["source"]
    columns = ("timeColumn", "spColumn", "mvColumn", "pvColumn")
    if not isinstance(source, dict) or source.keys() - {"format"} != {"raw", "delimiter", *columns}:
        raise ValueError("cl_mapping")
    try:
        fmt = from_payload(source, source["delimiter"])
    except ValueError as error:
        raise ValueError("cl_mapping") from error
    headers, rows = read_table(source)
    names = [source[c] for c in columns]
    if any(not isinstance(n, str) or n not in headers for n in names) or len(set(names)) != 4:
        raise ValueError("cl_mapping")
    ti, si, ui, yi = (headers.index(n) for n in names)
    try:
        dates = [fmt.timestamp(row[ti]) for row in rows]
    except (OverflowError, ValueError) as error:
        raise ValueError("cl_time") from error
    times = np.array([(d - dates[0]).total_seconds() for d in dates])
    if np.any(np.diff(times) <= 0):
        raise ValueError("cl_time")

    def column(index):
        out = []
        for row in rows:
            try:
                out.append(number(row[index], fmt))
            except ValueError:
                out.append(math.nan)
        return np.array(out)

    manual = column(headers.index(MANUAL_COLUMN)) if MANUAL_COLUMN in headers else None
    windows = []
    for key in ("training", "validation"):
        w = payload[key]
        if (not isinstance(w, dict) or w.keys() != {"rowStart", "rowEnd"} or type(w["rowStart"]) is not int or
                type(w["rowEnd"]) is not int or not 0 <= w["rowStart"] < w["rowEnd"] < len(rows)):
            raise ValueError("cl_window")
        windows.append((w["rowStart"], w["rowEnd"]))
    (a0, a1), (b0, b1) = windows
    if not (a1 < b0 or b1 < a0):
        raise ValueError("cl_independence")
    return times, column(si), column(ui), column(yi), manual, windows


def fit_fopdt(times, u, y):
    """Least-squares FOPDT (K, tau, delay) from deviation signals u to y, delay fractional by linear interpolation of u."""
    dt = float(np.median(np.diff(times)))
    span = float(times[-1] - times[0])

    def model(p, inp=u, grid=times):
        gain, tau, delay = p
        a = math.exp(-dt / tau)
        delayed = np.interp(grid - delay, grid, inp, left=0.0)
        return lfilter([0.0, (1 - a) * gain], [1.0, -a], delayed)

    best = None
    scale = max(float(np.ptp(y)) / max(float(np.ptp(u)), 1e-12), 1e-6)
    for delay0 in (0.5 * dt, 3 * dt, 10 * dt):
        for tau0 in (span / 20, span / 5):
            try:
                r = least_squares(lambda p: model(p) - y, [scale, tau0, delay0],
                                  bounds=([-1e6, dt / 2, 0.0], [1e6, 100 * span, span / 2]))
            except ValueError:
                continue
            if best is None or r.cost < best.cost:
                best = r
    if best is None:
        raise ValueError("cl_fit")
    return best.x, (lambda p, inp, grid: model(p, inp, grid))


def _fir_matrix(sp, rows, taps):
    """Regressors: setpoint at lags 0..taps-1 for each row (rows before the file start repeat its first value)."""
    index = rows[:, None] - np.arange(taps)[None, :]
    return sp[np.clip(index, 0, None)]


def _noise(times, sp, pv, window, tolerance):
    first, last = window
    changes = np.flatnonzero(np.abs(np.diff(sp[first:last + 1])) > 2 * tolerance)
    quiet = int(changes[0]) + 1 if len(changes) else last - first + 1
    if quiet < BASELINE_ROWS:
        raise ValueError("cl_baseline")
    y = pv[first:first + quiet]
    return float(np.std(np.diff(y), ddof=1) / math.sqrt(2)), len(changes)


def identify(payload):
    times, sp, mv, pv, manual, windows = _read(payload)
    lower, upper = payload["mvLower"], payload["mvUpper"]
    margin = SATURATION_MARGIN * (upper - lower)
    sp_tolerance = payload["spTolerance"]
    steps = np.diff(times)
    noise, trial_rows = [], []
    for first, last in windows:
        sl = slice(first, last + 1)
        if not (np.all(np.isfinite(sp[sl])) and np.all(np.isfinite(mv[sl])) and np.all(np.isfinite(pv[sl]))):
            raise ValueError("cl_data")
        if np.ptp(steps[first:last]) > UNIFORMITY * np.median(steps[first:last]):
            raise ValueError("cl_sampling")
        if manual is not None and np.any(manual[sl] != 0):
            raise ValueError("cl_manual_in_window")
        if np.any(mv[sl] <= lower + margin) or np.any(mv[sl] >= upper - margin):
            raise ValueError("cl_saturation")
        sigma, changes = _noise(times, sp, pv, (first, last), sp_tolerance)
        if changes < 1 or np.ptp(sp[sl]) < EXCITATION_NOISE_RATIO * max(sigma, 1e-12):
            raise ValueError("cl_excitation")
        noise.append(sigma)
        trial_rows.append(np.arange(first, last + 1))
    sigma = math.sqrt(sum(s * s for s in noise) / 2)

    def deviations(rows):
        base = slice(0, BASELINE_ROWS)
        return (sp[rows] - sp[rows][base].mean(), mv[rows] - mv[rows][base].mean(), pv[rows] - pv[rows][base].mean())

    rows_train, rows_valid = trial_rows
    taps = min(len(rows_train) // 3, MAXIMUM_TAPS)
    if taps < 5:
        raise ValueError("cl_window")
    sp_train, mv_train, pv_train = deviations(rows_train)
    sp_all = sp - sp[rows_train][:BASELINE_ROWS].mean()
    design = _fir_matrix(sp_all, rows_train, taps)
    ridge = 1e-9 * float(np.trace(design.T @ design))
    beta = np.linalg.solve(design.T @ design + ridge * np.eye(taps), design.T @ mv_train)    # stage 1
    projected = design @ beta
    params, model = fit_fopdt(times[rows_train] - times[rows_train][0], projected, pv_train)   # stage 2
    grid_train = times[rows_train] - times[rows_train][0]
    fitted = model(params, projected, grid_train)
    residual = pv_train - fitted

    # Dispersion by block bootstrap of the residuals (stage 2 only; stage 1 is kept).
    rng = np.random.default_rng(12345)
    estimates = []
    block = max(10, len(residual) // BLOCK_FRACTION)
    blocks = max(1, len(residual) // block)
    for _ in range(BOOTSTRAP):
        starts = rng.integers(0, len(residual) - block + 1, blocks)
        resampled = np.concatenate([residual[s:s + block] for s in starts])[:len(residual)]
        resampled = np.concatenate([resampled, residual[len(resampled):]])
        try:
            estimates.append(fit_fopdt(grid_train, projected, fitted + resampled)[0])
        except ValueError:
            continue
    if len(estimates) < BOOTSTRAP // 2:
        raise ValueError("cl_fit")
    spread = np.std(np.array(estimates), axis=0, ddof=1)

    # Second, independent estimate: the same two stages run on the validation window alone. The two windows come from different
    # excitations and disturbances, so their difference measures the real run-to-run dispersion, which the residual bootstrap
    # alone underestimates when the residuals are coloured by an unmeasured load disturbance.
    sp_valid, _, pv_valid = deviations(rows_valid)
    sp_valid_all = sp - sp[rows_valid][:BASELINE_ROWS].mean()
    grid_valid = times[rows_valid] - times[rows_valid][0]
    design_valid = _fir_matrix(sp_valid_all, rows_valid, taps)
    _, mv_valid, _ = deviations(rows_valid)
    ridge_valid = 1e-9 * float(np.trace(design_valid.T @ design_valid))
    beta_valid = np.linalg.solve(design_valid.T @ design_valid + ridge_valid * np.eye(taps), design_valid.T @ mv_valid)
    params_valid = fit_fopdt(grid_valid, design_valid @ beta_valid, pv_valid)[0]
    between = np.abs(params - params_valid) / math.sqrt(2)           # sample standard deviation of two estimates
    spread = np.maximum(spread, between)

    # Validation: the models learned on the training window predict the independent window.
    projected_valid = design_valid @ beta
    prediction = model(params, projected_valid, grid_valid)
    error = pv_valid - prediction
    rmse = float(math.sqrt(np.mean(error ** 2)))
    lag_one = float(np.corrcoef(error[:-1], error[1:])[0, 1]) if np.std(error) > 0 else 0.0
    maximum_rmse = max(2 * sigma, payload["pvTolerance"] / 2)
    gain, tau, delay = (float(v) for v in params)
    reasons = []
    if rmse > maximum_rmse:
        reasons.append("validation_rmse")
    if spread[0] > MAXIMUM_RELATIVE_DISPERSION["gain"] * abs(gain):
        reasons.append("dispersion_gain")
    if spread[1] > MAXIMUM_RELATIVE_DISPERSION["tau"] * tau:
        reasons.append("dispersion_tau")
    warnings = ["closed_loop_experimental", "validated_on_simulation_only"]
    if abs(lag_one) > RESIDUAL_LAG_ONE:
        # Expected with an unmeasured coloured disturbance even for a correct model: announced, not a refusal.
        warnings.append("correlated_residuals")
    if tau > (times[rows_train][-1] - times[rows_train][0]) / 4:
        warnings.append("short_horizon")
    return dict(algorithmVersion=VERSION, method="two_stage_fir", gain=gain, tauSeconds=tau, delaySeconds=delay,
                independentEstimate=dict(gain=float(params_valid[0]), tauSeconds=float(params_valid[1]), delaySeconds=float(params_valid[2])),
                standardErrors=dict(gain=float(spread[0]), tauSeconds=float(spread[1]), delaySeconds=float(spread[2])),
                noiseSigma=sigma, taps=int(taps), warnings=warnings,
                validation=dict(rmse=rmse, residualLagOne=lag_one, maximumRmse=maximum_rmse),
                assessment=dict(accepted=not reasons, reasons=reasons))
