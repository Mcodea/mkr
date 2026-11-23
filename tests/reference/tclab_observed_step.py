"""Check the observed-step requirement on the published Notre Dame TCLab recording.

Source: https://ndcbe.github.io/controls/tclab/01.02-first-order-model-for-a-single-heater.html
Input: tclab/data/tclab-data.csv at bec88ef9f6d9f8dcf1b24d87c52b5875fc1ffeb0 in jckantor/CBE30338-book.
Elapsed seconds are placed on an arbitrary UTC origin exclusively for the reader.
"""
import base64
import csv
from datetime import datetime, timedelta, timezone
from io import StringIO
from pathlib import Path
import sys

from fopdt import read_step


def verify(path):
    origin = datetime(2000, 1, 1, tzinfo=timezone.utc)
    normalized = StringIO()
    writer = csv.writer(normalized)
    writer.writerow(["timestamp", "Q1", "T1"])
    with path.open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    assert len(rows) == 800
    assert {float(row["Q1"]) for row in rows} == {50.0}
    assert float(rows[-1]["T1"]) > float(rows[0]["T1"])
    for row in rows:
        stamp = origin + timedelta(seconds=float(row["Time"]))
        writer.writerow([stamp.isoformat(), row["Q1"], row["T1"]])
    source = dict(raw=base64.b64encode(normalized.getvalue().encode()).decode(),
                  delimiter=",", timeColumn="timestamp", mvColumn="Q1", pvColumn="T1")
    try:
        read_step(source, input_tolerance=0.01, output_tolerance=0.01)
    except ValueError as error:
        if error.args != ("fit_step",):
            raise
    else:
        raise AssertionError("MKR accepted a recording without an observed input step")
    print("TCLab: 800 physical observations, constant heater input, fit_step confirmed.")


if __name__ == "__main__":
    verify(Path(sys.argv[1]))
