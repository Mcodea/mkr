"""Report timestamp defects without sorting, filling or resampling data."""
from dataclasses import dataclass
from datetime import datetime, timezone
from math import isfinite
from statistics import median
from typing import Sequence


@dataclass(frozen=True)
class TimeQuality:
    duplicates: tuple[int, ...]
    reversals: tuple[int, ...]
    gaps: tuple[int, ...]
    irregular_intervals: tuple[int, ...]
    estimated_step_seconds: float | None
    observed_span_seconds: float | None
    insufficient_duration: bool | None


def assess_time(
    timestamps: Sequence[datetime],
    *,
    relative_tolerance: float,
    gap_factor: float,
    minimum_duration_seconds: float,
) -> TimeQuality:
    """Report zero-based indices of the second sample in each affected interval."""
    settings = (relative_tolerance, gap_factor, minimum_duration_seconds)
    if (
        not all(isfinite(value) for value in settings)
        or not 0 <= relative_tolerance < 1
        or gap_factor <= 1 + relative_tolerance
        or minimum_duration_seconds <= 0
    ):
        raise ValueError("Specify a valid step tolerance, gap factor and minimum duration.")
    if any(not isinstance(stamp, datetime) for stamp in timestamps):
        raise ValueError("Time assessment requires parsed timestamps.")
    awareness = {stamp.utcoffset() is not None for stamp in timestamps}
    if len(awareness) > 1:
        raise ValueError("Mixed timezone-aware and timezone-naive timestamps require explicit normalization.")
    if awareness == {True}:
        timestamps = [stamp.astimezone(timezone.utc) for stamp in timestamps]

    seen, duplicates, reversals, positive_intervals = set(), [], [], []
    for index, stamp in enumerate(timestamps):
        if stamp in seen:
            duplicates.append(index)
        seen.add(stamp)
        if index:
            interval = (stamp - timestamps[index - 1]).total_seconds()
            if interval < 0:
                reversals.append(index)
            elif interval > 0:
                positive_intervals.append((index, interval))

    step = median(interval for _, interval in positive_intervals) if positive_intervals else None
    gaps, irregular = [], []
    if step is not None:
        for index, interval in positive_intervals:
            if interval > gap_factor * step:
                gaps.append(index)
            elif abs(interval - step) > relative_tolerance * step:
                irregular.append(index)
    span = (max(timestamps) - min(timestamps)).total_seconds() if timestamps else None
    return TimeQuality(
        tuple(duplicates), tuple(reversals), tuple(gaps), tuple(irregular),
        step, span, span < minimum_duration_seconds if span is not None else None,
    )
