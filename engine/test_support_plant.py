"""Test support (no tests): exact zero-order-hold plant stepping with an arbitrary, possibly fractional, delay.

Written for the independent oracles of the simulators (audit F-10); nothing here is shared with the production simulators."""
import math

import numpy as np
from scipy import signal


def first_order(gain, tau):
    return np.array([[-1.0 / tau]]), np.array([[gain / tau]]), np.array([[1.0]]), np.array([[0.0]])


def cascade(gain, slow, fast):
    return (np.array([[-1.0 / slow, 0.0], [1.0 / fast, -1.0 / fast]]), np.array([[gain / slow], [0.0]]), np.array([[0.0, 1.0]]), np.array([[0.0]]))


def stepper(system, h, delay):
    """Returns (initial_state, output(state), advance(state, k, history)). The input applied at time t is history[floor((t - delay) / h)]
    (zero before the first sample); it changes at k h + (delay mod h), so each sample interval is split there and every piece is an
    exact zero-order-hold step."""
    cache = {}
    c = system[2]
    r = delay % h if delay % h > 1e-12 else 0.0
    pieces = [(0.0, h)] if r == 0.0 else [(0.0, r), (r, h)]

    def advance(state, k, history):
        for start, stop in pieces:
            middle = k * h + (start + stop) / 2
            j = math.floor((middle - delay) / h + 1e-9)
            u = history[j] if 0 <= j < len(history) else 0.0
            duration = stop - start
            if duration not in cache:
                ad, bd, _, _, _ = signal.cont2discrete(system, duration, method="zoh")
                cache[duration] = (ad, bd[:, 0])
            ad, bd = cache[duration]
            state = ad @ state + bd * u
        return state

    return np.zeros(system[0].shape[0]), (lambda state: float((c @ state)[0])), advance
