from datetime import datetime, timedelta, timezone
import unittest

from mkr_data_import.time_quality import assess_time


class TimeQualityTests(unittest.TestCase):
    def assess(self, timestamps):
        return assess_time(timestamps, relative_tolerance=0.2, gap_factor=2.5,
                           minimum_duration_seconds=20)

    def test_anomalies_keep_original_indices(self):
        start = datetime(2026, 11, 21)
        stamps = [start + timedelta(seconds=seconds) for seconds in (0, 1, 2, 2, 3, 7, 8, 7.5, 9)]
        report = self.assess(stamps)
        self.assertEqual(report.duplicates, (3,))
        self.assertEqual(report.reversals, (7,))
        self.assertEqual(report.gaps, (5,))
        self.assertEqual(report.irregular_intervals, (8,))
        self.assertEqual(report.estimated_step_seconds, 1)
        self.assertEqual(report.observed_span_seconds, 9)
        self.assertTrue(report.insufficient_duration)

    def test_absent_precision_and_mixed_timezones(self):
        self.assertIsNone(self.assess([]).observed_span_seconds)
        stamp = datetime(2026, 11, 21, tzinfo=timezone.utc)
        self.assertIsNone(self.assess([stamp]).estimated_step_seconds)
        with self.assertRaisesRegex(ValueError, "Mixed timezone"):
            self.assess([stamp, stamp.replace(tzinfo=None)])
