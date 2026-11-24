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
    return dict(family="FOPDT", model=model or dict(gain=2.0, tauSeconds=30.0, delaySeconds=5.0), trial=source, mvTolerance=0.01,
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


def trial_from(response, amplitude=3.0, sigma=0.01, seed=1, pre=12, post=250, cadence=1.0):
    rng = np.random.default_rng(seed)
    start = datetime(2026, 5, 1, tzinfo=timezone.utc)
    lines = ["timestamp,mv,pv"]
    for i in range(pre + post):
        t = (i - pre) * cadence
        lines.append(f"{(start + timedelta(seconds=i * cadence)).isoformat()},{amplitude if t >= 0 else 0},{40 + amplitude * response(t) + rng.normal(0, sigma)}")
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",",
                timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


def second_order(gain, slow, fast, delay):
    return lambda t: gain * (1 - (slow * math.exp(-(t - delay) / slow) - fast * math.exp(-(t - delay) / fast)) / (slow - fast)) if t > delay else 0.0


def integrating(slope, delay):
    return lambda t: slope * max(t - delay, 0)


def family_request(family, model, source, sigma=0.01):
    return dict(family=family, model=model, trial=source, mvTolerance=0.01, pvTolerance=6 * sigma,
                maximumRmse=3 * sigma, maximumResidualLagOne=0.5)


class OtherFamilies(unittest.TestCase):
    SECOND = dict(gain=2.0, tauSeconds=30.0, secondTauSeconds=6.0, delaySeconds=2.0)
    TANK = dict(gain=0.05, delaySeconds=3.0)

    def test_second_order_drift_is_located_parameter_by_parameter(self):
        self.assertEqual(check(family_request("SOPDT", self.SECOND, trial_from(second_order(2, 30, 6, 2))))["verdict"], "consistent")
        for name, response, expected in (("slow pole", second_order(2, 45, 6, 2), "tauSeconds"),
                                         ("gain", second_order(1.5, 30, 6, 2), "gain"),
                                         ("fast pole", second_order(2, 30, 12, 2), "secondTauSeconds"),
                                         ("delay", second_order(2, 30, 6, 7), "delaySeconds")):
            with self.subTest(change=name):
                result = check(family_request("SOPDT", self.SECOND, trial_from(response)))
                self.assertEqual(result["verdict"], "drift")
                self.assertEqual(result["driftedParameters"], [expected])
                self.assertIn("standard_errors_unavailable", result["warnings"])    # only the relative threshold applies

    def test_integrator_drift_is_located(self):
        self.assertEqual(check(family_request("Integrateur", self.TANK, trial_from(integrating(0.05, 3), post=120)))["verdict"], "consistent")
        slope = check(family_request("Integrateur", self.TANK, trial_from(integrating(0.035, 3), post=120)))
        self.assertEqual((slope["verdict"], slope["driftedParameters"]), ("drift", ["gain"]))
        delay = check(family_request("Integrateur", self.TANK, trial_from(integrating(0.05, 8), post=120)))
        self.assertEqual((delay["verdict"], delay["driftedParameters"]), ("drift", ["delaySeconds"]))

    def test_noise_alone_raises_no_drift_alarm_for_the_other_families(self):
        for family, model, response, post in (("SOPDT", self.SECOND, second_order(2, 30, 6, 2), 250),
                                              ("Integrateur", self.TANK, integrating(0.05, 3), 120)):
            alarms = sum(check(family_request(family, model, trial_from(response, sigma=0.03, seed=s, post=post), sigma=0.03))["verdict"] == "drift"
                         for s in range(10))
            self.assertEqual(alarms, 0, family)

    def test_the_model_must_match_its_family(self):
        source = trial_from(integrating(0.05, 3), post=120)
        for family, model in (("FOPDT", self.TANK), ("Integrateur", self.SECOND), ("Broida", self.TANK)):
            with self.subTest(family=family), self.assertRaisesRegex(ValueError, "drift_mapping"):
                check(family_request(family, model, source))
        with self.assertRaisesRegex(ValueError, "drift_domain"):
            check(family_request("SOPDT", dict(self.SECOND, secondTauSeconds=50.0), source))        # the fast pole cannot exceed the slow one


if __name__ == "__main__":
    unittest.main()
