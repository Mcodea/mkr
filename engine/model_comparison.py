"""Compare candidate models on the same independent validation trial."""
import math

from fopdt import identify as fit_first_order
from sopdt import identify as fit_second_order


def compare(payload):
    limits = ("maximumRmse", "maximumResidualLagOne")
    if not isinstance(payload, dict) or payload.keys() != {"trials", *limits}:
        raise ValueError("fit_comparison_mapping")
    if not isinstance(payload["trials"], dict):
        raise ValueError("fit_mapping")
    for key in limits:
        value = payload[key]
        if type(value) not in (float, int) or not 0 < value <= 1e100:
            raise ValueError("fit_comparison_limits")
    if payload["maximumResidualLagOne"] > 1:
        raise ValueError("fit_comparison_limits")

    candidates = {}
    for name, fit in (("FOPDT", fit_first_order), ("SOPDT", fit_second_order)):
        report = fit(payload["trials"])
        validation = report["validation"]
        rmse = validation["rmse"]
        lag = validation["residualLagOne"]
        reasons = []
        if rmse > payload["maximumRmse"] or not math.isfinite(rmse):
            reasons.append("validation_rmse")
        # Residual correlation at floating-point precision is uninterpretable.
        scale = max(abs(point["observed"]) for point in validation["points"])
        if rmse > 32 * math.ulp(scale) and (lag is None or abs(lag) > payload["maximumResidualLagOne"]):
            reasons.append("validation_residuals")
        reasons.extend(w for w in report["warnings"] if w in {
            "parameter_bound", "short_horizon", "fast_pole_resolution", "ill_conditioned",
        })
        candidates[name] = dict(validationRmse=rmse, validationResidualLagOne=lag,
                                samples=validation["samples"], warnings=report["warnings"],
                                rejectionReasons=reasons, fitVersion=report["algorithmVersion"])
    # Choose the lowest order satisfying the declared validation criteria.
    selected = next((name for name, entry in candidates.items() if not entry["rejectionReasons"]), None)
    return dict(algorithmVersion="1.0.0", selected=selected, candidates=candidates,
                criteria={key: payload[key] for key in limits},
                decision="lowest_acceptable_order" if selected else "insufficient_evidence")
