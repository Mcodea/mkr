"""Single acceptance rule for an identified model on its independent validation trial.

Used by the FOPDT/SOPDT comparison and by `assess_model` for every family, so that no
tuning can start from a model whose holdout prediction fails the declared criteria.
"""
import math

from scipy.stats import f as fisher

from fopdt import is_round_off

VERSION = "1.1.0"
# Default acceptance criteria are derived from the measured pre-step noise only (never from a fit),
# so that thresholds are fixed before the model is seen.
NOISE_MULTIPLE = 2.0          # RMSE of a correct model on white noise is close to sigma.
DEFAULT_MAXIMUM_LAG = 0.5     # Lag-one residual correlation tolerated for a structurally adequate model.
SIGNIFICANCE = 0.99           # Holdout F-test level for preferring the second-order model.
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
    if not is_round_off(rmse, scale) and (lag is None or abs(lag) > criteria["maximumResidualLagOne"]):
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


def suggest(payload):
    """Proposes criteria from the noise measured on both pre-step plateaus and the declared PV tolerance."""
    from fopdt import read_trials
    training, validation = read_trials(payload)
    sigma = math.sqrt((training.baseline_noise ** 2 + validation.baseline_noise ** 2) / 2)
    tolerance = payload["pvTolerance"]
    maximum_rmse = max(NOISE_MULTIPLE * sigma, tolerance / 2)
    return dict(algorithmVersion=VERSION, baselineNoise=sigma, maximumRmse=maximum_rmse,
                maximumResidualLagOne=DEFAULT_MAXIMUM_LAG,
                basis="noise" if NOISE_MULTIPLE * sigma >= tolerance / 2 else "pv_tolerance",
                noiseMultiple=NOISE_MULTIPLE, trainingNoise=training.baseline_noise,
                validationNoise=validation.baseline_noise)


def significantly_better(first, second):
    """Holdout F-test: does the second model reduce the validation mean square error significantly?

    Both models predict the same validation samples. Under equal adequacy the MSE ratio follows
    approximately F(n, n). Round-off level errors are uninterpretable and never decide.
    """
    a, b = first["validation"], second["validation"]
    scale = max(abs(point["observed"]) for point in a["points"])
    if is_round_off(a["rmse"], scale) or b["rmse"] <= 0:
        return False, None
    ratio = (a["rmse"] / b["rmse"]) ** 2
    critical = float(fisher.ppf(SIGNIFICANCE, a["samples"], b["samples"]))
    return ratio > critical, dict(mseRatio=ratio, critical=critical, level=SIGNIFICANCE)
