"""Single acceptance rule for an identified model on its independent validation trial.

Used by the FOPDT/SOPDT comparison and by `assess_model` for every family, so that no
tuning can start from a model whose holdout prediction fails the declared criteria.
"""
import math

VERSION = "1.0.0"
# Estimation warnings that make the parameters unusable for tuning.
BLOCKING_WARNINGS = {
    "parameter_bound", "short_horizon", "fast_pole_resolution", "ill_conditioned",
    "integrator_validation_tail",
}


def read_criteria(payload):
    limits = ("maximumRmse", "maximumResidualLagOne")
    for key in limits:
        value = payload.get(key)
        if type(value) not in (float, int) or not 0 < value <= 1e100:
            raise ValueError("fit_comparison_limits")
    if payload["maximumResidualLagOne"] > 1:
        raise ValueError("fit_comparison_limits")
    return {key: payload[key] for key in limits}


def reasons(report, criteria):
    validation = report["validation"]
    rmse = validation["rmse"]
    lag = validation["residualLagOne"]
    found = []
    if not math.isfinite(rmse) or rmse > criteria["maximumRmse"]:
        found.append("validation_rmse")
    # Residual correlation at floating-point precision is uninterpretable.
    scale = max(abs(point["observed"]) for point in validation["points"])
    if rmse > 32 * math.ulp(scale) and (lag is None or abs(lag) > criteria["maximumResidualLagOne"]):
        found.append("validation_residuals")
    found.extend(w for w in report["warnings"] if w in BLOCKING_WARNINGS)
    return found


def assessment(report, criteria):
    found = reasons(report, criteria)
    return dict(algorithmVersion=VERSION, accepted=not found, reasons=found, criteria=criteria)


FAMILIES = ("FOPDT", "SOPDT", "Integrateur", "BroidaFOPDT")


def assess(payload):
    if not isinstance(payload, dict) or payload.keys() != {"family", "trials", "maximumRmse", "maximumResidualLagOne"}:
        raise ValueError("fit_comparison_mapping")
    if payload["family"] not in FAMILIES or not isinstance(payload["trials"], dict):
        raise ValueError("fit_mapping")
    criteria = read_criteria(payload)
    if payload["family"] == "FOPDT":
        from fopdt import identify
    elif payload["family"] == "SOPDT":
        from sopdt import identify
    elif payload["family"] == "Integrateur":
        from integrator import identify
    else:
        from broida import identify
    report = identify(payload["trials"])
    return dict(report=report, assessment=assessment(report, criteria))
