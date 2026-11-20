"""Bounded joint FOPDT sensitivity: a deterministic 3 x 3 x 3 grid.

The PI coefficients and scenario are fixed. The PV envelopes are pointwise extrema
of simulated runs, NOT confidence bands, stability margins or verified safe regions.
"""
from itertools import product

from fopdt_simulation import number
from pi_simulation import calculate

VERSION = "1.0.0"
PARAMETERS = ("gain", "tauSeconds", "delaySeconds")
LEVELS = (-1, 0, 1)


def calculate_combined(payload):
    expected = {"gain", "tauSeconds", "delaySeconds", "imc", "simc",
                "sampleSeconds", "durationSeconds", "setpoint", "minimumMv",
                "maximumMv", "disturbance", "disturbanceAtSeconds",
                "maximumMvRate", "variationPercent"}
    if not isinstance(payload, dict) or payload.keys() != expected:
        raise ValueError("combined_input")
    variation = number(payload["variationPercent"])
    if not 0 < variation <= 50:
        raise ValueError("combined_domain")
    base = {key: value for key, value in payload.items() if key != "variationPercent"}
    nominal = calculate(base)
    times = [p["seconds"] for p in nominal["imc"]["points"]]
    bounds = {method: dict(minPv=[float("inf")] * len(times),
                           maxPv=[-float("inf")] * len(times))
              for method in ("imc", "simc")}
    cases = []

    # Nominal first, then a reproducible lexicographic sweep of remaining cases.
    variants = [(0, 0, 0)] + [
        levels for levels in product(LEVELS, repeat=3) if levels != (0, 0, 0)
    ]
    for levels in variants:
        candidate = base.copy()
        for name, direction in zip(PARAMETERS, levels):
            candidate[name] = base[name] * (1 + direction * variation / 100)
        run = nominal if levels == (0, 0, 0) else calculate(candidate)
        metrics = {}
        for method in ("imc", "simc"):
            curve = run[method]
            if len(curve["points"]) != len(times):
                raise ValueError("combined_grid")
            envelope = bounds[method]
            for index, point in enumerate(curve["points"]):
                if point["seconds"] != times[index]:
                    raise ValueError("combined_grid")
                envelope["minPv"][index] = min(envelope["minPv"][index], point["pv"])
                envelope["maxPv"][index] = max(envelope["maxPv"][index], point["pv"])
            metrics[method] = {
                "iae": curve["iae"], "ise": curve["ise"],
                "finalError": curve["finalError"],
                "saturationSeconds": curve["saturationSeconds"],
                "rateLimitedSeconds": curve["rateLimitedSeconds"],
                "frozenSteps": curve["frozenSteps"],
                "overshootPv": curve["setpointResponse"]["overshootPv"],
                "settlingSeconds": curve["setpointResponse"]["settlingSeconds"],
            }
        cases.append({
            "key": f"K{levels[0]:+d}_T{levels[1]:+d}_D{levels[2]:+d}",
            "gain": candidate["gain"],
            "tauSeconds": candidate["tauSeconds"],
            "delaySeconds": candidate["delaySeconds"],
            "imc": metrics["imc"], "simc": metrics["simc"],
            "warnings": run["warnings"],
        })

    return {
        "algorithmVersion": VERSION,
        "variationPercent": variation,
        "cases": cases,
        "envelope": {
            "seconds": times,
            "imc": bounds["imc"],
            "simc": bounds["simc"],
        },
        "warnings": [
            "bounded_27_case_grid", "fixed_pi_coefficients",
            "pointwise_simulation_envelope", "not_a_confidence_band",
            "not_a_stability_proof", "not_a_global_robustness_guarantee",
        ],
    }
