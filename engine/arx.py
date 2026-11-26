"""ARX identification of an open-loop experiment with ARBITRARY excitation (steps, pulses, PRBS), without a steady state at the start.

Model (Ljung, System Identification: Theory for the User, 2nd ed., 1999, ch. 4 and 10; discrete time, uniform sampling Ts):
    y(k) = -a1 y(k-1) - ... - a_na y(k-na) + b1 u(k-nk) + ... + b_nb u(k-nk-nb+1) + d + e(k)
estimated by linear least squares; d absorbs the operating point, so no steady-state initial condition is needed. Covariance
sigma^2 (Phi' Phi)^-1 with sigma^2 = RSS / (N - p) (Ljung 1999, ch. 7). The parameters are valid only for OPEN-LOOP data: under feedback the
ARX estimate is biased by the noise-input correlation, which the user must exclude by declaring the experiment open-loop (otherwise refused).

Two noise hypotheses, two estimators, each judged by its OWN residuals (Ljung 1999, ch. 7 and 16):
  * ARX (equation error: noise enters with the dynamics): linear least squares; the one-step prediction residuals must be white.
  * OE (output error: measurement noise on the PV, the usual case for sensors): the ARX parameters refined by non-linear least squares on the
    free-run simulation error; the simulation residuals must be white. Plain ARX is biased by measurement noise, OE is not.
Model choice never rests on the fit alone. A candidate is ACCEPTED only if, on an independent validation series,
  1. it is stable (poles inside the unit circle);
  2. the free-run SIMULATION error stays within the declared maximum RMSE;
  3. the residuals of the estimator that produced it are white: lag-one autocorrelation within +/- 2.576 / sqrt(N) (99 %);
  4. those residuals are uncorrelated with the inputs at lags 0-20 (Bonferroni-corrected 99 % bound for the 21 simultaneous tests): no unmodelled
     dynamics, no feedback.
Among accepted candidates the one with the fewest parameters wins; ties go to the smaller validation simulation error. No candidate accepted:
the identification is refused and says why.
For (na, nb) = (1, 1) the exact zero-order-hold equivalent is a first-order model with delay: p = -a1, tau = -Ts / ln(p), K = b1 / (1 - p),
delay = (nk - 1) Ts, with standard errors by the delta method. The delay is a multiple of Ts: finer resolution needs a smaller Ts.
"""
import math

import numpy as np
from scipy.optimize import least_squares
from scipy.signal import lfilter, lfiltic
from scipy.stats import norm

from csv_format import from_payload
from csv_quality import read_table
from fopdt import LJUNG_Z_99, is_round_off, number

VERSION = "1.0.0"
MAXIMUM_DELAY_SAMPLES = 60
DEFAULT_ORDERS = ((1, 1), (2, 2))
UNIFORM_SAMPLING_TOLERANCE = 1e-3         # relative jitter of the sampling period accepted without resampling (never interpolate data)
MINIMUM_SAMPLES_PER_PARAMETER = 10
MAXIMUM_ABSOLUTE = 1e100


def read_series(source, mv_tolerance):
    required = {"raw", "delimiter", "timeColumn", "mvColumn", "pvColumn"}
    keys = source.keys() - {"format"} if isinstance(source, dict) else None
    if keys != required and keys != required | {"rowStart", "rowEnd"}:
        raise ValueError("arx_mapping")
    try:
        fmt = from_payload(source, source["delimiter"])
    except ValueError as error:
        raise ValueError("arx_mapping") from error
    headers, rows = read_table(source)
    if "rowStart" in source:
        first, last = source["rowStart"], source["rowEnd"]
        if type(first) is not int or type(last) is not int or not 0 <= first < last < len(rows):
            raise ValueError("arx_mapping")
        rows = rows[first:last + 1]          # inclusive data-row indices, as for the step identification; the source stays intact
    columns = [source[k] for k in ("timeColumn", "mvColumn", "pvColumn")]
    if any(not isinstance(c, str) or c not in headers for c in columns) or len(set(columns)) != 3:
        raise ValueError("arx_mapping")
    ti, ui, yi = (headers.index(c) for c in columns)
    try:
        dates = [fmt.timestamp(row[ti]) for row in rows]
    except (OverflowError, ValueError) as error:
        raise ValueError("arx_time") from error
    times = np.array([(d - dates[0]).total_seconds() for d in dates])
    if len(times) < 40 or np.any(np.diff(times) <= 0):
        raise ValueError("arx_time")
    inputs = np.array([number(row[ui], fmt) for row in rows])
    outputs = np.array([number(row[yi], fmt) for row in rows])
    ts = float(np.median(np.diff(times)))
    if np.max(np.abs(np.diff(times) - ts)) > UNIFORM_SAMPLING_TOLERANCE * ts:
        raise ValueError("arx_sampling")
    if np.ptp(inputs) <= max(10 * mv_tolerance, 1e-14 * np.max(np.abs(inputs))):
        raise ValueError("arx_excitation")
    return ts, inputs, outputs


def regressors(u, y, na, nb, nk):
    """Rows k = start..N-1; columns -y(k-1..k-na), u(k-nk..k-nk-nb+1), 1."""
    start = max(na, nk + nb - 1)
    n = len(y)
    columns = [-y[start - i:n - i] for i in range(1, na + 1)]
    columns += [u[start - nk - j:n - nk - j] for j in range(nb)]
    columns.append(np.ones(n - start))
    return np.column_stack(columns), y[start:], start


def fit(u, y, na, nb, nk):
    phi, target, start = regressors(u, y, na, nb, nk)
    scale = np.linalg.norm(phi, axis=0)
    if np.any(scale == 0):
        raise ValueError("arx_ill_conditioned")
    _, singular, right = np.linalg.svd(phi / scale, full_matrices=False)
    if singular[-1] <= 1e-9 * singular[0]:
        raise ValueError("arx_ill_conditioned")
    theta = np.linalg.lstsq(phi / scale, target, rcond=None)[0] / scale
    residual = target - phi @ theta
    dof = len(target) - phi.shape[1]
    if dof <= 0:
        raise ValueError("arx_data")
    sigma2 = float(residual @ residual) / dof
    covariance = sigma2 * ((right.T / singular ** 2) @ right) / np.outer(scale, scale)
    return theta, covariance, sigma2


def simulate(theta, na, nb, nk, u, y):
    """Free-run simulation from the first measured samples (seeds), vectorised with a linear filter. Returns (simulation, start) or (None, start)."""
    start = max(na, nk + nb - 1)
    a, b, d = theta[:na], theta[na:na + nb], theta[-1]
    denominator = np.concatenate(([1.0], a))
    total = 1.0 + float(np.sum(a))
    if abs(total) < 1e-12:
        return None, start
    equilibrium = d / total                                         # y - equilibrium obeys the homogeneous equation
    numerator = np.concatenate((np.zeros(nk), b))
    y_past = (y[start - 1::-1][:na] - equilibrium) if na else np.array([])
    x_past = u[start - 1::-1][:len(numerator) - 1]
    zi = lfiltic(numerator, denominator, y_past, x_past)
    out = np.array(y, dtype=float)
    out[start:] = lfilter(numerator, denominator, u[start:], zi=zi)[0] + equilibrium
    if not np.all(np.isfinite(out)) or np.max(np.abs(out)) > 1e150:
        return None, start
    return out, start


def output_error_fit(u, y, na, nb, nk, theta0):
    """Output-error refinement: minimise the simulation error on the training series, starting from the ARX estimate."""
    def residual(theta):
        simulated, start = simulate(theta, na, nb, nk, u, y)
        return np.full(len(y) - start, 1e6) if simulated is None else y[start:] - simulated[start:]

    result = least_squares(residual, theta0, method="lm", xtol=1e-12, ftol=1e-12, max_nfev=400 * len(theta0))
    if not result.success and result.status <= 0:
        return None
    r = result.fun
    dof = len(r) - len(theta0)
    if dof <= 0:
        return None
    sigma2 = float(r @ r) / dof
    try:
        covariance = sigma2 * np.linalg.inv(result.jac.T @ result.jac)
    except np.linalg.LinAlgError:
        return None
    return result.x, covariance, sigma2


def lag_correlation(x, z, lag):
    x = x - np.mean(x)
    z = z - np.mean(z)
    n = len(x) - lag
    denominator = math.sqrt(float(x @ x) * float(z @ z))
    return float(x[lag:] @ z[:n]) / denominator if denominator > 0 else 0.0


def poles(theta, na):
    return np.roots(np.concatenate(([1.0], theta[:na])))


def evaluate(theta, na, nb, nk, u, y, maximum_rmse, estimator):
    """Judge a candidate on the validation series with the residuals that match the estimator that produced it."""
    reasons = []
    p = poles(theta, na)
    if np.any(np.abs(p) >= 1.0):
        reasons.append("unstable_model")
    simulated, start = simulate(theta, na, nb, nk, u, y)
    if simulated is None:
        return dict(reasons=reasons + ["simulation_diverges"], simulationRmse=None, residualLagOne=None, inputCorrelation=None, poles=p)
    sim_error = y[start:] - simulated[start:]
    rmse = math.sqrt(float(sim_error @ sim_error) / len(sim_error))
    if not math.isfinite(rmse) or rmse > maximum_rmse:
        reasons.append("validation_simulation_rmse")
    if estimator == "ARX":
        phi, target, _ = regressors(u, y, na, nb, nk)
        e = target - phi @ theta                                   # one-step prediction residuals (white under equation-error noise)
    else:
        e = sim_error                                              # simulation residuals (white under output-error noise)
    bound = min(1.0, LJUNG_Z_99 / math.sqrt(len(e)))
    scale = max(abs(float(np.max(np.abs(y)))), 1e-300)
    exact = is_round_off(math.sqrt(float(e @ e) / len(e)), scale)
    lag_one = lag_correlation(e[1:], e[:-1], 0) if len(e) > 2 else 0.0
    if not exact and abs(lag_one) > bound:
        reasons.append("validation_residuals_not_white")
    k0 = len(y) - len(e)
    lags = min(21, len(e) // 4)
    worst = max(abs(lag_correlation(e, u[k0:], lag)) for lag in range(0, lags))
    # 21 correlations are tested together: Bonferroni keeps the overall level at 99 % (a single-lag bound would reject a correct model one time in five)
    cross_bound = min(1.0, float(norm.isf(0.005 / lags)) / math.sqrt(len(e)))
    if not exact and worst > cross_bound:
        reasons.append("residuals_correlated_with_input")
    return dict(reasons=reasons, simulationRmse=rmse, residualLagOne=lag_one, inputCorrelation=worst, bound=bound, poles=p)


def first_order_equivalent(theta, covariance, ts, nk, horizon=None):
    """Exact zero-order-hold equivalent of ARX(1,1,nk) as first order with delay (None when the pole is not in (0, 1))."""
    a1, b1 = float(theta[0]), float(theta[1])
    pole = -a1
    if not 0 < pole < 1:
        return None
    # A pole indistinguishable from 1 is an integrator: tau = -Ts / ln p and K = b / (1 - p) diverge and are not identifiable (the data fix
    # only K / tau, the slope). The first-order equivalent is published only when p is below 1 at the same 99 % level as the residual tests
    # (LOT 0b, P0-2): an ARX model of an integrator stays a valid predictor, but is never presented as a first-order process.
    if pole + LJUNG_Z_99 * math.sqrt(max(float(covariance[0, 0]), 0.0)) >= 1.0:
        return None
    # Observability: a time constant longer than the recorded duration is not seen as curvature in the data, only as a slope (the local
    # standard error of the pole, computed in the linearised model, would claim otherwise). Not published beyond the training record.
    if horizon is not None and -ts / math.log(pole) > horizon:
        return None
    tau = -ts / math.log(pole)
    gain = b1 / (1 - pole)
    # delta method: tau(p) = -Ts / ln p, K(b, p) = b / (1 - p) with p = -a1
    d_tau_d_a1 = -ts / (math.log(pole) ** 2) / pole                     # d tau / d p = Ts / (p ln^2 p); dp/da1 = -1
    d_tau_d_a1 = ts / (pole * math.log(pole) ** 2) * -1.0
    gradient_gain = np.array([b1 / (1 - pole) ** 2 * -1.0 * -1.0, 1 / (1 - pole)])       # dK/da1 = b/(1-p)^2 * dp/da1 ... see below
    # dK/da1 = d/da1 [ b1 / (1 + a1) ] = -b1 / (1 + a1)^2 ; dK/db1 = 1 / (1 + a1)
    gradient_gain = np.array([-b1 / (1 + a1) ** 2, 1 / (1 + a1)])
    gradient_tau = np.array([d_tau_d_a1])
    sub = covariance[:2, :2]
    var_gain = float(gradient_gain @ sub @ gradient_gain)
    var_tau = float(gradient_tau[0] ** 2 * covariance[0, 0])
    return dict(gain=gain, tauSeconds=tau, delaySeconds=(nk - 1) * ts,
                standardErrors=dict(gain=math.sqrt(max(var_gain, 0.0)), tauSeconds=math.sqrt(max(var_tau, 0.0)), delaySeconds=0.0))


def identify(payload):
    keys = {"training", "validation", "mvTolerance", "maximumRmse", "loop"}
    if not isinstance(payload, dict) or payload.keys() != keys:
        raise ValueError("arx_mapping")
    if payload["loop"] != "open":
        raise ValueError("arx_closed_loop")                 # feedback biases ARX: use the closed-loop identification
    mv_tol, limit = payload["mvTolerance"], payload["maximumRmse"]
    if any(type(v) not in (int, float) or not 0 < v <= MAXIMUM_ABSOLUTE for v in (mv_tol, limit)):
        raise ValueError("arx_mapping")
    ts_t, u_t, y_t = read_series(payload["training"], mv_tol)
    ts_v, u_v, y_v = read_series(payload["validation"], mv_tol)
    if abs(ts_t - ts_v) > UNIFORM_SAMPLING_TOLERANCE * ts_t:
        raise ValueError("arx_sampling")
    candidates, best = [], None
    for na, nb in DEFAULT_ORDERS:
        parameters = na + nb + 1
        # Search heuristic only (acceptance is decided by the validation criteria below): the delay in samples is located by the
        # linear one-step fit on the training series; the few best delays and their neighbours are then judged in full.
        scan = []
        for nk in range(1, MAXIMUM_DELAY_SAMPLES + 1):
            if len(y_t) < MINIMUM_SAMPLES_PER_PARAMETER * parameters + nk + nb or len(y_v) < 20 + nk + nb:
                break
            try:
                scan.append((fit(u_t, y_t, na, nb, nk)[2], nk))
            except ValueError:
                continue
        keep = sorted({k for _, nk in sorted(scan)[:3] for k in (nk - 1, nk, nk + 1) if 1 <= k <= MAXIMUM_DELAY_SAMPLES})
        for nk in keep:
            try:
                theta, covariance, sigma2 = fit(u_t, y_t, na, nb, nk)
            except ValueError:
                continue
            estimates = [("ARX", theta, covariance, sigma2)]
            refined = output_error_fit(u_t, y_t, na, nb, nk, theta)
            if refined is not None:
                estimates.append(("OE", refined[0], refined[1], refined[2]))
            judged = []
            for estimator, th, cov, var in estimates:
                check = evaluate(th, na, nb, nk, u_v, y_v, limit, estimator)
                judged.append((estimator, th, cov, var, check))
            # the output-error estimate is preferred when it passes (unbiased under measurement noise), otherwise ARX
            passing = [j for j in judged if not j[4]["reasons"]]
            estimator, th, cov, var, check = (sorted(passing, key=lambda j: j[0] != "OE")[0] if passing else min(judged, key=lambda j: j[4]["simulationRmse"] if j[4]["simulationRmse"] is not None else math.inf))
            entry = dict(na=na, nb=nb, nk=nk, parameters=parameters, estimator=estimator, a=th[:na].tolist(), b=th[na:na + nb].tolist(), offset=float(th[-1]),
                         trainingResidualVariance=var, validationSimulationRmse=check["simulationRmse"],
                         residualLagOne=check["residualLagOne"], inputCorrelation=check["inputCorrelation"],
                         poles=[complex(p).real if abs(complex(p).imag) < 1e-12 else [complex(p).real, complex(p).imag] for p in check["poles"]],
                         reasons=check["reasons"], accepted=not check["reasons"])
            candidates.append(entry)
            if entry["accepted"]:
                key = (parameters, entry["validationSimulationRmse"])
                if best is None or key < best[0]:
                    best = (key, entry, th, cov)
    if not candidates:
        raise ValueError("arx_data")
    result = dict(algorithmVersion=VERSION, samplePeriodSeconds=ts_t, loop="open", candidates=sorted(candidates, key=lambda c: (c["parameters"], c["nk"]))[:80])
    if best is None:
        result.update(selected=None, decision="refused", reasons=sorted({r for c in candidates for r in c["reasons"]}),
                      actions=["Prévoir un essai de validation plus informatif (excitation riche : échelons de signes opposés, séquence pseudo-aléatoire).",
                               "Vérifier que l'essai est bien en boucle ouverte et sans saturation.", "Ou accepter un modèle moins exigeant en connaissance de cause."])
        return result
    _, entry, theta, covariance = best
    selected = dict(entry)
    if entry["na"] == 1 and entry["nb"] == 1:
        selected["firstOrder"] = first_order_equivalent(theta, covariance, ts_t, entry["nk"], horizon=ts_t * len(y_t))
    selected["standardErrors"] = np.sqrt(np.maximum(np.diag(covariance), 0.0)).tolist()
    result.update(selected=selected, decision="accepted")
    return result
