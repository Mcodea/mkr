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

VERSION = "0.2.0"
SIGNIFICANCE_MULTIPLE = 3.0
# Below these relative changes a parameter is not worth acting on. The fast pole of a second-order model is the least determined
# parameter of its family, hence the wider band.
RELATIVE_CHANGE = dict(gain=0.10, tauSeconds=0.15, secondTauSeconds=0.30, delaySeconds=0.25)
FAMILIES = {
    "FOPDT": ("gain", "tauSeconds", "delaySeconds"),
    "SOPDT": ("gain", "tauSeconds", "secondTauSeconds", "delaySeconds"),
    "Integrateur": ("gain", "delaySeconds"),
}


def _model(family, model):
    names = FAMILIES[family]
    if not isinstance(model, dict) or model.keys() != set(names):
        raise ValueError("drift_mapping")
    values = [model[k] for k in names]
    if any(type(v) not in (int, float) or not math.isfinite(v) for v in values):
        raise ValueError("drift_mapping")
    model = dict(zip(names, (float(v) for v in values)))
    if (model["gain"] == 0 or model["delaySeconds"] < 0 or any(model[n] <= 0 for n in names if n.endswith("Seconds") and n != "delaySeconds")
            or ("secondTauSeconds" in model and model["secondTauSeconds"] > model["tauSeconds"])):
        raise ValueError("drift_domain")
    return model


def _predict(family, step, model):
    if family == "FOPDT":
        return basis(step.times, model["tauSeconds"], model["delaySeconds"])
    if family == "SOPDT":
        from sopdt import basis as second
        return second(step.times, model["tauSeconds"], model["secondTauSeconds"], model["delaySeconds"])
    return np.maximum(step.times - model["delaySeconds"], 0)


def _refit(family, step):
    """Re-estimation of the new trial alone: (parameters, standard errors or None, warnings)."""
    if family == "FOPDT":
        gain, tau, delay, *_ = estimate(step)
        warnings = list(delay_warnings(delay, step))
        fit = diagnostics(step, gain, basis(step.times, tau, delay))
        errors = conditional_errors(step, gain, tau, delay, fit, warnings)
        return dict(gain=gain, tauSeconds=tau, delaySeconds=delay), errors, warnings
    if family == "SOPDT":
        from sopdt import estimate as fit_second
        gain, slow, fast, delay, *_ = fit_second(step)
        return dict(gain=gain, tauSeconds=slow, secondTauSeconds=fast, delaySeconds=delay), None, list(delay_warnings(delay, step))
    from integrator import estimate as fit_integrator
    gain, delay, _ = fit_integrator(step)
    return dict(gain=gain, delaySeconds=delay), None, list(delay_warnings(delay, step))


def check(payload):
    required = {"family", "model", "trial", "mvTolerance", "pvTolerance", "maximumRmse", "maximumResidualLagOne"}
    if not isinstance(payload, dict) or payload.keys() != required or payload["family"] not in FAMILIES:
        raise ValueError("drift_mapping")
    family = payload["family"]
    old = _model(family, payload["model"])
    tolerances = [payload["mvTolerance"], payload["pvTolerance"]]
    if any(type(v) not in (int, float) or not 1e-100 <= v <= 1e100 for v in tolerances):
        raise ValueError("fit_tolerance")
    criteria = {k: payload[k] for k in ("maximumRmse", "maximumResidualLagOne")}
    if any(type(v) not in (int, float) or not 0 < v <= 1e100 for v in criteria.values()):
        raise ValueError("fit_comparison_limits")
    step = read_step(payload["trial"], *tolerances)
    prediction = diagnostics(step, old["gain"], _predict(family, step, old))
    found = reasons(dict(validation=prediction, warnings=[]), criteria)

    new, errors, warnings = _refit(family, step)
    changes = {}
    for name in old:
        relative = (new[name] - old[name]) / abs(old[name])
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
    return dict(algorithmVersion=VERSION, family=family, verdict=verdict, reasons=found, driftedParameters=drifted,
                predictionRmse=prediction["rmse"], residualLagOne=prediction["residualLagOne"], baselineNoise=step.baseline_noise,
                changes=changes, warnings=warnings, criteria=criteria)
