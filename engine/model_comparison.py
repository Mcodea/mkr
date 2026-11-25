"""Compare candidate models on the same independent validation trial."""
from validation_gate import read_criteria, reasons, significantly_better
from fopdt import identify as fit_first_order
from sopdt import identify as fit_second_order


def compare(payload):
    limits = ("maximumRmse", "maximumResidualLagOne")
    if not isinstance(payload, dict) or payload.keys() != {"trials", *limits}:
        raise ValueError("fit_comparison_mapping")
    if not isinstance(payload["trials"], dict):
        raise ValueError("fit_mapping")
    criteria = read_criteria(payload)

    candidates, reports = {}, {}
    for name, fit in (("FOPDT", fit_first_order), ("SOPDT", fit_second_order)):
        report = reports[name] = fit(payload["trials"])
        validation = report["validation"]
        candidates[name] = dict(validationRmse=validation["rmse"], validationResidualLagOne=validation["residualLagOne"],
                                samples=validation["samples"], warnings=report["warnings"],
                                rejectionReasons=reasons(report, criteria), fitVersion=report["algorithmVersion"])
    # Lowest acceptable order, unless the second order predicts the holdout significantly better
    # (audit NC-04: a first-order model passing lax thresholds must not hide unmodelled dynamics).
    accepted = [name for name, entry in candidates.items() if not entry["rejectionReasons"]]
    better, test = significantly_better(reports["FOPDT"], reports["SOPDT"])
    if accepted == ["FOPDT", "SOPDT"] and better:
        selected, decision = "SOPDT", "significantly_better_higher_order"
    elif accepted:
        selected, decision = accepted[0], "lowest_acceptable_order"
    else:
        selected, decision = None, "insufficient_evidence"
    return dict(algorithmVersion="1.1.0", selected=selected, candidates=candidates, criteria=criteria,
                decision=decision, holdoutTest=test)
