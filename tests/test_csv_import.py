import unittest
from decimal import Decimal
from datetime import datetime
from mkr_engine.io.csv_import import import_csv


class CsvImportTests(unittest.TestCase):
    def load(self, raw, **overrides):
        options = dict(delimiter=";", encoding="utf-8", decimal_separator=",",
                       timestamp_column="date", date_format="%d/%m/%Y %H:%M:%S",
                       columns={"température": "°C"})
        options.update(overrides)
        return import_csv(raw, **options)

    def test_french_csv_retains_original_and_units(self):
        raw = 'date;température\r\n21/11/2026 12:00:00;"12,50"\r\n'.encode()
        result = self.load(raw)
        self.assertEqual(result.errors, ())
        self.assertEqual(result.raw, raw)
        self.assertEqual(result.rows[0][1], "12,50")
        self.assertEqual(result.records[0]["values"]["température"], Decimal("12.50"))
        self.assertEqual(result.records[0]["timestamp"], datetime(2026, 11, 21, 12))
        self.assertEqual(result.metadata["columns"], {"température": "°C"})

    def test_invalid_and_missing_values_do_not_remove_rows(self):
        result = self.load('date;température\n31/02/2026 12:00:00;\n21/11/2026 12:00:00;1.2\n'.encode())
        self.assertEqual([e.code for e in result.errors], ["invalid_date", "missing_value", "invalid_number"])
        self.assertEqual([e.row for e in result.errors], [2, 2, 3])
        self.assertEqual(len(result.rows), 2)
        self.assertIsNone(result.records[0]["timestamp"])
        self.assertEqual(result.rows[1][1], "1.2")

    def test_encoding_and_column_validation(self):
        raw = 'date;température\n21/11/2026 12:00:00;1,2\n'.encode("cp1252")
        self.assertEqual(self.load(raw, encoding="cp1252").errors, ())
        self.assertEqual(self.load(raw).errors[0].code, "invalid_encoding")
        self.assertEqual(self.load(raw, encoding="cp1252", columns={"missing": "K"}).errors[0].code, "missing_column")
        self.assertEqual(self.load(raw, encoding="cp1252", columns={"température": ""}).errors[0].code, "invalid_configuration")

    def test_other_delimiter_and_invalid_width(self):
        raw = b'date\tvalue\n2026-11-21\t1.5\n2026-11-22\n'
        result = self.load(raw, delimiter="\t", decimal_separator=".", date_format="%Y-%m-%d", columns={"value": "1"})
        self.assertEqual(result.records[0]["values"]["value"], Decimal("1.5"))
        self.assertEqual(result.errors[0].code, "row_width")
        self.assertEqual(result.rows[1], ("2026-11-22",))

    def test_duplicate_headers_and_bad_syntax(self):
        self.assertEqual(self.load(b'date;date\n').errors[0].code, "invalid_header")
        result = self.load('date;température\n"unterminated'.encode())
        self.assertEqual(result.errors[0].code, "invalid_csv")
        self.assertEqual(result.raw, 'date;température\n"unterminated'.encode())


if __name__ == "__main__":
    unittest.main()
