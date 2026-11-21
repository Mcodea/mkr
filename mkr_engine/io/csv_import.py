"""Import timestamped CSV without modifying original bytes or source cells."""
from dataclasses import dataclass
from datetime import datetime
from decimal import Decimal, InvalidOperation
import csv
import hashlib
import io
import re


@dataclass(frozen=True)
class ImportErrorDetail:
    code: str
    message: str
    row: int | None = None  # CSV record number, header is 1
    column: str | None = None


@dataclass(frozen=True)
class ImportResult:
    raw: bytes
    metadata: dict
    headers: tuple[str, ...]
    rows: tuple[tuple[str, ...], ...]
    records: tuple[dict, ...]
    errors: tuple[ImportErrorDetail, ...]


def import_csv(raw: bytes, *, delimiter: str, encoding: str, decimal_separator: str,
               timestamp_column: str, date_format: str, columns: dict[str, str]) -> ImportResult:
    """Columns maps signal column names to units. Dates use explicit strptime format.

    Errors are reported per cell; invalid and missing parsed values are None. Raw
    cells and bytes are retained, and no rows are removed for data errors.
    """
    if not isinstance(raw, bytes):
        raise TypeError("raw must be bytes")
    metadata = {"sha256": hashlib.sha256(raw).hexdigest(), "encoding": encoding,
                "delimiter": delimiter, "decimal_separator": decimal_separator,
                "timestamp_column": timestamp_column, "date_format": date_format,
                "columns": dict(columns) if isinstance(columns, dict) else columns}
    errors = []
    headers, rows, records = [], [], []

    def result():
        return ImportResult(raw, metadata, tuple(headers), tuple(rows), tuple(records), tuple(errors))

    if (delimiter not in (",", ";", "\t", "|") or decimal_separator not in (",", ".")
            or not isinstance(encoding, str) or not encoding
            or not isinstance(timestamp_column, str) or not timestamp_column
            or not isinstance(date_format, str) or not date_format
            or not isinstance(columns, dict) or not columns
            or any(not isinstance(k, str) or not k.strip() or not isinstance(v, str) or not v.strip()
                   for k, v in columns.items()) or timestamp_column in columns):
        errors.append(ImportErrorDetail("invalid_configuration", "Invalid CSV settings or column units."))
        return result()
    try:
        text = raw.decode(encoding, errors="strict")
    except (UnicodeError, LookupError):
        errors.append(ImportErrorDetail("invalid_encoding", "Unable to decode with the specified encoding."))
        return result()
    try:
        reader = csv.reader(io.StringIO(text, newline=""), delimiter=delimiter, strict=True)
        headers.extend(next(reader, []))
        if not headers or any(not h.strip() for h in headers) or len(set(headers)) != len(headers):
            errors.append(ImportErrorDetail("invalid_header", "Headers must be present and unique."))
            return result()
        if any(c not in headers for c in (timestamp_column, *columns)):
            errors.append(ImportErrorDetail("missing_column", "A mapped column is absent."))
            return result()
        pattern = re.compile(r"[+-]?(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][+-]?\d+)?\Z")
        for number, row in enumerate(reader, start=2):
            rows.append(tuple(row))
            if len(row) != len(headers):
                errors.append(ImportErrorDetail("row_width", "Column count differs from header.", number))
                records.append({"timestamp": None, "values": {}})
                continue
            cells = dict(zip(headers, row))
            try:
                stamp = datetime.strptime(cells[timestamp_column], date_format)
            except ValueError:
                stamp = None
                errors.append(ImportErrorDetail("invalid_date", "Date does not match the specified format.", number, timestamp_column))
            values = {}
            for column in columns:
                cell = cells[column]
                if not cell.strip():
                    values[column] = None
                    errors.append(ImportErrorDetail("missing_value", "Value is missing.", number, column))
                    continue
                try:
                    if not pattern.fullmatch(cell) or (decimal_separator == "," and "." in cell) or (decimal_separator == "." and "," in cell):
                        raise InvalidOperation
                    value = Decimal(cell.replace(",", "."))
                    if not value.is_finite():
                        raise InvalidOperation
                    values[column] = value
                except InvalidOperation:
                    values[column] = None
                    errors.append(ImportErrorDetail("invalid_number", "Invalid numeric value.", number, column))
            records.append({"timestamp": stamp, "values": values})
    except csv.Error:
        errors.append(ImportErrorDetail("invalid_csv", "Malformed CSV syntax."))
    metadata["row_count"] = len(rows)
    return result()
