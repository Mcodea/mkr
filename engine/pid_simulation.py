"""Exploratory sampled filtered PID on a deviation-form FOPDT process.

Independent prototype; no automatic PID tuning or vendor block equivalence.
P and I act on error, D acts only on measured PV with backward-Euler filtering.
Output clamps and rate constraints follow the existing MKR PI simulation convention.
"""
import math

from fopdt_simulation import grid_index, mv_increment, number, plant_step, second_order_step
from pi_simulation import response_metrics


def calculate(payload):
    required = {
        "gain", "tauSeconds", "delaySeconds", "controller", "sampleSeconds",
        "durationSeconds", "setpoint", "minimumMv", "maximumMv",
        "disturbance", "disturbanceAtSeconds",
    }
    if not isinstance(payload, dict) or payload.keys() - {"maximumMvRate", "measurementNoise", "secondTauSeconds"} != required:
        raise ValueError("pid_input")
    gain, tau, delay, h, duration, sp, lower, upper, disturbance, event = (
        number(payload[key]) for key in (
            "gain", "tauSeconds", "delaySeconds", "sampleSeconds",
            "durationSeconds", "setpoint", "minimumMv", "maximumMv",
            "disturbance", "disturbanceAtSeconds"
        )
    )
    if (gain == 0 or tau <= 0 or delay < 0 or h <= 0 or duration <= 0 or
            not lower < 0 < upper or not 0 <= event <= duration):
        raise ValueError("pid_domain")
    controller = payload["controller"]
    if not isinstance(controller, dict) or controller.keys() != {
        "kp", "tiSeconds", "tdSeconds", "filterSeconds"
    }:
        raise ValueError("pid_input")
    kp, ti, td, tf = (
        number(controller[key]) for key in
        ("kp", "tiSeconds", "tdSeconds", "filterSeconds")
    )
    # Td=0 provides an exact comparison with existing PI controller, Tf=0 then valid.
    if (kp == 0 or math.copysign(1, kp) != math.copysign(1, gain) or
            ti <= 0 or td < 0 or tf < 0 or (td > 0 and tf <= 0)):
        raise ValueError("pid_domain")

    rate_step = mv_increment(payload, h)
    n, event_index = grid_index(duration, h), grid_index(event, h)
    if not 2 <= n <= 5000:
        raise ValueError("pid_grid")
    if "secondTauSeconds" in payload:
        fast = number(payload["secondTauSeconds"])
        advance = second_order_step(gain, tau, fast, delay, h, duration)
    else:
        advance = plant_step(gain, tau, delay, h, duration)
    # Sensor noise is a deterministic input, never an implicit random draw.
    noisy = "measurementNoise" in payload
    if noisy:
        values = payload["measurementNoise"]
        if not isinstance(values, list) or len(values) != n:
            raise ValueError("pid_noise")
        try:
            noise = [number(value) for value in values]
        except ValueError as error:
            raise ValueError("pid_noise") from error
    else:
        noise = None

    pv = integral = derivative = previous_pv = previous_mv = 0.0
    iae = ise = saturated = rate_limited = 0.0
    frozen = 0
    us, points = [], []
    decay = tf / (tf + h) if td > 0 else 0.0
    # Backward Euler for D(s)=-Kp*Td*s/(Tf*s+1) on PV.
    pv_change_gain = kp * td / (tf + h) if td > 0 else 0.0

    for k in range(n):
        measured_pv = pv + (noise[k] if noise is not None else 0.0)
        if not math.isfinite(measured_pv):
            raise ValueError("pid_range")
        error = sp - measured_pv
        derivative = decay * derivative - pv_change_gain * (measured_pv - previous_pv)
        previous_pv = measured_pv
        raw = kp * error + integral + derivative
        if not math.isfinite(raw):
            raise ValueError("pid_range")
        bounded = max(lower, min(upper, raw))
        effective_lower = max(lower, previous_mv - rate_step)
        effective_upper = min(upper, previous_mv + rate_step)
        mv = max(effective_lower, min(effective_upper, bounded))
        previous_mv = mv

        step_integral = kp * h / ti * error
        if not math.isfinite(step_integral):
            raise ValueError("pid_range")
        # Conditional integration at actuator bounds incl. rate limit;
        # consistent with MKR's PI engine, not a vendor's proprietary anti-windup.
        if (raw >= effective_upper and step_integral > 0) or (
                raw <= effective_lower and step_integral < 0):
            frozen += 1
        else:
            integral += step_integral

        applied_disturbance = disturbance if k >= event_index else 0.0
        us.append(mv + applied_disturbance)
        points.append(dict(
            seconds=k * h, pv=pv, cv=raw, mv=mv,
            disturbance=applied_disturbance, derivative=derivative,
            measuredPv=measured_pv
        ))
        # Tracking performance reflects actual process PV, not a noisy sensor.
        physical_error = sp - pv
        iae += abs(physical_error) * h
        ise += physical_error * physical_error * h
        saturated += h if raw != bounded else 0.0
        rate_limited += h if bounded != mv else 0.0
        pv = advance(pv, us)
        if not math.isfinite(pv):
            raise ValueError("pid_range")

    points.append(dict(
        seconds=duration, pv=pv, cv=None, mv=None,
        disturbance=None, derivative=None, measuredPv=None
    ))
    response_end = event_index if disturbance != 0 else n
    warnings = ["exploratory_pid", "derivative_on_measurement",
                "backward_euler_filter", "conditional_integration"]
    if noisy:
        warnings.append("deterministic_measurement_noise")
    if h > tau / 10:
        warnings.append("coarse_sample")
    if td > 0 and h > tf:
        warnings.append("coarse_derivative_filter")
    if delay >= duration:
        warnings.append("delay_beyond_horizon")

    return dict(
        algorithmVersion="0.2.0",
        controllerForm="ideal_dependent_pid_derivative_on_pv",
        warnings=warnings,
        iae=iae, ise=ise, finalError=sp-pv,
        saturationSeconds=saturated, rateLimitedSeconds=rate_limited,
        frozenSteps=frozen, points=points,
        setpointResponse=response_metrics(points[:response_end + 1], sp)
    )
