"""Explicit, replayable transformations of one numeric time series."""
from dataclasses import dataclass
from decimal import Decimal
from typing import Literal


Sample = tuple[Decimal, Decimal]


@dataclass(frozen=True)
class Transformation:
    operation: Literal["window", "resample", "moving_average"]
    parameters: tuple[Decimal, ...]
    reason: str
    version: int = 1


@dataclass(frozen=True)
class TransformedSeries:
    original: tuple[Sample, ...]
    values: tuple[Sample, ...]
    history: tuple[Transformation, ...]


def transform(
    samples: tuple[Sample, ...], history: tuple[Transformation, ...],
) -> TransformedSeries:
    """Replay from immutable (seconds, value) pairs, with no extrapolation."""
    original = tuple((time, value) for time, value in samples)
    if any(not time.is_finite() or not value.is_finite() for time, value in original):
        raise ValueError("All input samples must contain finite times and values.")
    if any(right[0] <= left[0] for left, right in zip(original, original[1:])):
        raise ValueError("Input timestamps must be strictly increasing.")
    values = original
    for step in history:
        if step.version != 1 or not step.reason.strip() or any(not p.is_finite() for p in step.parameters):
            raise ValueError("Transformation requires supported parameters, a version and a reason.")
        if step.operation == "window":
            start, end = step.parameters
            if start > end:
                raise ValueError("Window start cannot follow window end.")
            values = tuple(sample for sample in values if start <= sample[0] <= end)
        elif step.operation == "resample":
            cadence, maximum_gap = step.parameters
            values = _resample(values, cadence, maximum_gap)
        elif step.operation == "moving_average":
            (count,) = step.parameters
            if count < 1 or count != count.to_integral_value() or count > len(values):
                raise ValueError("Filter window must be an integer within the series length.")
            width = int(count)
            values = tuple((time, sum(value for _, value in values[index-width+1:index+1]) / count)
                           for index, (time, _) in enumerate(values) if index >= width - 1)
        else:
            raise ValueError("Unknown transformation.")
        if not values:
            raise ValueError("Transformation produced no samples.")
    return TransformedSeries(original, values, tuple(history))


def _resample(values: tuple[Sample, ...], cadence: Decimal, maximum_gap: Decimal) -> tuple[Sample, ...]:
    if len(values) < 2 or cadence <= 0 or maximum_gap <= 0:
        raise ValueError("Resampling requires at least two samples and positive cadence and gap bounds.")
    start, end = values[0][0], values[-1][0]
    if (end - start) / cadence >= 1_000_000:
        raise ValueError("Resampling would exceed one million samples.")
    output = []
    index = 0
    for number in range(int((end-start) // cadence) + 1):
        time = start + number * cadence
        while index + 1 < len(values) and values[index+1][0] <= time:
            index += 1
        left_time, left = values[index]
        if left_time == time:
            output.append((time, left))
            continue
        right_time, right = values[index+1]
        gap = right_time - left_time
        if gap > maximum_gap:
            raise ValueError("Interpolation gap exceeds the declared maximum.")
        output.append((time, left + (right-left) * (time-left_time) / gap))
    return tuple(output)
