"""Compare saved simulation results using explicitly selected minimization criteria."""


METRICS = {"iae", "ise", "itae", "saturationSeconds", "rateLimitedSeconds", "mvTravel"}


def compare(payload):
    if payload.keys() != {"criteria", "runs"}:
        raise ValueError("comparison_input")
    criteria, runs = payload["criteria"], payload["runs"]
    if (not isinstance(criteria, list) or any(not isinstance(key, str) or key not in METRICS for key in criteria)
            or len(criteria) != len(set(criteria)) or not isinstance(runs, list) or len(runs) < 2):
        raise ValueError("comparison_input")
    summaries = []
    names = set()
    reference = None
    for run in runs:
        if not isinstance(run, dict) or not isinstance(run.get("name"), str) or not run["name"].strip():
            raise ValueError("comparison_input")
        name = run["name"]
        if name in names:
            raise ValueError("comparison_duplicate")
        names.add(name)
        issues = []
        scenario = run.get("scenario")
        if not isinstance(scenario, dict) or not scenario:
            issues.append("missing_scenario")
        elif reference is None:
            reference = scenario
        elif scenario != reference:
            issues.append("different_scenario")
        if not isinstance(run.get("parameters"), dict) or not run["parameters"]:
            issues.append("missing_parameters")
        if run.get("valid") is not True:
            issues.append("validity_unconfirmed")
        result = run.get("result")
        values = {}
        for key in criteria:
            value = result.get(key) if isinstance(result, dict) else None
            if type(value) not in (float, int) or not 0 <= value <= 1e308:
                issues.append("missing_or_invalid_" + key)
            else:
                values[key] = value
        summaries.append(dict(name=name, parameters=run.get("parameters"), metrics=values, issues=issues))

    decision, preferred = "missing_criteria", None
    if criteria and any(row["issues"] for row in summaries):
        decision = "insufficient_evidence"
    elif criteria:
        def dominates(left, right):
            pairs = [(left["metrics"][key], right["metrics"][key]) for key in criteria]
            return all(a <= b for a, b in pairs) and any(a < b for a, b in pairs)

        frontier = [row for row in summaries if not any(dominates(other, row) for other in summaries)]
        if len(frontier) == 1:
            preferred, decision = frontier[0]["name"], "dominates_on_selected_criteria"
        elif all(row["metrics"] == summaries[0]["metrics"] for row in summaries):
            decision = "equal"
        else:
            decision = "no_unique_preference"
    return dict(algorithmVersion="1.0.0", criteria=criteria, decision=decision, preferred=preferred, runs=summaries)
