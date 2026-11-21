"""Generate deterministic FOPDT reference CSVs and their ground truth."""
from datetime import datetime, timedelta, timezone
import csv
from pathlib import Path
import json
from math import expm1
from random import Random


CASES = {
    "clean": {},
    "noisy": {"noise": 0.08},
    "negative_gain": {"gain": -2.0},
    "long_delay": {"delay": 30.0},
    "saturated": {"upper_limit": 1.0},
    "irregular": {"irregular": True},
    "unexcited": {"step": 0.0},
}


def generate(directory: Path):
    directory.mkdir(parents=True, exist_ok=True)
    reference = {}
    epoch = datetime(2026, 1, 1, tzinfo=timezone.utc)
    for name, overrides in CASES.items():
        case = {
            "gain": 2.0, "delay": 4.0, "tau": 12.0, "step_time": 10,
            "step": 1.0, "noise": 0.0, "upper_limit": None,
            "irregular": False, "seed": 2026, "duration": 150,
            "units": {"MV": "%", "PV": "bar"},
            **overrides,
        }
        rng = Random(case["seed"])
        with (directory / f"{name}.csv").open("w", newline="", encoding="utf-8") as stream:
            writer = csv.writer(stream, lineterminator="\n")
            writer.writerow(("timestamp", "MV", "PV"))
            for second in range(case["duration"] + 1):
                if case["irregular"] and second % 7 == 1:
                    continue
                elapsed = max(0, second - case["step_time"] - case["delay"])
                mv = case["step"] if second >= case["step_time"] else 0
                pv = -case["gain"] * case["step"] * expm1(-elapsed / case["tau"])
                if case["upper_limit"] is not None:
                    pv = min(case["upper_limit"], pv)
                pv += rng.gauss(0, case["noise"])
                writer.writerow(((epoch + timedelta(seconds=second)).isoformat(), mv, f"{pv:.9f}"))
        reference[name] = case
    (directory / "truth.json").write_text(json.dumps(reference, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    generate(Path(__file__).parent)
