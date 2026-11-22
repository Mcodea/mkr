"""Inventory the two open-loop archives published under DOI 10.7481/1963162."""
from collections import Counter
from hashlib import sha256
from io import BytesIO
from pathlib import PurePosixPath
from urllib.request import urlopen
from zipfile import ZipFile


ARCHIVES = {
    "Dataset2_2021_06_OpenLoopExperiments.zip": "5f2f237f1b90ab7efd240dfd993558d767a8342c095608492baad1f368569d97",
    "Dataset4_2021_08_OpenLoopExperiments.zip": "2d2906bdfd93019ceb1fa4ff495ff428acd47209e2a2f634b27ba08bd792057d",
}


def inspect(name, expected_digest):
    with urlopen("https://data.openei.org/files/6474/" + name, timeout=60) as response:
        data = response.read()
    if sha256(data).hexdigest() != expected_digest:
        raise ValueError(f"Source revision changed: {name}")
    print(name, expected_digest)
    with ZipFile(BytesIO(data)) as archive:
        files = [PurePosixPath(entry.filename) for entry in archive.infolist() if not entry.is_dir()]
        for directory, count in sorted(Counter(p.parent for p in files if p.suffix.lower() == ".csv").items()):
            print(directory, count, "CSV")
        for path in files:
            if path.name.lower() == "readme.txt":
                print(path)
                print(archive.read(str(path)).decode("utf-8-sig"))


if __name__ == "__main__":
    for archive_name, digest in ARCHIVES.items():
        inspect(archive_name, digest)
