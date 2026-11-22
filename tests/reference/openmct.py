"""Inventory physical motor records from Von Chong & Cardenas, DOI 10.17632/5xvg43r9r8.2."""
import argparse
import csv
from io import BytesIO, TextIOWrapper
from itertools import groupby
from pathlib import PurePosixPath
from urllib.request import urlopen
from zipfile import ZipFile


URL = "https://api.data.mendeley.com/datasets/5xvg43r9r8/zip/file_downloaded?version=2"


def inspect(archive):
    paths = [name for name in archive.namelist()
             if "03_system_identification" in PurePosixPath(name).parts
             and PurePosixPath(name).name.lower().startswith("raw_data")
             and name.lower().endswith(".txt")]
    if len(paths) < 2:
        raise ValueError("Expected at least two raw identification records.")
    for path in sorted(paths):
        with archive.open(path) as raw, TextIOWrapper(raw, encoding="utf-8-sig") as stream:
            metadata = [next(stream).strip() for _ in range(2)]
            reader = csv.DictReader(stream, skipinitialspace=True)
            if not {"PWM", "MEAS", "DT_ms"}.issubset(reader.fieldnames or []):
                raise ValueError(f"{path}: unexpected columns {reader.fieldnames}")
            levels = [(value, sum(1 for _ in group)) for value, group in
                      groupby(reader, key=lambda row: float(row["PWM"]))]
            if len(levels) < 2:
                raise ValueError(f"{path}: no recorded change in actuation")
            print(path, metadata, "samples", sum(n for _, n in levels), "first plateaus", levels[:12])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", nargs="?", help="Published OpenMCT version 2 ZIP")
    args = parser.parse_args()
    if args.archive:
        with ZipFile(args.archive) as archive:
            inspect(archive)
    else:
        with urlopen(URL, timeout=90) as response, ZipFile(BytesIO(response.read())) as archive:
            inspect(archive)
