"""Declared reading format of a CSV file: decimal separator, timestamp pattern and time zone.

The original bytes are never changed. The format is declared by the user (MKR only *suggests* one),
stored with the import, and applied every time the file is read, so a French historian export
(semicolon, decimal comma, local date and time) is read exactly the same way by quality control,
trial preview, step search and identification.

Local times are converted to UTC with the IANA time-zone database. A local time that does not exist
(spring forward) or that is ambiguous (autumn fallback) is refused, never guessed.
"""
from datetime import datetime, timezone
import math
import re

from csv_quality import NUMBER, TIMESTAMP

DECIMALS = (".", ",")
NUMBER_COMMA = re.compile(r"[+-]?(?:[0-9]+(?:,[0-9]*)?|,[0-9]+)(?:[eE][+-]?[0-9]+)?")
ZONE_NAME = re.compile(r"UTC|[A-Za-z]+(?:/[A-Za-z0-9_+-]+){1,2}")
# Whitelisted patterns: a free strptime pattern could hide an ambiguous day/month order.
PATTERNS = (
    "%d/%m/%Y %H:%M:%S", "%d/%m/%Y %H:%M:%S.%f", "%d.%m.%Y %H:%M:%S", "%d.%m.%Y %H:%M:%S.%f",
    "%Y-%m-%d %H:%M:%S", "%Y-%m-%d %H:%M:%S.%f", "%Y-%m-%dT%H:%M:%S", "%Y-%m-%dT%H:%M:%S.%f",
)


class Format:
    def __init__(self, decimal=".", pattern=None, zone=None):
        self.decimal, self.pattern, self.zone_name = decimal, pattern, zone
        self._zone = None
        if pattern is not None:
            if zone == "UTC":
                self._zone = timezone.utc
            else:
                try:
                    from zoneinfo import ZoneInfo
                    self._zone = ZoneInfo(zone)
                except Exception as error:                      # unknown zone or no tz database installed
                    raise ValueError("csv_timezone") from error

    @property
    def is_default(self):
        return self.decimal == "." and self.pattern is None

    def number(self, cell):
        """Finite float, or NaN when the cell is not a number in the declared notation."""
        cell = cell.strip()
        if self.decimal == ",":
            return float(cell.replace(",", ".")) if NUMBER_COMMA.fullmatch(cell) else math.nan
        return float(cell) if NUMBER.fullmatch(cell) else math.nan

    def timestamp(self, cell):
        """UTC datetime. Raises ValueError for any cell that is not a valid, unambiguous instant."""
        if self.pattern is None:
            if not TIMESTAMP.fullmatch(cell):
                raise ValueError("timestamp")
            return datetime.fromisoformat(cell).astimezone(timezone.utc)
        naive = datetime.strptime(cell.strip(), self.pattern)
        if self._zone is timezone.utc:
            return naive.replace(tzinfo=timezone.utc)
        first, second = naive.replace(tzinfo=self._zone, fold=0), naive.replace(tzinfo=self._zone, fold=1)
        instant = first.astimezone(timezone.utc)
        # Nonexistent first: in a spring-forward gap Python also reports two offsets, so the round trip decides.
        if instant.astimezone(self._zone).replace(tzinfo=None) != naive:
            raise ValueError("nonexistent_local_time")         # spring forward: the hour does not exist
        if first.utcoffset() != second.utcoffset():
            raise ValueError("ambiguous_local_time")           # autumn fallback: the hour exists twice
        return instant


DEFAULT = Format()


def from_payload(payload, delimiter=None):
    """Format declared in a request (`format` key, optional); the default is ISO 8601 with offset and a decimal point."""
    spec = payload.get("format") if isinstance(payload, dict) else None
    if spec is None:
        return DEFAULT
    time = spec.get("time") if isinstance(spec, dict) else None
    if (not isinstance(spec, dict) or spec.keys() != {"decimal", "time"} or spec["decimal"] not in DECIMALS
            or not (time is None or (isinstance(time, dict) and time.keys() == {"pattern", "zone"}
                                     and time["pattern"] in PATTERNS and isinstance(time["zone"], str)
                                     and ZONE_NAME.fullmatch(time["zone"])))):
        raise ValueError("csv_format")
    if spec["decimal"] == "," and delimiter == ",":
        raise ValueError("csv_format")                         # a decimal comma cannot share the field separator
    return Format(spec["decimal"], time["pattern"] if time else None, time["zone"] if time else None)


def sampled(rows, first=50, spread=200):
    """The first rows plus evenly spaced rows of the whole file: a recording often starts on a flat, integer-valued plateau."""
    step = max(1, len(rows) // spread)
    return rows[:first] + rows[first::step]


def suggest(headers, rows):
    """Proposal only: what the file looks like. Nothing is applied until the user confirms it."""
    cells = [row[i] for row in sampled(rows) for i in range(len(headers))]
    comma = sum(bool(NUMBER_COMMA.fullmatch(c.strip())) and "," in c for c in cells)
    point = sum(bool(NUMBER.fullmatch(c.strip())) and "." in c for c in cells)
    column = pattern = None
    first = rows[:20]
    for index, name in enumerate(headers):
        values = [row[index].strip() for row in first]
        if values and all(TIMESTAMP.fullmatch(v) for v in values):
            column, pattern = name, None
            break
        for candidate in PATTERNS:
            try:
                for v in values:
                    datetime.strptime(v, candidate)
            except ValueError:
                continue
            if values:
                column, pattern = name, candidate
                break
        if column:
            break
    return dict(decimal="," if comma and not point else ".", timeColumn=column, timePattern=pattern,
                isoWithOffset=column is not None and pattern is None)
