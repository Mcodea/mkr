"""Deterministic one-factor-at-a-time PI sensitivity around a saved FOPDT model.

No statistical confidence or guaranteed stability claims. The PI coefficients are held
fixed while each plant parameter is independently varied by the declared percentage.
"""
import math

from fopdt_simulation import number
from pi_simulation import calculate

VERSION = "1.0.0"
CASES = (
    ("nominal", None, 0),
    ("gain_minus", "gain", -1),
    ("gain_plus", "gain", 1),
    ("tau_minus", "tauSeconds", -1),
    ("tau_plus", "tauSeconds", 1),
    ("delay_minus", "delaySeconds", -1),
    ("delay_plus", "delaySeconds", 1),
)


def calculate_sensitivity(payload):
    expected = {"gain", "tauSeconds", "delaySeconds", "imc", "simc",
                "sampleSeconds", "durationSeconds", "setpoint", "minimumMv",
                "maximumMv", "disturbance", "disturbanceAtSeconds",
                "maximumMvRate", "variationPercent"}
    if not isinstance(payload, dict) or payload.keys() != expected:
        raise ValueError("robust_input")
    variation = number(payload["variationPercent"])
    if variation <= 0 or variation > 50:
        raise ValueError("robust_domain")
    base = {key: value for key, value in payload.items() if key != "variationPercent"}
    # Reuse the exact validated PI simulation to check the nominal domain.
    nominal = calculate(base)
    results = []
    for name, parameter, direction in CASES:
        candidate = base.copy()
        if parameter is not None:
            candidate[parameter] = base[parameter] * (1 + direction * variation / 100)
        if name == "nominal":
            run = nominal
        else:
            run = calculate(candidate)

        def metrics(method):
            curve = run[method]
            return dict(
                iae=curve["iae"],
                ise=curve["ise"],
                finalError=curve["finalError"],
                saturationSeconds=curve["saturationSeconds"],
                rateLimitedSeconds=curve["rateLimitedSeconds"],
                frozenSteps=curve["frozenSteps"],
                overshootPv=curve["setpointResponse"]["overshootPv"],
                settlingSeconds=curve["setpointResponse"]["settlingSeconds"],
            )

        results.append(dict(
            key=name,
            gain=candidate["gain"],
            tauSeconds=candidate["tauSeconds"],
            delaySeconds=candidate["delaySeconds"],
            imc=metrics("imc"),
            simc=metrics("simc"),
            warnings=run["warnings"],
        ))

    return dict(
        algorithmVersion=VERSION,
        variationPercent=variation,
        cases=results,
        warnings=["local_one_factor_sensitivity", "fixed_pi_coefficients",
                  "nominal_model_only", "not_a_stability_proof"],
    )
