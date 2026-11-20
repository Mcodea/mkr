"""Shared grid, actuator input and FOPDT dynamics for simulations."""
import math


def number(value):
    if type(value) not in (float, int) or not -1e12 <= value <= 1e12:
        raise ValueError("sim_input")
    return float(value)


def grid_index(seconds, sample_seconds):
    ratio = seconds / sample_seconds
    if not math.isfinite(ratio) or ratio > 5000:
        raise ValueError("sim_grid")
    rounded = round(ratio)
    if abs(ratio - rounded) > 1e-8:
        raise ValueError("sim_grid")
    return rounded


def mv_increment(payload, sample):
    value = payload.get("maximumMvRate")
    if value is None:
        return math.inf
    rate = number(value)
    if rate <= 0:
        raise ValueError("sim_domain")
    return rate * sample


def plant_step(gain, tau, delay, h, duration):
    delay_steps = min(delay, duration) / h
    full = math.floor(delay_steps)
    fraction = h * (delay_steps - full)
    if math.isclose(fraction, h, rel_tol=1e-12):
        full, fraction = full + 1, 0
    alpha_first = -math.expm1(-fraction / tau)
    alpha_second = -math.expm1(-(h - fraction) / tau)

    def advance(pv, inputs):
        delayed = len(inputs) - 1 - full
        previous = inputs[delayed - 1] if delayed > 0 else 0.0
        current = inputs[delayed] if delayed >= 0 else 0.0
        pv += alpha_first * (gain * previous - pv)
        return pv + alpha_second * (gain * current - pv)

    return advance
