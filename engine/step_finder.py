"""Find identifiable open-loop MV steps in a long recording and propose independent trial pairs.

Every candidate window is validated with the same reader as the identification
(`fopdt.read_step`), so a window listed here is accepted by the estimators for the
declared tolerances. Nothing is smoothed, filled or modified: rows are only located.

Scope: step tests with the controller in manual (MV is the applied output). Closed-loop
data, ramps, pulses and slow MV drifts are not identification trials and are reported
as rejections, never silently used.
"""
from datetime import datetime, timezone
import math

import numpy as np

from csv_format import from_payload
from csv_quality import read_table
from fopdt import number, read_step

VERSION = "1.1.0"
MAXIMUM_PRE_SAMPLES = 60       # Longest pre-step baseline offered to the estimators.
MAXIMUM_CANDIDATES = 200       # Bound on the number of windows validated per request.
MAXIMUM_PAIRS = 20
AMPLITUDE_RATIO = (0.5, 2.0)   # Same rule as fopdt.read_trials.
SHARED_BASELINE_SAMPLES = 8    # Baseline kept by the second trial when two steps share a plateau.


def _series(source):
    required = {"raw", "delimiter", "timeColumn", "mvColumn", "pvColumn"}
    if not isinstance(source, dict) or source.keys() - {"format"} != required:
        raise ValueError("fit_mapping")
    try:
        fmt = from_payload(source, source["delimiter"])
    except ValueError as error:
        raise ValueError("fit_mapping") from error
    headers, rows = read_table(source)
    columns = [source[field] for field in ("timeColumn", "mvColumn", "pvColumn")]
    if any(not isinstance(c, str) or c not in headers for c in columns) or len(set(columns)) != 3:
        raise ValueError("fit_mapping")
    ti, ui, yi = (headers.index(c) for c in columns)
    dates = []
    for row in rows:
        try:
            dates.append(fmt.timestamp(row[ti]))
        except (OverflowError, ValueError) as error:
            raise ValueError("fit_time") from error
    times = np.array([(d - dates[0]).total_seconds() for d in dates])
    if np.any(np.diff(times) <= 0):
        raise ValueError("fit_time")

    def column(index):
        values = []
        for row in rows:
            try:
                values.append(number(row[index], fmt))
            except ValueError:
                values.append(math.nan)       # A gap or bad value only blocks windows containing it.
        return np.array(values)

    return dates, column(ui), column(yi)


def _read_tolerances(payload):
    if not isinstance(payload, dict) or payload.keys() != {"source", "mvTolerance", "pvTolerance"}:
        raise ValueError("fit_mapping")
    tolerances = [payload["mvTolerance"], payload["pvTolerance"]]
    if any(type(v) not in (float, int) or not 1e-100 <= v <= 1e100 for v in tolerances):
        raise ValueError("fit_tolerance")
    return tolerances


def find(payload):
    mv_tolerance, pv_tolerance = _read_tolerances(payload)
    source = payload["source"]
    dates, mv, pv = _series(source)
    n = len(dates)
    invalid = ~(np.isfinite(mv) & np.isfinite(pv))
    jump = np.zeros(n, dtype=bool)
    both = ~invalid[1:] & ~invalid[:-1]
    jump[1:] = both & (np.abs(mv[1:] - mv[:-1]) > 2 * mv_tolerance)
    events = np.flatnonzero(jump)
    invalid_rows = np.flatnonzero(invalid)

    candidates, rejected = [], []
    for position, k in enumerate(events):
        k = int(k)
        if len(candidates) + len(rejected) >= MAXIMUM_CANDIDATES:
            rejected.append(dict(stepRow=k, reason="search_limit"))
            break
        previous_event = int(events[position - 1]) if position else -1
        next_event = int(events[position + 1]) if position + 1 < len(events) else n
        if previous_event == k - 1 or next_event == k + 1:
            rejected.append(dict(stepRow=k, reason="ramp_or_multi_sample_change"))
            continue
        before = invalid_rows[invalid_rows < k]
        after = invalid_rows[invalid_rows > k]
        floor = max(previous_event, int(before[-1]) + 1 if len(before) else 0, 0)
        ceiling = min(next_event, int(after[0]) if len(after) else n) - 1
        # Longest recent baseline whose PV stays inside the declared tolerance.
        start = k
        low = high = pv[k - 1]
        while start > floor and k - start < MAXIMUM_PRE_SAMPLES:
            value = pv[start - 1]
            if max(high, value) - min(low, value) > pv_tolerance:
                break
            low, high = min(low, value), max(high, value)
            start -= 1
        if k - start < 4:
            rejected.append(dict(stepRow=k, reason="baseline_too_short_or_unstable"))
            continue
        if ceiling - k + 1 < 12:
            rejected.append(dict(stepRow=k, reason="response_window_too_short"))
            continue
        try:
            candidates.append(_describe(source, dates, mv, k, start, ceiling, mv_tolerance, pv_tolerance, len(candidates) + 1))
        except ValueError as error:
            rejected.append(dict(stepRow=k, reason=str(error)))
    return dict(algorithmVersion=VERSION, sourceRows=n, candidates=candidates, rejected=rejected,
                pairs=_pairs(source, dates, mv, candidates, mv_tolerance, pv_tolerance))


def _describe(source, dates, mv, k, start, end, mv_tolerance, pv_tolerance, identifier):
    """Validate rows start..end with the estimators' own reader and summarise the trial."""
    step = read_step(dict(source, rowStart=start, rowEnd=end), mv_tolerance, pv_tolerance)
    tail = max(6, len(step.observations) // 8)
    tail_values = step.observations[-tail:]
    settled = bool(np.ptp(tail_values) <= 2 * pv_tolerance)
    warnings = [] if settled else ["response_not_settled"]
    if step.baseline_noise > 0 and abs(float(np.mean(tail_values)) - step.baseline) < 5 * step.baseline_noise:
        warnings.append("low_signal_to_noise")
    return dict(
        id=identifier, rowStart=start, rowEnd=end, stepRow=k,
        startUtc=dates[start].isoformat(), endUtc=dates[end].isoformat(),
        mvBefore=float(mv[k - 1]), mvAfter=float(mv[k]), amplitude=step.amplitude,
        pvBaseline=step.baseline, pvResponse=float(np.mean(tail_values)) - step.baseline,
        preSamples=k - start, postSamples=len(step.observations),
        horizonSeconds=float(step.times[-1]), onsetIntervalSeconds=step.onset_interval,
        settled=settled, baselineNoise=step.baseline_noise, warnings=warnings)


def _separate(source, dates, mv, first, second, mv_tolerance, pv_tolerance):
    """Row-disjoint windows for two trials, or None.

    Back-to-back steps (up then down) share the settled plateau between them. The estimators require
    disjoint time ranges, so the plateau is split: the second trial keeps its last 4 to 8 baseline
    samples and the first trial stops just before them. Both windows are validated again.
    """
    if second["rowStart"] > first["rowEnd"]:
        return first, second
    pre = min(second["preSamples"], SHARED_BASELINE_SAMPLES)
    start = second["stepRow"] - pre
    if pre < 4 or start - first["stepRow"] < 12:
        return None
    try:
        return (_describe(source, dates, mv, first["stepRow"], first["rowStart"], start - 1,
                          mv_tolerance, pv_tolerance, first["id"]),
                _describe(source, dates, mv, second["stepRow"], start, second["rowEnd"],
                          mv_tolerance, pv_tolerance, second["id"]))
    except ValueError:
        return None


def _pairs(source, dates, mv, candidates, mv_tolerance, pv_tolerance):
    """Pairs the estimators accept; opposite directions and settled responses first."""
    pairs = []
    for i, first in enumerate(candidates):
        for second in candidates[i + 1:]:
            ratio = abs(second["amplitude"] / first["amplitude"])
            if not AMPLITUDE_RATIO[0] <= ratio <= AMPLITUDE_RATIO[1]:
                continue
            windows = _separate(source, dates, mv, first, second, mv_tolerance, pv_tolerance)
            if windows is None:
                continue
            a, b = windows
            opposite = first["amplitude"] * second["amplitude"] < 0
            notes = []
            if not (a["settled"] and b["settled"]):
                notes.append("response_not_settled")
            if not opposite:
                notes.append("same_direction")
            if windows[0] is not first:
                notes.append("shared_plateau_split")
            pairs.append(dict(
                firstId=first["id"], secondId=second["id"], amplitudeRatio=ratio, oppositeDirection=opposite,
                firstWindow=dict(rowStart=a["rowStart"], rowEnd=a["rowEnd"]),
                secondWindow=dict(rowStart=b["rowStart"], rowEnd=b["rowEnd"]),
                firstSettled=a["settled"], secondSettled=b["settled"], notes=notes))
    pairs.sort(key=lambda p: (("response_not_settled" in p["notes"]), not p["oppositeDirection"],
                              abs(math.log(p["amplitudeRatio"])), p["firstId"], p["secondId"]))
    return pairs[:MAXIMUM_PAIRS]
