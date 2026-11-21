from decimal import Decimal
import unittest

from mkr_data_import.signal_quality import assess_signal


class SignalQualityTests(unittest.TestCase):
    def test_observed_anomalies(self):
        values = list(map(Decimal, ["2", "2", "100", "2"]))
        report = assess_signal(values, flat_tolerance=Decimal(0),
                               saturation=(Decimal(0), Decimal(100)),
                               outlier_distance=Decimal(20), modes=["MAN", "MAN", "AUTO", "AUTO"])
        self.assertEqual(report.outliers, (2,))
        self.assertEqual(report.saturated, (2,))
        self.assertEqual(report.mode_changes, (2,))
        self.assertFalse(report.frozen)
        self.assertIsNone(report.high_variation)

    def test_incomplete_data_do_not_prove_a_frozen_signal(self):
        report = assess_signal([Decimal(2), Decimal("NaN"), None], flat_tolerance=Decimal(0))
        self.assertEqual(report.invalid, (1, 2))
        self.assertIsNone(report.frozen)
        self.assertIsNone(report.insufficient_excitation)

    def test_flat_signal_and_insufficient_excitation(self):
        report = assess_signal([Decimal("2.0"), Decimal("2.1")],
                               flat_tolerance=Decimal("0.1"), excitation_span=Decimal(1))
        self.assertTrue(report.frozen)
        self.assertTrue(report.insufficient_excitation)
