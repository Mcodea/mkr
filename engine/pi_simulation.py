"""Sampled PI comparison on a deviation-form FOPDT plant."""
import math


def calculate(payload):
    required = {"gain", "tauSeconds", "delaySeconds", "imc", "simc", "sampleSeconds", "durationSeconds",
                "setpoint", "minimumMv", "maximumMv", "disturbance", "disturbanceAtSeconds"}
    if payload.keys() != required:
        raise ValueError("sim_input")

    def number(value):
        if type(value) not in (float, int) or not -1e12 <= value <= 1e12:
            raise ValueError("sim_input")
        return float(value)

    gain, tau, delay, h, duration, sp, lower, upper, disturbance, event = (
        number(payload[key]) for key in ("gain", "tauSeconds", "delaySeconds", "sampleSeconds", "durationSeconds",
                                        "setpoint", "minimumMv", "maximumMv", "disturbance", "disturbanceAtSeconds"))
    if gain == 0 or tau <= 0 or delay < 0 or h <= 0 or duration <= 0 or not lower < 0 < upper or not 0 <= event <= duration:
        raise ValueError("sim_domain")

    def index(seconds):
        ratio = seconds / h
        if not math.isfinite(ratio) or ratio > 5000:
            raise ValueError("sim_grid")
        rounded = round(ratio)
        if abs(ratio - rounded) > 1e-8:
            raise ValueError("sim_grid")
        return rounded

    n, event_index = index(duration), index(event)
    if not 2 <= n <= 5000:
        raise ValueError("sim_grid")
    delay_steps = min(delay, duration) / h
    full = math.floor(delay_steps)
    fraction = h * (delay_steps - full)
    if math.isclose(fraction, h, rel_tol=1e-12):
        full, fraction = full + 1, 0
    alpha_first = -math.expm1(-fraction / tau)
    alpha_second = -math.expm1(-(h - fraction) / tau)

    def simulate(settings):
        if not isinstance(settings, dict) or settings.keys() != {"kp", "tiSeconds"}:
            raise ValueError("sim_input")
        kp, ti = number(settings["kp"]), number(settings["tiSeconds"])
        if kp == 0 or math.copysign(1, kp) != math.copysign(1, gain) or ti <= 0:
            raise ValueError("sim_domain")
        ys, us, points = 0.0, [], []
        integral = iae = ise = saturated = 0.0
        frozen = 0
        for k in range(n):
            error = sp - ys
            raw = kp * error + integral
            if not math.isfinite(raw):
                raise ValueError("sim_range")
            mv = max(lower, min(upper, raw))
            step_i = (kp * (h / ti)) * error
            if not math.isfinite(step_i):
                raise ValueError("sim_range")
            if (raw >= upper and step_i > 0) or (raw <= lower and step_i < 0):
                frozen += 1
            else:
                integral += step_i
            applied_disturbance = disturbance if k >= event_index else 0.0
            us.append(mv + applied_disturbance)
            points.append(dict(seconds=k * h, pv=ys, cv=raw, mv=mv, disturbance=applied_disturbance))
            iae += abs(error) * h
            ise += error * error * h
            saturated += h if raw != mv else 0.0

            delayed = k - full
            previous = us[delayed - 1] if delayed > 0 else 0.0
            current = us[delayed] if delayed >= 0 else 0.0
            ys += alpha_first * (gain * previous - ys)
            ys += alpha_second * (gain * current - ys)

        points.append(dict(seconds=duration, pv=ys, cv=None, mv=None, disturbance=None))
        return dict(iae=iae, ise=ise, saturationSeconds=saturated, frozenSteps=frozen, finalError=sp - ys, points=points)

    warnings = ["exploratory_simulation"]
    if h > tau / 10:
        warnings.append("coarse_sample")
    if delay >= duration:
        warnings.append("delay_beyond_horizon")
    return dict(algorithmVersion="1.0.0", warnings=warnings, imc=simulate(payload["imc"]), simc=simulate(payload["simc"]))
