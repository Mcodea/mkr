"""Continuous, unfiltered SIMC PID for a stable two-real-pole SOPDT."""
import math

from pi_tuning import calculate as calculate_pi


def calculate(payload):
    if not isinstance(payload, dict) or "secondTauSeconds" not in payload:
        raise ValueError("pid_input")
    parameters = dict(payload)
    fast = parameters.pop("secondTauSeconds")
    pi = calculate_pi(parameters)
    if type(fast) not in (int, float) or not 0 < fast <= parameters["tauSeconds"]:
        raise ValueError("pid_second_pole")
    series = pi["simc"]
    kc, integral = series["kp"], series["tiSeconds"]
    # Expand Kc (1 + 1/(Ti*s)) (1 + Td*s) and match like powers of s.
    ti = integral + fast
    kp = kc * (ti / integral)
    td = fast * (integral / ti)
    if not all(math.isfinite(v) and v != 0 for v in (kp, ti, td)):
        raise ValueError("pid_range")
    return dict(algorithmVersion="1.0.0", method="SIMC_SOPDT", timeUnit="s",
                controllerForm="continuous_ideal_unfiltered_error",
                series=dict(kc=kc, tiSeconds=integral, tdSeconds=fast),
                ideal=dict(kp=kp, tiSeconds=ti, tdSeconds=td),
                warnings=[*pi["warnings"], "derivative_filter_unspecified",
                          "vendor_equivalence_unverified"])
