from datetime import datetime
from decimal import Decimal
from io import BytesIO
import hashlib
import unittest

from openpyxl import Workbook

from mkr_data_import.io.excel_import import import_excel


class ExcelImportTests(unittest.TestCase):
    def workbook(self, *rows):
        workbook = Workbook()
        sheet = workbook.active
        sheet.title = "Essai"
        sheet.append(["Titre"])
        sheet.append(["Horodatage", "Mesure"])
        for row in rows:
            sheet.append(row)
        stream = BytesIO()
        workbook.save(stream)
        workbook.close()
        return stream.getvalue()

    def load(self, raw, **options):
        return import_excel(raw, **{
            "sheet": "Essai", "timestamp_column": "Horodatage",
            "columns": {"Mesure": "bar"}, "header_row": 2, **options,
        })

    def test_native_date_and_original_bytes(self):
        stamp = datetime(2026, 11, 21, 12, 30)
        raw = self.workbook([stamp, 1.25])
        result = self.load(raw)
        self.assertEqual(result.errors, ())
        self.assertEqual(result.records, ({"timestamp": stamp, "values": {"Mesure": Decimal("1.25")}},))
        self.assertEqual(result.raw, raw)
        self.assertEqual(result.metadata["sha256"], hashlib.sha256(raw).hexdigest())

    def test_missing_sheet_and_bad_workbook(self):
        self.assertEqual(self.load(self.workbook(), sheet="Absent").errors[0].code, "missing_sheet")
        self.assertEqual(self.load(b"bad workbook").errors[0].code, "invalid_workbook")

    def test_empty_cells_and_formulas_stay_visible(self):
        stamp = datetime(2026, 11, 21, 12, 30)
        result = self.load(self.workbook([stamp, None], ["date ambiguë", "=1+2"]))
        self.assertEqual([(e.code, e.row) for e in result.errors], [
            ("missing_value", 3), ("invalid_date", 4), ("invalid_number", 4),
        ])
        self.assertEqual(result.rows, ((stamp, None), ("date ambiguë", "=1+2")))
        self.assertTrue(all(record["values"]["Mesure"] is None for record in result.records))
