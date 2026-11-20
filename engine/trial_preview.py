"""Read-only and provenance-preserving preview of two imported process signals."""
import csv
from datetime import datetime, timezone
import math

from csv_quality import NUMBER, TIMESTAMP, read_table

VERSION = "1.1.0"
MAX_PLOT_POINTS = 600



def display_indices(mv, pv, limit=MAX_PLOT_POINTS):
    """Select original source rows, prioritizing both signal extrema and sharp changes.

    Each time-ordered bucket contributes up to ten indices: its boundaries,
    MV/PV minima and maxima, and both sides of the largest intra-bucket
    adjacent change of each signal. Selection is an *inspection rendering*
    only; source rows, timestamps and fit inputs are never altered.
    """
    length = len(mv)
    if length <= limit:
        return list(range(length))
    # Ten candidates maximum per bucket; the union is globally <= limit.
    bucket_count = max(1, limit // 10)
    selected = set()
    for bucket in range(bucket_count):
        first = bucket * length // bucket_count
        end = (bucket + 1) * length // bucket_count
        if first >= end:
            continue
        local = range(first, end)
        selected.update((first, end - 1))
        for signal in (mv, pv):
            selected.add(min(local, key=lambda i: signal[i]))
            selected.add(max(local, key=lambda i: signal[i]))
            if end - first >= 2:
                edge = max(range(first + 1, end),
                           key=lambda i: abs(signal[i] - signal[i - 1]))
                selected.update((edge - 1, edge))
    return sorted(selected)


def preview(payload):
    expected = {"raw", "delimiter", "timeColumn", "mvColumn", "pvColumn"}
    if not isinstance(payload, dict) or payload.keys() != expected:
        raise ValueError("preview_mapping")
    try:
        headers, rows = read_table(payload)
    except (UnicodeError, csv.Error) as error:
        raise ValueError("csv_encoding_or_syntax") from error
    names = [payload[key] for key in ("timeColumn", "mvColumn", "pvColumn")]
    if any(type(name) is not str or name not in headers for name in names) or len(set(names)) != 3:
        raise ValueError("preview_mapping")
    ti, mi, pi = [headers.index(name) for name in names]
    times, mv, pv = [], [], []
    for row in rows:
        stamp = row[ti]
        if not TIMESTAMP.fullmatch(stamp):
            raise ValueError("preview_time")
        try:
            date = datetime.fromisoformat(stamp).astimezone(timezone.utc)
        except (ValueError, OverflowError) as error:
            raise ValueError("preview_time") from error
        if times and date <= times[-1]:
            raise ValueError("preview_time")
        times.append(date)
        values = []
        for index in (mi, pi):
            cell = row[index].strip()
            value = float(cell) if NUMBER.fullmatch(cell) else math.nan
            if not math.isfinite(value) or abs(value) > 1e100:
                raise ValueError("preview_data")
            values.append(value)
        mv.append(values[0])
        pv.append(values[1])

    if len(times) < 2:
        raise ValueError("preview_data")
    indices = display_indices(mv, pv)
    origin = times[0]
    points = [dict(rowIndex=i, seconds=(times[i] - origin).total_seconds(),
                   mv=mv[i], pv=pv[i]) for i in indices]
    return dict(algorithmVersion=VERSION, sourceRows=len(times),
                displayedRows=len(points), displayDecimated=len(times) > len(points),
                firstUtc=origin.isoformat(), lastUtc=times[-1].isoformat(), points=points)
