"""Theoretical PI settings for FOPDT models, per MKR-SPEC-001 §8."""
import math

from stability_margins import analyze


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

    def with_margins(ti, proportional=None):
        # Nominal-model margins of the loop this tuning closes on the identified FOPDT (audit NC-17).
        try:
            return analyze(gain, [tau], delay, kp if proportional is None else proportional, ti, 0.0)
        except ValueError:
            return None

    # Published IMC-PI (Rivera, Morari and Skogestad 1986): IMC filter 1/(lambda s + 1) with a first-order Pade approximation of the delay,
    # lambda = response. Kc = (tau + theta/2) / (K (lambda + theta/2)), Ti = tau + theta/2. Not the Lambda rule above (Ti = tau, no theta/2).
    kp_imc = (tau + delay / 2) / ((response + delay / 2) * gain)
    ti_imc = tau + delay / 2
    return dict(algorithmVersion="1.2.0", responseSeconds=response, warnings=warnings,
                imcPublished=dict(kp=kp_imc, tiSeconds=ti_imc, parallelKi=kp_imc / ti_imc, margins=with_margins(ti_imc, kp_imc),
                                  reference="Rivera, Morari & Skogestad, Ind. Eng. Chem. Process Des. Dev. 25 (1986) 252-265"),
                imc=dict(kp=kp, tiSeconds=tau, parallelKi=kp / tau, margins=with_margins(tau)),
                simc=dict(kp=kp, tiSeconds=ti_simc, parallelKi=kp / ti_simc, margins=with_margins(ti_simc)))
