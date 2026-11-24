"""Does a previously validated FOPDT model still describe the process? (loop monitoring after commissioning)

A NEW open-loop step trial (same reading rules as the identification: clean MV step, flat baseline) is predicted with the
stored model. The verdict reuses the acceptance rule of the validation gate (prediction error against the measured noise, residual
correlation). The trial is also re-estimated alone, and each parameter's change is judged against its own standard error:
a change is flagged only if it is both statistically significant (more than three standard errors, when they are known) and larger
than a stated relative threshold, so that noise alone does not raise an alarm and a statistically perfect but tiny change does not either.

It decides nothing about the controller: it says whether the model is still consistent with new data, and where it moved.
"""
import math

import numpy as np

from fopdt import basis, conditional_errors, diagnostics, delay_warnings, estimate, read_step
from validation_gate import reasons

VERSION = "0.1.0"
SIGNIFICANCE_MULTIPLE = 3.0
RELATIVE_CHANGE = dict(gain=0.10, tauSeconds=0.15, delaySeconds=0.25)   # below these, a change is not worth acting on


def _model(payload):
    model = payload.get("model") if isinstance(payload, dict) else None
    if not isinstance(model, dict) or model.keys() != {"gain", "tauSeconds", "delaySeconds"}:
        raise ValueError("drift_mapping")
    values = [model[k] for k in ("gain", "tauSeconds", "delaySeconds")]
    if any(type(v) not in (int, float) or not math.isfinite(v) for v in values):
        raise ValueError("drift_mapping")
    gain, tau, delay = values
    if gain == 0 or tau <= 0 or delay < 0:
        raise ValueError("drift_domain")
    return gain, tau, delay


def check(payload):
    required = {"model", "trial", "mvTolerance", "pvTolerance", "maximumRmse", "maximumResidualLagOne"}
    if not isinstance(payload, dict) or payload.keys() != required:
        raise ValueError("drift_mapping")
    gain, tau, delay = _model(payload)
    tolerances = [payload["mvTolerance"], payload["pvTolerance"]]
    if any(type(v) not in (int, float) or not 1e-100 <= v <= 1e100 for v in tolerances):
        raise ValueError("fit_tolerance")
    criteria = {k: payload[k] for k in ("maximumRmse", "maximumResidualLagOne")}
    if any(type(v) not in (int, float) or not 0 < v <= 1e100 for v in criteria.values()):
        raise ValueError("fit_comparison_limits")
    step = read_step(payload["trial"], *tolerances)
    warnings = []
    prediction = diagnostics(step, gain, basis(step.times, tau, delay))
    report = dict(validation=prediction, warnings=[])
    found = reasons(report, criteria)

    new_gain, new_tau, new_delay, *_ = estimate(step)
    new_warnings = list(delay_warnings(new_delay, step))
    refit = diagnostics(step, new_gain, basis(step.times, new_tau, new_delay))
    errors = conditional_errors(step, new_gain, new_tau, new_delay, refit, new_warnings)
    old = dict(gain=gain, tauSeconds=tau, delaySeconds=delay)
    new = dict(gain=new_gain, tauSeconds=new_tau, delaySeconds=new_delay)
    changes = {}
    for name in old:
        relative = (new[name] - old[name]) / abs(old[name]) if old[name] else math.inf
        error = errors[name] if errors else None
        significant = error is None or abs(new[name] - old[name]) > SIGNIFICANCE_MULTIPLE * error
        changes[name] = dict(previous=old[name], current=new[name], relativeChange=relative, standardError=error,
                             flagged=bool(abs(relative) > RELATIVE_CHANGE[name] and significant))
    drifted = [n for n, c in changes.items() if c["flagged"]]
    if found and drifted:
        verdict = "drift"
    elif found:
        verdict = "inconsistent_without_parameter_change"      # the model fails but no single parameter explains it: structure or disturbance
    elif drifted:
        verdict = "parameter_changed_prediction_acceptable"
    else:
        verdict = "consistent"
    if errors is None:
        warnings.append("standard_errors_unavailable")         # the flag then rests on the relative threshold alone
    return dict(algorithmVersion=VERSION, verdict=verdict, reasons=found, driftedParameters=drifted,
                predictionRmse=prediction["rmse"], residualLagOne=prediction["residualLagOne"], baselineNoise=step.baseline_noise,
                changes=changes, warnings=warnings + new_warnings, criteria=criteria)
