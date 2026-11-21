import unittest

from model_comparison import compare
from test_fopdt import source as first_order
from test_sopdt import source as second_order


def compare_trials(training, validation):
    return compare(dict(trials=dict(training=training, validation=validation,
                                    mvTolerance=0.01, pvTolerance=0.01),
                        maximumRmse=0.001, maximumResidualLagOne=0.5))


class ComparisonTests(unittest.TestCase):
    def test_parsimony_on_single_pole(self):
        self.assertEqual(compare_trials(first_order(1), first_order(2))["selected"], "FOPDT")

    def test_two_resolvable_poles(self):
        self.assertEqual(compare_trials(second_order(1, 5, 2), second_order(2, 5, 2))["selected"], "SOPDT")

    def test_good_training_fit_cannot_override_validation_drift(self):
        result = compare_trials(second_order(1, 5, 2), second_order(2, 8, 2))
        self.assertEqual(result["decision"], "insufficient_evidence")
        self.assertIsNone(result["selected"])
