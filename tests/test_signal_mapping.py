from decimal import Decimal
import unittest

from mkr_data_import.io.csv_import import import_csv
from mkr_data_import.signal_mapping import map_signals


class SignalMappingTests(unittest.TestCase):
    def setUp(self):
        self.source = import_csv(
            b"Date;Measured;Applied;Mode\n2026-11-21;3.5;-8;MAN\n",
            delimiter=";", encoding="utf-8", decimal_separator=".",
            timestamp_column="Date", date_format="%Y-%m-%d",
            columns={"Measured": "bar", "Applied": "%"},
        )
        self.roles = {"timestamp": "Date", "PV": "Measured", "MV": "Applied"}

    def test_explicit_roles_preserve_sign_units_and_optional_absence(self):
        mapped = map_signals(self.source, {**self.roles, "mode": "Mode"})
        self.assertEqual(mapped.values["MV"], (Decimal("-8"),))
        self.assertEqual(mapped.values["mode"], ("MAN",))
        self.assertEqual(mapped.units, {"PV": "bar", "MV": "%"})
        self.assertNotIn("SP", mapped.values)

    def test_incomplete_and_ambiguous_mapping(self):
        with self.assertRaises(ValueError):
            map_signals(self.source, {"timestamp": "Date", "PV": "Measured"})
        with self.assertRaises(ValueError):
            map_signals(self.source, {**self.roles, "MV": "Measured"})

    def test_missing_units_and_wrong_time_column(self):
        with self.assertRaisesRegex(ValueError, "declared unit"):
            map_signals(self.source, {**self.roles, "SP": "Mode"})
        with self.assertRaisesRegex(ValueError, "parsed as a timestamp"):
            map_signals(self.source, {**self.roles, "timestamp": "Mode"})
