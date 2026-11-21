import unittest

from pid_tuning import calculate


class SimcPidTests(unittest.TestCase):
    def test_signed_gain_and_series_equivalence(self):
        report = calculate(dict(gain=-2, tauSeconds=30, secondTauSeconds=3,
                                responseSeconds=2, delaySeconds=1))
        self.assertEqual(report["series"], dict(kc=-5, tiSeconds=12, tdSeconds=3))
        ideal = report["ideal"]
        for s in (0.1j, 1j, 2 + 3j):
            reference = -5 * (1 + 1/(12*s)) * (1 + 3*s)
            actual = ideal["kp"] * (1 + 1/(ideal["tiSeconds"]*s) + ideal["tdSeconds"]*s)
            self.assertAlmostEqual(actual, reference)

    def test_invalid_second_pole(self):
        for pole in (0, -1, 12, float("nan"), True):
            with self.assertRaisesRegex(ValueError, "pid_second_pole"):
                calculate(dict(gain=2, tauSeconds=10, secondTauSeconds=pole,
                               responseSeconds=3, delaySeconds=1))
