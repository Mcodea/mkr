"""Inspect the published NAWI open-loop archives. DOI 10.7481/1963162."""
from hashlib import sha256
from io import BytesIO
from pathlib import PurePosixPath
from urllib.request import urlopen
from zipfile import ZipFile

from openpyxl import load_workbook
from pypdf import PdfReader


def inspect(archive_name):
    with urlopen("https://data.openei.org/files/6474/" + archive_name, timeout=60) as response:
        data = response.read()
    print(archive_name, "SHA256", sha256(data).hexdigest())
    with ZipFile(BytesIO(data)) as archive:
        for entry in archive.infolist():
            path = PurePosixPath(entry.filename)
            if entry.is_dir() or "__MACOSX" in path.parts:
                continue
            print(path, entry.file_size)
            if "readme" in path.name.lower():
                raw = archive.read(entry)
                if path.suffix.lower() == ".pdf":
                    for page in PdfReader(BytesIO(raw)).pages:
                        print(page.extract_text())
                elif path.suffix.lower() in {".txt", ".md"}:
                    print(raw.decode("utf-8", errors="replace"))
            if path.suffix.lower() == ".xlsx":
                workbook = load_workbook(BytesIO(archive.read(entry)), read_only=True, data_only=True)
                try:
                    for sheet in workbook:
                        print(sheet.title, sheet.max_row, sheet.max_column)
                        for row in sheet.iter_rows(max_row=3, values_only=True):
                            print(row)
                finally:
                    workbook.close()


if __name__ == "__main__":
    for name in ("Dataset2_2021_06_OpenLoopExperiments.zip", "Dataset4_2021_08_OpenLoopExperiments.zip"):
        inspect(name)
