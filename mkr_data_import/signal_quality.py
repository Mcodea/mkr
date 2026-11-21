"""Describe signal anomalies using explicit thresholds in the signal's units."""
from dataclasses import dataclass
from decimal import Decimal
from statistics import median
from typing import Sequence


@dataclass(frozen=True)
class SignalQuality:
    invalid: tuple[int, ...]
    outliers: tuple[int, ...] | None
    saturated: tuple[int, ...] | None
    mode_changes: tuple[int, ...] | None
    frozen: bool | None
    high_variation: bool | None
    insufficient_excitation: bool | None


def assess_signal(
    values: Sequence[Decimal | None],
    *,
    flat_tolerance: Decimal,
    saturation: tuple[Decimal, Decimal] | None = None,
    outlier_distance: Decimal | None = None,
    variation_limit: Decimal | None = None,
    excitation_span: Decimal | None = None,
    modes: Sequence[str] | None = None,
) -> SignalQuality:
    """Variation describes adjacent samples and does not isolate sensor noise."""
    thresholds = (flat_tolerance, outlier_distance, variation_limit, excitation_span)
    if any(value is not None and (not value.is_finite() or value < 0) for value in thresholds):
        raise ValueError("Thresholds must be finite, nonnegative and expressed in signal units.")
    if saturation is not None:
        low, high = saturation
        if not low.is_finite() or not high.is_finite() or low >= high:
            raise ValueError("Expected increasing finite saturation limits.")
    if modes is not None and (len(modes) != len(values) or any(not mode.strip() for mode in modes)):
        raise ValueError("Declared modes must correspond to all samples.")

    valid = {index: value for index, value in enumerate(values)
             if value is not None and value.is_finite()}
    invalid = tuple(index for index in range(len(values)) if index not in valid)
    complete = len(valid) == len(values) and len(values) >= 2
    span = max(valid.values()) - min(valid.values()) if complete else None
    center = median(valid.values()) if outlier_distance is not None and len(valid) >= 3 else None
    outliers = (
        tuple(index for index, value in valid.items() if abs(value - center) > outlier_distance)
        if center is not None else None
    )
    saturated = (
        tuple(index for index, value in valid.items() if value <= low or value >= high)
        if saturation is not None and valid else None
    )
    mode_changes = (
        tuple(index for index in range(1, len(modes)) if modes[index] != modes[index - 1])
        if modes else None
    )
    high_variation = (
        median(abs(valid[index] - valid[index - 1]) for index in range(1, len(values))) > variation_limit
        if complete and variation_limit is not None else None
    )
    return SignalQuality(
        invalid, outliers, saturated, mode_changes,
        span <= flat_tolerance if span is not None else None,
        high_variation,
        span < excitation_span if span is not None and excitation_span is not None else None,
    )
