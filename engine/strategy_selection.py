"""Select a generic feedback strategy from comparable simulated candidates."""
from math import isfinite

from pid_simulation import calculate
from tuning_comparison import compare


def select(payload):
    required = {"studyId", "modelRunId", "scenario", "controllers", "criteria", "maximumAbsoluteFinalError"}
    if payload.keys() != required or any(not isinstance(payload[k], str) or not payload[k].strip()
                                       for k in ("studyId", "modelRunId")):
        raise ValueError("strategy_input")
    scenario, controllers = payload["scenario"], payload["controllers"]
    if not isinstance(scenario, dict) or "controller" in scenario or not isinstance(controllers, dict):
        raise ValueError("strategy_input")
    if len(controllers) < 2 or controllers.keys() - {"P", "PI", "PID"}:
        raise ValueError("strategy_candidates")
    error_limit = payload["maximumAbsoluteFinalError"]
    if type(error_limit) not in (int, float) or not 0 <= error_limit <= 1e100:
        raise ValueError("strategy_objective")
    criteria = payload["criteria"]
    runs, evidence = [], []
    for name, settings in controllers.items():
        if not isinstance(settings, dict):
            raise ValueError("strategy_controller")
        if name == "P":
            if settings.keys() != {"kind", "kp"} or settings["kind"] != "P":
                raise ValueError("strategy_controller")
        elif settings.keys() != {"kp", "tiSeconds", "tdSeconds", "filterSeconds"}:
            raise ValueError("strategy_controller")
        elif type(settings["tdSeconds"]) not in (int, float) or (name == "PI") != (settings["tdSeconds"] == 0):
            raise ValueError("strategy_controller")
        result = calculate({**scenario, "controller": settings})
        error = abs(result["finalError"])
        issues = []
        if error > error_limit or not isfinite(error):
            issues.append("final_error_exceeds_limit")
        if name == "PID" and "measurementNoise" not in scenario:
            issues.append("derivative_noise_response_unassessed")
        issues.extend(w for w in result["warnings"] if w in {
            "coarse_sample", "coarse_derivative_filter", "delay_beyond_horizon"
        })
        evidence.append(dict(strategy=name, absoluteFinalError=error, reasons=issues,
                             metrics={key: result[key] for key in ("iae", "ise", "itae", "mvTravel", "saturationSeconds", "rateLimitedSeconds")}))
        runs.append(dict(name=name, scenario=scenario, parameters=settings, valid=not issues, result=result))
    comparison = compare(dict(criteria=criteria, runs=runs))
    return dict(algorithmVersion="1.0.0", studyId=payload["studyId"], modelRunId=payload["modelRunId"],
                selected=comparison["preferred"], decision=comparison["decision"],
                evidence=evidence, comparison=comparison, maximumAbsoluteFinalError=error_limit,
                qualification="simulation_only")
