"""Compare candidate models on the same independent validation trial."""
from validation_gate import read_criteria, reasons
from fopdt import identify as fit_first_order
from sopdt import identify as fit_second_order


def compare(payload):
    limits = ("maximumRmse", "maximumResidualLagOne")
    if not isinstance(payload, dict) or payload.keys() != {"trials", *limits}:
        raise ValueError("fit_comparison_mapping")
    if not isinstance(payload["trials"], dict):
        raise ValueError("fit_mapping")
    criteria = read_criteria(payload)

    candidates = {}
    for name, fit in (("FOPDT", fit_first_order), ("SOPDT", fit_second_order)):
        report = fit(payload["trials"])
        validation = report["validation"]
        candidates[name] = dict(validationRmse=validation["rmse"], validationResidualLagOne=validation["residualLagOne"],
                                samples=validation["samples"], warnings=report["warnings"],
                                rejectionReasons=reasons(report, criteria), fitVersion=report["algorithmVersion"])
    # Choose the lowest order satisfying the declared validation criteria.
    selected = next((name for name, entry in candidates.items() if not entry["rejectionReasons"]), None)
    return dict(algorithmVersion="1.0.0", selected=selected, candidates=candidates, criteria=criteria,
                decision="lowest_acceptable_order" if selected else "insufficient_evidence")
