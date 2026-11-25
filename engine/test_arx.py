"""ARX / output-error identification with arbitrary excitation: parameter recovery with known truth, non-steady start, model-order
choice, refusals. The synthetic truth is generated here from the exact zero-order-hold difference equation, independently of the module."""
import base64
import math
import unittest
from datetime import datetime, timedelta, timezone

import numpy as np

import arx
from recommend import calculate as recommend
from test_recommend import sensitivity_peak


def make(day, u, y, ts):
    start = datetime(2026, 6, day, tzinfo=timezone.utc)
    lines = ["timestamp,mv,pv"] + [f"{(start + timedelta(seconds=i * ts)).isoformat()},{float(u[i])!r},{float(y[i])!r}" for i in range(len(u))]
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",", timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


def prbs(n, rng, hold=5):
    return np.repeat(rng.choice([-1.0, 1.0], size=n // hold + 1), hold)[:n]


def first_order_zoh(u, tau, gain, delay_samples, ts, y0, base, noise, rng):
    """Exact ZOH equivalent of K exp(-theta s)/(tau s+1) with theta = delay_samples * ts: y(k+1) = p y(k) + K (1-p) u(k - delay_samples)."""
    p = math.exp(-ts / tau)
    y = np.zeros(len(u))
    y[0] = y0
    for k in range(1, len(u)):
        y[k] = p * y[k - 1] + gain * (1 - p) * (u[k - 1 - delay_samples] if k - 1 - delay_samples >= 0 else 0.0)
    return base + y + noise * rng.standard_normal(len(u))


def second_order_zoh(u, tau1, tau2, gain, ts, noise, rng):
    p1, p2 = math.exp(-ts / tau1), math.exp(-ts / tau2)
    y1 = np.zeros(len(u))
    y = np.zeros(len(u))
    for k in range(1, len(u)):
        y1[k] = p1 * y1[k - 1] + (1 - p1) * u[k - 1]
        y[k] = p2 * y[k - 1] + (1 - p2) * y1[k - 1]
    return gain * y + noise * rng.standard_normal(len(u))


def payload(train, valid, limit=0.2, **extra):
    return dict(training=train, validation=valid, mvTolerance=1e-6, maximumRmse=limit, loop="open", **extra)


class Recovery(unittest.TestCase):
    def test_gain_time_constant_and_delay_are_recovered_from_a_non_steady_start_and_published_errors_cover_the_truth(self):
        ts, tau, gain, nk = 2.0, 20.0, 1.5, 3
        hits = {"gain": 0, "tau": 0}
        runs = 0
        for seed in range(6):
            rng = np.random.default_rng(seed)
            u, v = prbs(600, rng), prbs(500, rng)
            y = first_order_zoh(u, tau, gain, nk, ts, y0=5.0, base=50.0, noise=0.02, rng=rng)           # starts 5 units off, operating point 50
            w = first_order_zoh(v, tau, gain, nk, ts, y0=-3.0, base=50.0, noise=0.02, rng=rng)
            result = arx.identify(payload(make(1, u, y, ts), make(2, v, w, ts)))
            self.assertEqual(result["decision"], "accepted", result.get("reasons"))
            chosen = result["selected"]
            equivalent = chosen["firstOrder"]
            self.assertEqual((chosen["na"], chosen["nb"]), (1, 1))
            self.assertEqual(equivalent["delaySeconds"], nk * ts)                                       # delay in samples is found exactly
            self.assertAlmostEqual(equivalent["gain"] / gain, 1, delta=0.01)
            self.assertAlmostEqual(equivalent["tauSeconds"] / tau, 1, delta=0.03)
            errors = equivalent["standardErrors"]
            hits["gain"] += abs(equivalent["gain"] - gain) <= 3 * errors["gain"]
            hits["tau"] += abs(equivalent["tauSeconds"] - tau) <= 3 * errors["tauSeconds"]
            runs += 1
        self.assertGreaterEqual(hits["gain"], runs - 1)
        self.assertGreaterEqual(hits["tau"], runs - 1)

    def test_a_second_order_process_is_refused_as_first_order_and_accepted_as_second_order(self):
        ts = 2.0
        rng = np.random.default_rng(11)
        u, v = prbs(800, rng), prbs(700, rng)
        y = second_order_zoh(u, 25.0, 8.0, 1.2, ts, 0.01, rng)
        w = second_order_zoh(v, 25.0, 8.0, 1.2, ts, 0.01, rng)
        result = arx.identify(payload(make(1, u, y, ts), make(2, v, w, ts), limit=0.1))
        self.assertEqual(result["decision"], "accepted", result.get("reasons"))
        self.assertEqual((result["selected"]["na"], result["selected"]["nb"]), (2, 2))
        first_order = [c for c in result["candidates"] if c["na"] == 1]
        self.assertTrue(first_order and all(not c["accepted"] for c in first_order))

    def test_the_identified_model_feeds_the_guided_tuning_and_the_tuning_holds_on_the_true_process(self):
        ts, tau, gain, nk = 2.0, 20.0, 1.5, 3
        rng = np.random.default_rng(5)
        u, v = prbs(600, rng), prbs(500, rng)
        y = first_order_zoh(u, tau, gain, nk, ts, 2.0, 10.0, 0.02, rng)
        w = first_order_zoh(v, tau, gain, nk, ts, -1.0, 10.0, 0.02, rng)
        chosen = arx.identify(payload(make(1, u, y, ts), make(2, v, w, ts)))["selected"]["firstOrder"]
        tuning = recommend(dict(family="FOPDT", gain=chosen["gain"], tauSeconds=chosen["tauSeconds"], delaySeconds=chosen["delaySeconds"] + ts,
                                secondTauSeconds=None, sampleSeconds=ts, standardErrors=chosen["standardErrors"], objective="balanced",
                                derivativeFilterRatio=None, processVariationPercent=None, optimise=False))
        self.assertEqual(tuning["decision"], "recommended")
        c = tuning["recommendation"]["controller"]
        # true continuous plant: K exp(-theta s)/(tau s + 1) with theta = nk Ts (+ the ZOH half sample, + the sampling delay Ts/2 of the loop)
        peak = sensitivity_peak(gain, [tau], nk * ts + ts / 2 + ts / 2, c["kp"], c["tiSeconds"], 0.0)
        self.assertLessEqual(peak, 1.7 + 0.1)


class Refusals(unittest.TestCase):
    def series(self, seed=3, **changes):
        ts = 2.0
        rng = np.random.default_rng(seed)
        u, v = prbs(400, rng), prbs(300, rng)
        y = first_order_zoh(u, 20.0, 1.5, 2, ts, 0.0, 0.0, 0.02, rng)
        w = first_order_zoh(v, 20.0, 1.5, 2, ts, 0.0, 0.0, 0.02, rng)
        return ts, u, y, v, w

    def test_closed_loop_data_are_refused(self):
        ts, u, y, v, w = self.series()
        with self.assertRaisesRegex(ValueError, "arx_closed_loop"):
            arx.identify({**payload(make(1, u, y, ts), make(2, v, w, ts)), "loop": "closed"})

    def test_a_constant_input_has_no_information(self):
        ts, u, y, v, w = self.series()
        with self.assertRaisesRegex(ValueError, "arx_excitation"):
            arx.identify(payload(make(1, np.ones_like(u), y, ts), make(2, v, w, ts)))

    def test_irregular_sampling_is_refused_not_interpolated(self):
        ts, u, y, v, w = self.series()
        train = make(1, u, y, ts)
        lines = base64.b64decode(train["raw"]).decode().split("\n")
        start = datetime(2026, 6, 1, tzinfo=timezone.utc)
        lines[50] = f"{(start + timedelta(seconds=49.2 * ts)).isoformat()}" + lines[50][lines[50].index(","):]
        train["raw"] = base64.b64encode("\n".join(lines).encode()).decode()
        with self.assertRaisesRegex(ValueError, "arx_sampling"):
            arx.identify(payload(train, make(2, v, w, ts)))

    def test_an_uninformative_validation_series_does_not_validate_anything(self):
        ts, u, y, v, w = self.series()
        flat = np.zeros_like(v)
        flat[100:] = 1.0                                       # one step only: the delay and the dynamics are barely tested
        rng = np.random.default_rng(9)
        weak = first_order_zoh(flat, 20.0, 1.5, 2, ts, 0.0, 0.0, 0.5, rng)       # heavy noise hides the response
        result = arx.identify(payload(make(1, u, y, ts), make(2, flat, weak, ts), limit=0.05))
        self.assertEqual(result["decision"], "refused")
        self.assertTrue(result["actions"])

    def test_too_few_samples_and_bad_mapping_are_refused(self):
        ts, u, y, v, w = self.series()
        short = make(1, u[:20], y[:20], ts)
        with self.assertRaises(ValueError):
            arx.identify(payload(short, make(2, v, w, ts)))
        bad = make(1, u, y, ts)
        bad["mvColumn"] = "nope"
        with self.assertRaisesRegex(ValueError, "arx_mapping"):
            arx.identify(payload(bad, make(2, v, w, ts)))
        with self.assertRaises(ValueError):
            arx.identify({"training": 1})


if __name__ == "__main__":
    unittest.main()
