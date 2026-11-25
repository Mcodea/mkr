import json
import unittest

from mkr_engine import respond
from test_fopdt import source as first_order
from test_sopdt import source as second_order
import base64
import math
from datetime import datetime, timedelta, timezone

import numpy as np

from model_comparison import compare
from step_finder import find
from validation_gate import assess, suggest


def payload(family, training, validation, rmse=0.01, lag=0.5):
    return dict(family=family, maximumRmse=rmse, maximumResidualLagOne=lag,
                trials=dict(training=training, validation=validation, mvTolerance=0.01, pvTolerance=0.01))


class ValidationGateTests(unittest.TestCase):
    def test_consistent_holdout_is_accepted(self):
        result = assess(payload("FOPDT", first_order(1), first_order(2, amplitude=-1.5)))
        self.assertTrue(result["assessment"]["accepted"])
        self.assertEqual(result["assessment"]["reasons"], [])
        self.assertEqual(result["assessment"]["criteria"], dict(maximumRmse=0.01, maximumResidualLagOne=0.5))

    def test_holdout_from_another_process_is_refused_for_every_family(self):
        # Audit NC-02: the validation trial follows a process with three times the gain.
        drifted = first_order(2, gain=-9)
        for family in ("FOPDT", "SOPDT", "BroidaFOPDT"):
            with self.subTest(family=family):
                result = assess(payload(family, first_order(1), drifted))
                self.assertFalse(result["assessment"]["accepted"])
                self.assertIn("validation_rmse", result["assessment"]["reasons"])

    def test_sopdt_holdout_refuses_first_order_but_accepts_second_order(self):
        trials = (second_order(1, 5, 2), second_order(2, 5, 2))
        self.assertFalse(assess(payload("FOPDT", *trials, rmse=0.001))["assessment"]["accepted"])
        self.assertTrue(assess(payload("SOPDT", *trials, rmse=0.001))["assessment"]["accepted"])

    def test_blocking_estimation_warning_refuses_even_with_small_error(self):
        # A 70-sample trial cannot show four time constants of a 40 s process.
        slow = assess(payload("FOPDT", first_order(1, tau=40), first_order(2, tau=40), rmse=10, lag=1))
        self.assertFalse(slow["assessment"]["accepted"])
        self.assertTrue({"short_horizon", "parameter_bound"} & set(slow["assessment"]["reasons"]))

    def test_fit_error_at_round_off_level_is_not_judged_on_residual_correlation(self):
        # Two windows of one exact recording (up then down, gain -1.5, tau 40 s, delay 10 s): the fit leaves
        # ~1e-13 of numerical error on a PV of about 8. That is round-off, far below any sensor resolution,
        # so the model must not be refused on the autocorrelation of that error.
        start = datetime(2026, 2, 1, tzinfo=timezone.utc)
        lines = ["timestamp;mv;pv"]
        for i in range(600):
            t = 5.0 * i
            level = (2 * (1 - math.exp(-(t - 310) / 40)) if t >= 310 else 0.0)
            level -= (2 * (1 - math.exp(-(t - 1510) / 40)) if t >= 1510 else 0.0)
            lines.append(f"{(start + timedelta(seconds=t)).isoformat()};{2 if 300 <= t < 1500 else 0};{8 - 1.5 * level!r}")
        source = dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=";",
                      timeColumn="timestamp", mvColumn="mv", pvColumn="pv")
        pair = find(dict(source=source, mvTolerance=0.01, pvTolerance=0.01))["pairs"][0]
        trials = dict(training=dict(source, **pair["firstWindow"]), validation=dict(source, **pair["secondWindow"]),
                      mvTolerance=0.01, pvTolerance=0.01)
        result = assess(dict(family="FOPDT", trials=trials, maximumRmse=0.01, maximumResidualLagOne=0.5))
        self.assertGreater(result["report"]["validation"]["rmse"], 32 * math.ulp(8.0))   # above the old guard
        self.assertLess(result["report"]["validation"]["rmse"], 1e-9)
        self.assertTrue(result["assessment"]["accepted"], result["assessment"]["reasons"])

    def test_criteria_and_mapping_are_checked(self):
        base = payload("FOPDT", first_order(1), first_order(2))
        for change in (dict(maximumRmse=0), dict(maximumResidualLagOne=1.5), dict(maximumRmse=True)):
            with self.subTest(change=change), self.assertRaisesRegex(ValueError, "fit_comparison_limits"):
                assess({**base, **change})
        with self.assertRaisesRegex(ValueError, "fit_mapping"):
            assess({**base, "family": "PID"})

    def test_ipc_operation(self):
        request = dict(version=1, requestId="gate", operation="assess_model",
                       payload=payload("FOPDT", first_order(1), first_order(2, gain=-9)))
        reply = respond(json.dumps(request).encode())
        self.assertFalse(reply["result"]["assessment"]["accepted"])
        self.assertAlmostEqual(reply["result"]["report"]["gain"], -3, places=5)


def noisy(day, sigma, slow=20.0, fast=None, seed=0):
    """FOPDT (or SOPDT when fast is set) step with white noise on every PV sample, 0.5 s cadence."""
    rng = np.random.default_rng(seed + day)
    start = datetime(2026, 5, day, tzinfo=timezone.utc)
    lines = ["timestamp,mv,pv"]
    for i in range(600):
        t = 0.5 * i
        x = max(0.0, t - 13.0)
        shape = (1 - math.exp(-x / slow)) if fast is None else \
            1 - (slow * math.exp(-x / slow) - fast * math.exp(-x / fast)) / (slow - fast)
        lines.append(f"{(start + timedelta(seconds=t)).isoformat()},{1.0 if t >= 10 else 0.0},{2 * shape + sigma * rng.standard_normal()!r}")
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",",
                timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


class CriteriaFromNoiseTests(unittest.TestCase):
    def trials(self, sigma, fast=None, seed=0):
        return dict(training=noisy(1, sigma, fast=fast, seed=seed), validation=noisy(2, sigma, fast=fast, seed=seed),
                    mvTolerance=1e-6, pvTolerance=max(4 * sigma, 1e-6))

    def test_suggestion_uses_the_measured_plateau_noise_only(self):
        trials = self.trials(0.02)
        result = suggest(trials)
        self.assertAlmostEqual(result["baselineNoise"], 0.02, delta=0.008)   # 20 pre-step samples per trial
        floor = trials["pvTolerance"] / 2
        self.assertAlmostEqual(result["maximumRmse"], max(2 * result["baselineNoise"], floor))
        self.assertEqual(result["basis"], "noise" if 2 * result["baselineNoise"] >= floor else "pv_tolerance")
        # Ljung 99 % whiteness bound for the 580 post-step validation samples (no fixed number): 2.576 / sqrt(N).
        self.assertAlmostEqual(result["maximumResidualLagOne"], 2.576 / math.sqrt(580), places=9)
        self.assertEqual(result["lagBasis"], "ljung_white_residuals_99")
        # A tighter declared tolerance lets the measured noise set the threshold.
        tight = suggest({**trials, "pvTolerance": 0.05})
        self.assertEqual(tight["basis"], "noise")
        self.assertAlmostEqual(tight["maximumRmse"], 2 * tight["baselineNoise"])
        # Without measurable noise the declared PV tolerance sets the floor.
        exact = suggest(dict(training=first_order(1), validation=first_order(2), mvTolerance=0.01, pvTolerance=0.01))
        self.assertEqual((exact["baselineNoise"], exact["maximumRmse"], exact["basis"]), (0.0, 0.005, "pv_tolerance"))

    def test_audit_case_lax_thresholds_no_longer_hide_the_second_pole(self):
        # Audit NC-04: true SOPDT (20 s, 12 s), sigma 0.02, thresholds 0.2 / 0.9 used to select FOPDT.
        trials = self.trials(0.02, fast=12.0)
        result = compare(dict(trials=trials, maximumRmse=0.2, maximumResidualLagOne=0.9))
        self.assertEqual(result["candidates"]["FOPDT"]["rejectionReasons"], [])
        self.assertEqual((result["selected"], result["decision"]), ("SOPDT", "significantly_better_higher_order"))
        self.assertGreater(result["holdoutTest"]["mseRatio"], result["holdoutTest"]["critical"])

    def test_true_first_order_keeps_parsimony_with_suggested_criteria(self):
        for seed in range(10):
            with self.subTest(seed=seed):
                trials = self.trials(0.02, seed=seed)
                criteria = suggest(trials)
                result = compare(dict(trials=trials, maximumRmse=criteria["maximumRmse"],
                                      maximumResidualLagOne=criteria["maximumResidualLagOne"]))
                self.assertEqual(result["selected"], "FOPDT")

    def test_exact_data_never_triggers_the_holdout_test(self):
        result = compare(dict(trials=dict(training=first_order(1), validation=first_order(2), mvTolerance=0.01,
                                          pvTolerance=0.01), maximumRmse=0.001, maximumResidualLagOne=0.5))
        self.assertEqual((result["selected"], result["decision"], result["holdoutTest"]), ("FOPDT", "lowest_acceptable_order", None))


if __name__ == "__main__":
    unittest.main()
