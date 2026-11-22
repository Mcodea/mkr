"""Physical motor records, Von Chong and Cardenas, DOI 10.17632/5xvg43r9r8.2."""
from collections import Counter
import csv
from io import BytesIO
from itertools import groupby
from urllib.request import urlopen
from zipfile import ZipFile

URL = "https://data.mendeley.com/public-files/datasets/5xvg43r9r8/versions/2/downloads/dataset.zip"

with urlopen(URL, timeout=90) as response:
    archive = ZipFile(BytesIO(response.read()))
for path in archive.namelist():
    if "03_system_identification/" in path and path.lower().endswith("raw_data.txt"):
        lines = archive.read(path).decode("utf-8-sig").splitlines()
        print(path, len(lines), lines[:2])
        rows = list(csv.reader(lines[1:]))
        print([(v, len(list(g))) for v, g in groupby(rows, key=lambda r: r[5])][:20])
