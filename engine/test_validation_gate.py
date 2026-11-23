import json
import unittest

from mkr_engine import respond
from test_fopdt import source as first_order
from test_sopdt import source as second_order
from validation_gate import assess


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


if __name__ == "__main__":
    unittest.main()
