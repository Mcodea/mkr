"""Read a named worksheet without rewriting its original workbook."""
from datetime import datetime
from decimal import Decimal
import hashlib
from io import BytesIO
from math import isfinite
from xml.etree.ElementTree import ParseError
from zipfile import BadZipFile

from openpyxl import load_workbook
from openpyxl.utils.exceptions import InvalidFileException

from .csv_import import ImportErrorDetail, ImportResult


def import_excel(
    raw: bytes,
    *,
    sheet: str,
    timestamp_column: str,
    columns: dict[str, str],
    header_row: int = 1,
) -> ImportResult:
    """Read native Excel dates and numeric cells with explicitly declared units."""
    if not isinstance(raw, bytes):
        raise TypeError("raw must be bytes")
    metadata = {
        "sha256": hashlib.sha256(raw).hexdigest(),
        "sheet": sheet,
        "timestamp_column": timestamp_column,
        "columns": columns.copy() if isinstance(columns, dict) else columns,
        "header_row": header_row,
    }
    headers, rows, records, errors = [], [], [], []

    def result():
        return ImportResult(raw, metadata, tuple(headers), tuple(rows), tuple(records), tuple(errors))

    if (
        any(not isinstance(value, str) or not value.strip() for value in (sheet, timestamp_column))
        or type(header_row) is not int or header_row < 1
        or not isinstance(columns, dict) or not columns or timestamp_column in columns
        or any(not isinstance(value, str) or not value.strip()
               for pair in columns.items() for value in pair)
    ):
        errors.append(ImportErrorDetail("invalid_configuration", "Invalid worksheet settings or column units."))
        return result()

    try:
        workbook = load_workbook(BytesIO(raw), read_only=True, data_only=False)
    except (BadZipFile, InvalidFileException, KeyError, OSError, ValueError, ParseError):
        errors.append(ImportErrorDetail("invalid_workbook", "Unable to read the XLSX workbook."))
        return result()

    try:
        if sheet not in workbook.sheetnames:
            errors.append(ImportErrorDetail("missing_sheet", "The specified worksheet is absent."))
            return result()
        worksheet = workbook[sheet]
        raw_rows = worksheet.iter_rows(min_row=header_row, values_only=True)
        header = list(next(raw_rows, ()))
        while header and header[-1] is None:
            header.pop()
        if (
            not header
            or any(not isinstance(cell, str) or not cell.strip() for cell in header)
            or len(set(header)) != len(header)
        ):
            errors.append(ImportErrorDetail("invalid_header", "Headers must be present and unique.", header_row))
            return result()

        headers.extend(header)
        if any(column not in headers for column in (timestamp_column, *columns)):
            errors.append(ImportErrorDetail("missing_column", "A mapped column is absent.", header_row))
            return result()

        for number, row in enumerate(raw_rows, start=header_row + 1):
            rows.append(row)
            cells = dict(zip(headers, row))
            stamp = cells.get(timestamp_column)
            if not isinstance(stamp, datetime):
                errors.append(ImportErrorDetail("invalid_date", "Expected a native Excel date.", number, timestamp_column))
                stamp = None
            values = {}
            for column in columns:
                cell = cells.get(column)
                if cell is None:
                    errors.append(ImportErrorDetail("missing_value", "Value is missing.", number, column))
                    values[column] = None
                elif type(cell) is int or (type(cell) is float and isfinite(cell)):
                    values[column] = Decimal(str(cell))
                else:
                    errors.append(ImportErrorDetail("invalid_number", "Expected a finite numeric cell.", number, column))
                    values[column] = None
            records.append({"timestamp": stamp, "values": values})
        metadata["row_count"] = len(rows)
    except (BadZipFile, InvalidFileException, KeyError, OSError, ValueError, ParseError):
        errors.append(ImportErrorDetail("invalid_workbook", "Unable to read the XLSX worksheet."))
    finally:
        workbook.close()
    return result()
