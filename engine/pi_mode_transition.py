"""Exploratory manual-to-automatic PI transition on an initially steady FOPDT plant.

All variables are deviations from the initial operating point. No PLC I/O.
"""
import math

from fopdt_simulation import grid_index, mv_increment, number, plant_step


def calculate(payload):
    required = {"gain", "tauSeconds", "delaySeconds", "sampleSeconds",
                "durationSeconds", "setpoint", "minimumMv", "maximumMv",
                "manualMv", "automaticAtSeconds", "kp", "tiSeconds"}
    if not isinstance(payload, dict) or set(payload) - {"maximumMvRate"} != required:
        raise ValueError("sim_input")

    gain, tau, delay, h, duration, setpoint, lower, upper, manual, switch, kp, ti = (
        number(payload[key]) for key in (
            "gain", "tauSeconds", "delaySeconds", "sampleSeconds",
            "durationSeconds", "setpoint", "minimumMv", "maximumMv",
            "manualMv", "automaticAtSeconds", "kp", "tiSeconds"
        )
    )
    if (gain == 0 or tau <= 0 or delay < 0 or h <= 0 or duration <= 0
            or not lower < 0 < upper or kp == 0 or ti <= 0
            or math.copysign(1, kp) != math.copysign(1, gain)
            or not 0 < switch < duration):
        raise ValueError("sim_domain")

    count, switch_index = grid_index(duration, h), grid_index(switch, h)
    if not 2 <= count <= 5000 or not 1 <= switch_index < count:
        raise ValueError("sim_grid")

    increment = mv_increment(payload, h)
    advance = plant_step(gain, tau, delay, h, duration)
    pv = previous_mv = integral = 0.0
    inputs, points = [], []
    saturation_steps = rate_steps = frozen_steps = 0
    iae = ise = 0.0
    transfer_bump = None

    for k in range(count):
        seconds = k * h
        error = setpoint - pv
        mode = "manual" if k < switch_index else "automatic"
        if mode == "manual":
            requested = manual
            cv = None  # A manual target is not a calculated regulator output.
        else:
            if k == switch_index:
                # Track the *applied* actuator command, not its requested value.
                # This makes the first automatic command continuous even if
                # the manual command was saturated or rate-limited.
                integral = previous_mv - kp * error
            cv = kp * error + integral
            requested = cv
            if k == switch_index:
                transfer_bump = cv - previous_mv
        if not math.isfinite(requested):
            raise ValueError("sim_range")

        bounded = max(lower, min(upper, requested))
        effective_lower = max(lower, previous_mv - increment)
        effective_upper = min(upper, previous_mv + increment)
        mv = max(effective_lower, min(effective_upper, bounded))
        if requested != bounded:
            saturation_steps += 1
        if mv != bounded:
            rate_steps += 1

        if mode == "manual":
            # Explicit integral tracking keeps the controller ready for handover.
            integral = mv - kp * error
        else:
            delta_i = kp * (h / ti) * error
            if not math.isfinite(delta_i):
                raise ValueError("sim_range")
            if ((requested >= effective_upper and delta_i > 0)
                    or (requested <= effective_lower and delta_i < 0)):
                frozen_steps += 1
            else:
                integral += delta_i

        points.append(dict(seconds=seconds, mode=mode, setpoint=setpoint,
                           pv=pv, cv=cv, requestedMv=requested, mv=mv))
        iae += h * abs(error)
        ise += h * error * error
        inputs.append(mv)
        previous_mv = mv
        pv = advance(pv, inputs)
        if not math.isfinite(pv):
            raise ValueError("sim_range")

    points.append(dict(seconds=duration, mode="automatic", setpoint=setpoint,
                       pv=pv, cv=None, requestedMv=None, mv=None))
    warnings = ["exploratory_simulation", "manual_auto_not_industrially_qualified"]
    if h > tau / 10:
        warnings.append("coarse_sample")
    if delay >= duration:
        warnings.append("delay_beyond_horizon")
    return dict(algorithmVersion="1.2.0", warnings=warnings,
                transitionAtSeconds=switch_index * h,
                transferBumpMv=transfer_bump,
                iae=iae, ise=ise, finalError=setpoint - pv,
                saturationSeconds=saturation_steps * h,
                rateLimitedSeconds=rate_steps * h,
                frozenSteps=frozen_steps, points=points)
