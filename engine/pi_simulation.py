"""Sampled PI comparison on a deviation-form FOPDT plant."""
import math

from fopdt_simulation import grid_index, mv_increment, number, plant_step


def response_metrics(points, reference):
    overshoot = settling = None
    if reference != 0 and len(points) >= 2:
        # Use signed, physical-unit differences to keep tiny setpoints numerically safe.
        direction = math.copysign(1, reference)
        overshoot = max(0.0, max(direction * (point["pv"] - reference) for point in points))
        outside = [index for index, point in enumerate(points)
                   if abs(point["pv"] - reference) > 0.05 * abs(reference)]
        first_in_band = outside[-1] + 1 if outside else 0
        if first_in_band < len(points) - 1:
            settling = points[first_in_band]["seconds"]
    return dict(windowEndSeconds=points[-1]["seconds"], overshootPv=overshoot, settlingSeconds=settling)


def additional_metrics(points, sp, window_end):
    """Left-endpoint ITAE; total MV travel includes departure from zero deviation."""
    itae = travel = previous_mv = 0.0
    for current, following in zip(points, points[1:]):
        dt = following["seconds"] - current["seconds"]
        itae += current["seconds"] * abs(sp - current["pv"]) * dt
        travel += abs(current["mv"] - previous_mv)
        previous_mv = current["mv"]
    crossings = []
    if sp:
        for threshold in (0.1, 0.9):
            for left, right in zip(points[:window_end], points[1:window_end+1]):
                a, b = left["pv"]/sp, right["pv"]/sp
                if a < threshold <= b:
                    crossings.append(left["seconds"] + (threshold-a)/(b-a) * (right["seconds"]-left["seconds"]))
                    break
    rise = crossings[1] - crossings[0] if len(crossings) == 2 else None
    return dict(itae=itae, mvTravel=travel, riseTimeSeconds=rise)


def calculate(payload):
    required = {"gain", "tauSeconds", "delaySeconds", "imc", "simc", "sampleSeconds", "durationSeconds",
                "setpoint", "minimumMv", "maximumMv", "disturbance", "disturbanceAtSeconds"}
    if payload.keys() - {"maximumMvRate"} != required:
        raise ValueError("sim_input")

    gain, tau, delay, h, duration, sp, lower, upper, disturbance, event = (
        number(payload[key]) for key in ("gain", "tauSeconds", "delaySeconds", "sampleSeconds", "durationSeconds",
                                        "setpoint", "minimumMv", "maximumMv", "disturbance", "disturbanceAtSeconds"))
    if gain == 0 or tau <= 0 or delay < 0 or h <= 0 or duration <= 0 or not lower < 0 < upper or not 0 <= event <= duration:
        raise ValueError("sim_domain")

    rate_step = mv_increment(payload, h)
    n, event_index = grid_index(duration, h), grid_index(event, h)
    if not 2 <= n <= 5000:
        raise ValueError("sim_grid")
    advance = plant_step(gain, tau, delay, h, duration)
    window_end = event_index if disturbance != 0 else n

    def simulate(settings):
        if not isinstance(settings, dict) or settings.keys() != {"kp", "tiSeconds"}:
            raise ValueError("sim_input")
        kp, ti = number(settings["kp"]), number(settings["tiSeconds"])
        if kp == 0 or math.copysign(1, kp) != math.copysign(1, gain) or ti <= 0:
            raise ValueError("sim_domain")
        ys, us, points = 0.0, [], []
        integral = iae = ise = saturated = rate_limited = previous_mv = 0.0
        frozen = 0
        for k in range(n):
            error = sp - ys
            raw = kp * error + integral
            if not math.isfinite(raw):
                raise ValueError("sim_range")
            bounded = max(lower, min(upper, raw))
            effective_lower = max(lower, previous_mv - rate_step)
            effective_upper = min(upper, previous_mv + rate_step)
            mv = max(effective_lower, min(effective_upper, bounded))
            previous_mv = mv
            step_i = (kp * (h / ti)) * error
            if not math.isfinite(step_i):
                raise ValueError("sim_range")
            if (raw >= effective_upper and step_i > 0) or (raw <= effective_lower and step_i < 0):
                frozen += 1
            else:
                integral += step_i
            applied_disturbance = disturbance if k >= event_index else 0.0
            us.append(mv + applied_disturbance)
            points.append(dict(seconds=k * h, pv=ys, cv=raw, mv=mv, disturbance=applied_disturbance))
            iae += abs(error) * h
            ise += error * error * h
            saturated += h if raw != bounded else 0.0
            rate_limited += h if bounded != mv else 0.0

            ys = advance(ys, us)

        points.append(dict(seconds=duration, pv=ys, cv=None, mv=None, disturbance=None))
        return dict(**additional_metrics(points, sp, window_end), iae=iae, ise=ise, saturationSeconds=saturated, rateLimitedSeconds=rate_limited,
                    frozenSteps=frozen, finalError=sp - ys, points=points,
                    setpointResponse=response_metrics(points[:window_end + 1], sp))

    warnings = ["exploratory_simulation"]
    if h > tau / 10:
        warnings.append("coarse_sample")
    if delay >= duration:
        warnings.append("delay_beyond_horizon")
    return dict(algorithmVersion="1.1.0", warnings=warnings, imc=simulate(payload["imc"]), simc=simulate(payload["simc"]))
