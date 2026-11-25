"""Open-loop response to a held MV step on an initially steady FOPDT model."""
import math

from fopdt_simulation import grid_index, mv_increment, number, plant_step


def calculate(payload):
    fields = ("gain", "tauSeconds", "delaySeconds", "sampleSeconds", "durationSeconds",
              "stepMv", "stepAtSeconds", "minimumMv", "maximumMv")
    if payload.keys() - {"maximumMvRate"} != set(fields):
        raise ValueError("sim_input")
    gain, tau, delay, sample, duration, step, event, lower, upper = (
        number(payload[key]) for key in fields
    )
    if (gain == 0 or tau <= 0 or delay < 0 or sample <= 0 or duration <= 0
            or not lower < 0 < upper or not 0 <= event < duration or step == 0):
        raise ValueError("sim_domain")
    count, step_index = grid_index(duration, sample), grid_index(event, sample)
    if not 2 <= count <= 5000 or step_index >= count:
        raise ValueError("sim_grid")

    applied = min(upper, max(lower, step))
    steady_pv = gain * applied
    onset = step_index * sample + delay

    rate_step = mv_increment(payload, sample)
    limited_steps = 0
    if math.isinf(rate_step):
        def pv_at(seconds):
            elapsed = max(0.0, seconds - onset)
            return steady_pv * -math.expm1(-elapsed / tau)

        points = [
            dict(seconds=k * sample, pv=pv_at(k * sample),
                 requestedMv=step if k >= step_index else 0.0,
                 appliedMv=applied if k >= step_index else 0.0)
            for k in range(count)
        ]
        pv = pv_at(duration)
    else:
        advance = plant_step(gain, tau, delay, sample, duration)
        pv = mv = 0.0
        inputs, points = [], []
        for k in range(count):
            target = applied if k >= step_index else 0.0
            mv = max(mv - rate_step, min(mv + rate_step, target))
            limited_steps += mv != target
            points.append(dict(seconds=k * sample, pv=pv,
                               requestedMv=step if k >= step_index else 0.0, appliedMv=mv))
            inputs.append(mv)
            pv = advance(pv, inputs)
    points.append(dict(seconds=duration, pv=pv, requestedMv=None, appliedMv=None))
    warnings = ["exploratory_simulation"]
    if onset >= duration:
        warnings.append("response_beyond_horizon")
    return dict(algorithmVersion="1.1.0", warnings=warnings, steadyPv=steady_pv,
                saturationSeconds=(count - step_index) * sample if applied != step else 0.0,
                rateLimitedSeconds=limited_steps * sample, points=points)
