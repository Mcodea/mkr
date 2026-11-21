"""Assign imported columns to engineering roles without inferring absent signals."""
from dataclasses import dataclass
from datetime import datetime
from decimal import Decimal
from typing import Mapping

from .io.csv_import import ImportResult


@dataclass(frozen=True)
class MappedSignals:
    timestamps: tuple[datetime, ...]
    values: Mapping[str, tuple[Decimal | str, ...]]
    units: Mapping[str, str]


def map_signals(source: ImportResult, roles: Mapping[str, str]) -> MappedSignals:
    """Require timestamp, controlled measurement PV and applied command MV."""
    allowed_roles = {"timestamp", "SP", "PV", "MV", "disturbance", "position", "mode"}
    if not {"timestamp", "PV", "MV"} <= roles.keys() or not roles.keys() <= allowed_roles:
        raise ValueError("Assign timestamp, PV and MV using supported signal roles.")
    if any(not isinstance(column, str) or not column for column in roles.values()):
        raise ValueError("Each assigned role requires an explicit column name.")
    if len(set(roles.values())) != len(roles):
        raise ValueError("A source column cannot be assigned to multiple roles.")
    if roles["timestamp"] != source.metadata.get("timestamp_column"):
        raise ValueError("The timestamp role must use the column parsed as a timestamp.")
    if source.errors or not source.records:
        raise ValueError("Signal mapping requires an import with records and no parsing errors.")
    declared_units = source.metadata["columns"]
    units, signals = {}, {}
    for role, column in roles.items():
        if role == "timestamp":
            continue
        if role == "mode":
            if column not in source.headers:
                raise ValueError("The declared mode column is absent.")
            index = source.headers.index(column)
            values = tuple(row[index] if index < len(row) else None for row in source.rows)
            if any(not isinstance(value, str) or not value.strip() for value in values):
                raise ValueError("Mode values must be explicit nonempty labels.")
            signals[role] = values
            continue
        unit = declared_units.get(column)
        if not unit:
            raise ValueError(f"The column assigned to {role} requires a declared unit.")
        signals[role] = tuple(record["values"][column] for record in source.records)
        units[role] = unit

    timestamps = tuple(record["timestamp"] for record in source.records)
    return MappedSignals(timestamps, signals, units)
