"""Read a bounded CSV and report descriptive quality without changing its rows."""
import base64
import csv
import io
import math
import re

MAX_BYTES = 1_048_576
MAX_ROWS = 10_000
TIMESTAMP = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,6})?(?:Z|[+-](?:[01][0-9]|2[0-3]):[0-5][0-9])")
NUMBER = re.compile(r"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?")
ROLES = {"SP", "PV", "CV", "MV", "d"}


def read_table(payload):
    if payload.get("delimiter") not in (",", ";") or not isinstance(payload.get("raw"), str):
        raise ValueError("invalid_request")
    raw = base64.b64decode(payload["raw"], validate=True)
    if len(raw) > MAX_BYTES:
        raise ValueError("csv_limit")
    reader = csv.reader(io.StringIO(raw.decode("utf-8-sig"), newline=""),
                        delimiter=payload["delimiter"], strict=True)
    headers = next(reader, [])
    if (not 1 <= len(headers) <= 64 or any(not h.strip() or len(h) > 120 for h in headers)
            or len(set(headers)) != len(headers)):
        raise ValueError("csv_header")
    rows = []
    for row in reader:
        if len(rows) == MAX_ROWS:
            raise ValueError("csv_limit")
        if len(row) != len(headers):
            raise ValueError("csv_width")
        rows.append(row)
    if not rows:
        raise ValueError("csv_empty")
    return headers, rows


def analyze(headers, rows, payload):
    time_column = payload.get("timeColumn")
    value_column = payload.get("valueColumn")
    role = payload.get("role")
    unit = payload.get("unit")
    if (not isinstance(time_column, str) or time_column not in headers
            or not isinstance(value_column, str) or value_column not in headers
            or time_column == value_column or not isinstance(role, str) or role not in ROLES
            or not isinstance(unit, str) or not unit.strip() or len(unit) > 40):
        raise ValueError("csv_mapping")

    from csv_format import from_payload
    fmt = from_payload(payload, payload.get("delimiter"))
    ti, vi = headers.index(time_column), headers.index(value_column)
    seen = set()
    previous = None
    minimum = maximum = None
    valid = missing = invalid = invalid_times = duplicates = reversals = 0
    intervals = []
    for row in rows:
        try:
            stamp = fmt.timestamp(row[ti])
        except (ValueError, OverflowError):
            invalid_times += 1
            previous = None
        else:
            duplicates += stamp in seen
            if previous is not None:
                interval = (stamp - previous).total_seconds()
                reversals += interval < 0
                if interval > 0:
                    intervals.append(interval)
            seen.add(stamp)
            previous = stamp
        cell = row[vi].strip()
        if not cell:
            missing += 1
            continue
        value = fmt.number(cell)
        if not math.isfinite(value):
            invalid += 1
            continue
        valid += 1
        minimum = value if minimum is None else min(value, minimum)
        maximum = value if maximum is None else max(value, maximum)

    return dict(algorithmVersion="1.1.0", validValues=valid, missingValues=missing,
                invalidValues=invalid, invalidTimestamps=invalid_times,
                duplicateTimestamps=duplicates, reversedTimestamps=reversals,
                minimum=minimum, maximum=maximum, minPositiveStepSeconds=min(intervals, default=None),
                maxPositiveStepSeconds=max(intervals, default=None))


def process_csv(payload, inspect_only):
    keys = {"raw", "delimiter"}
    if not inspect_only:
        keys |= {"timeColumn", "valueColumn", "role", "unit"}
    if not keys <= payload.keys() <= keys | {"format"}:
        raise ValueError("invalid_request")
    try:
        headers, rows = read_table(payload)
    except (UnicodeError, csv.Error) as error:
        raise ValueError("csv_encoding_or_syntax") from error
    result = dict(headers=headers, preview=rows[:200], rowCount=len(rows))
    if inspect_only:
        from csv_format import suggest
        result["suggestedFormat"] = suggest(headers, rows)
    if not inspect_only:
        result["quality"] = analyze(headers, rows, payload)
    return result
