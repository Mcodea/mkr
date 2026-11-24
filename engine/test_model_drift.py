import base64
import json
import math
import unittest
from datetime import datetime, timedelta, timezone

import numpy as np

from mkr_engine import respond
from model_drift import check


def trial(gain=2.0, tau=30.0, delay=5.0, amplitude=3.0, sigma=0.02, seed=1, cadence=1.0, pre=12, post=200):
    rng = np.random.default_rng(seed)
    start = datetime(2026, 5, 1, tzinfo=timezone.utc)
    lines = ["timestamp,mv,pv"]
    for i in range(pre + post):
        t = (i - pre) * cadence
        pv = 40 + gain * amplitude * (1 - math.exp(-max(t - delay, 0) / tau)) + rng.normal(0, sigma)
        lines.append(f"{(start + timedelta(seconds=i * cadence)).isoformat()},{amplitude if t >= 0 else 0},{pv}")
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",",
                timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


def request(source, model=None, sigma=0.02):
    return dict(model=model or dict(gain=2.0, tauSeconds=30.0, delaySeconds=5.0), trial=source, mvTolerance=0.01,
                pvTolerance=6 * sigma, maximumRmse=max(2 * sigma, 3 * sigma), maximumResidualLagOne=0.5)


class Drift(unittest.TestCase):
    def test_an_unchanged_process_is_consistent(self):
        for seed in range(5):
            with self.subTest(seed=seed):
                result = check(request(trial(seed=seed)))
                self.assertEqual(result["verdict"], "consistent", result)
                self.assertEqual(result["driftedParameters"], [])

    def test_a_slower_process_is_a_drift_of_tau_only(self):
        result = check(request(trial(tau=45.0)))
        self.assertEqual(result["verdict"], "drift")
        self.assertEqual(result["driftedParameters"], ["tauSeconds"])
        self.assertAlmostEqual(result["changes"]["tauSeconds"]["current"] / 45.0, 1, delta=0.05)

    def test_a_lower_gain_and_a_longer_delay_are_each_located(self):
        gain = check(request(trial(gain=1.5)))
        self.assertEqual((gain["verdict"], gain["driftedParameters"]), ("drift", ["gain"]))
        delay = check(request(trial(delay=9.0)))
        self.assertEqual(delay["verdict"], "drift")
        self.assertIn("delaySeconds", delay["driftedParameters"])

    def test_a_small_change_inside_the_tolerated_band_is_not_an_alarm(self):
        result = check(request(trial(tau=31.0, gain=2.04)))          # +3 % and +2 %
        self.assertEqual(result["driftedParameters"], [])

    def test_noise_alone_does_not_raise_an_alarm_over_many_realisations(self):
        alarms = sum(check(request(trial(seed=s, sigma=0.05), sigma=0.05))["verdict"] == "drift" for s in range(20))
        self.assertLessEqual(alarms, 1)

    def test_the_sign_of_the_gain_is_kept(self):
        result = check(request(trial(gain=-2.0), model=dict(gain=-2.0, tauSeconds=30.0, delaySeconds=5.0)))
        self.assertEqual(result["verdict"], "consistent")

    def test_bad_inputs(self):
        source = trial()
        with self.assertRaisesRegex(ValueError, "drift_mapping"):
            check(dict(request(source), model=dict(gain=1.0)))
        with self.assertRaisesRegex(ValueError, "drift_domain"):
            check(request(source, model=dict(gain=0, tauSeconds=1, delaySeconds=0)))
        with self.assertRaisesRegex(ValueError, "fit_step"):
            check(request(dict(source, raw=base64.b64encode(b"timestamp,mv,pv\n" + b"\n".join(
                f"2026-05-01T00:00:{i:02d}+00:00,0,1".encode() for i in range(30))).decode())))

    def test_the_engine_serves_it(self):
        reply = respond(json.dumps(dict(version=1, requestId="drift", operation="check_model_drift", payload=request(trial()))).encode())
        self.assertEqual(reply["result"]["verdict"], "consistent")


if __name__ == "__main__":
    unittest.main()
