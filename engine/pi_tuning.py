"""Theoretical PI settings for FOPDT models, per MKR-SPEC-001 §8."""
import math


def calculate(payload):
    if payload.keys() != {"gain", "tauSeconds", "delaySeconds", "responseSeconds"}:
        raise ValueError("pi_input")
    gain, tau, delay, response = (payload[key] for key in
                                  ("gain", "tauSeconds", "delaySeconds", "responseSeconds"))
    if any(type(x) not in (int, float) or not -1e100 <= x <= 1e100 for x in (gain, tau, delay, response)):
        raise ValueError("pi_input")
    if gain == 0 or tau <= 0 or delay < 0 or response <= 0:
        raise ValueError("pi_domain")
    effective = response + delay
    kp = (tau / effective) / gain
    ti_simc = min(tau, 4 * effective)
    if not all(math.isfinite(x) and x != 0 for x in (kp, kp / tau, kp / ti_simc)):
        raise ValueError("pi_range")

    warnings = ["exploratory_model", "actuator_limits_unassessed"]
    if response < delay:
        warnings.append("tight_response")
    return dict(algorithmVersion="1.0.0", responseSeconds=response, warnings=warnings,
                imc=dict(kp=kp, tiSeconds=tau, parallelKi=kp / tau),
                simc=dict(kp=kp, tiSeconds=ti_simc, parallelKi=kp / ti_simc))
