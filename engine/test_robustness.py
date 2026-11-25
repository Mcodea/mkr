"""Hostile and malformed inputs must end in a coded refusal (ValueError), never in another exception, a hang or a non-finite result.

Seeded, so a failure reproduces. Covers the operations added late: block adapters, integrator PI, model drift, closed loop.
"""
import copy
import math
import random
import unittest

import test_closed_loop as closed
import test_model_drift as drift
from block_adapters import pid_compact, pid_int, pidff
from closed_loop import identify as identify_closed_loop
from model_drift import check
from pi_tuning_integrator import calculate as integrator_pi

HOSTILE = [None, True, False, 0, -1, 1, 1e308, -1e308, 1e-300, float("nan"), float("inf"), "", "x", [], {}, [1], {"a": 1}, 2 ** 80]


def mutate(rng, payload):
    """Copy of `payload` with one random leaf replaced by a hostile value, or one key dropped or added."""
    mutated = copy.deepcopy(payload)
    paths = []

    def walk(node, path):
        if isinstance(node, dict):
            for key, value in node.items():
                paths.append(path + (key,))
                walk(value, path + (key,))

    walk(mutated, ())
    path = rng.choice(paths)
    parent = mutated
    for key in path[:-1]:
        parent = parent[key]
    action = rng.choice(("replace", "replace", "drop", "add"))
    if action == "replace":
        parent[path[-1]] = rng.choice(HOSTILE)
    elif action == "drop":
        parent.pop(path[-1])
    else:
        parent["unexpected"] = rng.choice(HOSTILE)
    return mutated


def finite(value):
    if isinstance(value, float):
        return math.isfinite(value)
    if isinstance(value, dict):
        return all(finite(v) for v in value.values())
    if isinstance(value, (list, tuple)):
        return all(finite(v) for v in value)
    return True


class Hostile(unittest.TestCase):
    def hammer(self, function, payload, rounds):
        rng = random.Random(20261124)
        refused = 0
        for _ in range(rounds):
            case = mutate(rng, payload)
            try:
                result = function(case)
            except ValueError:
                refused += 1
                continue
            except Exception as error:                      # noqa: BLE001 - the point of the test
                self.fail(f"{function.__module__}.{function.__name__} raised {type(error).__name__}: {error} for {case!r}"[:600])
            self.assertTrue(finite(result), f"non-finite result for {case!r}"[:400])
        self.assertGreater(refused, rounds // 10)           # the hostile values are actually exercised

    def test_block_adapters(self):
        self.hammer(pidff, dict(kp=-1.5, tiSeconds=12.5, tdSeconds=2.0, filterSeconds=0.5), 400)
        self.hammer(pid_int, dict(kp=2.0, tiSeconds=12.5, tdSeconds=0, tsSeconds=0.2, pvSpan=200.0, mvSpan=100.0), 400)
        self.hammer(pid_compact, dict(version="V3.0", kp=2.0, tiSeconds=15.0, tdSeconds=3.0, filterSeconds=0.6,
                                      outputPercentPerMvUnit=1.0, cpuFamily="S7-1500", firmware="3.1"), 400)

    def test_integrator_pi(self):
        self.hammer(integrator_pi, dict(gain=0.05, delaySeconds=3.0, responseSeconds=8.0), 300)

    def test_model_drift(self):
        source = drift.trial()
        payload = drift.request(source)
        payload["trial"] = {k: v for k, v in source.items()}
        self.hammer(check, payload, 120)

    def test_closed_loop(self):
        source, _, _ = closed.loop(sigma=0.01, disturbance=0.005, n=900, sp_events=[(60, 1.0), (450, -1.0)])
        payload = closed.request(source, training=(20, 400), validation=(430, 880))
        self.hammer(identify_closed_loop, payload, 60)


class HostileOlderOperations(unittest.TestCase):
    """The same hostile inputs against the operations written earlier: identification, step search, validation gate, PI and PID tuning."""

    hammer = Hostile.hammer

    def test_identification_and_gate(self):
        import fopdt, sopdt, integrator, broida, step_finder, validation_gate
        first, second = drift.trial(), drift.trial(seed=2)
        trials = dict(training=first, validation=dict(second, raw=second["raw"]), mvTolerance=0.01, pvTolerance=0.1)
        for module in (fopdt, sopdt, integrator, broida):
            self.hammer(module.identify, trials, 40)
        self.hammer(step_finder.find, dict(source=first, mvTolerance=0.01, pvTolerance=0.1), 60)
        self.hammer(validation_gate.suggest, trials, 40)
        self.hammer(validation_gate.assess, dict(family="FOPDT", trials=trials, maximumRmse=0.1, maximumResidualLagOne=0.5), 40)

    def test_tuning(self):
        import pi_tuning, pid_tuning
        self.hammer(pi_tuning.calculate, dict(gain=2.0, tauSeconds=30.0, delaySeconds=5.0, responseSeconds=10.0), 300)
        self.hammer(pid_tuning.calculate, dict(gain=2.0, tauSeconds=30.0, secondTauSeconds=6.0, delaySeconds=5.0, responseSeconds=10.0), 300)


if __name__ == "__main__":
    unittest.main()
