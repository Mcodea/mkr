import sys
from pathlib import Path
from uuid import UUID
from pypdf import PdfReader

path = Path(sys.argv[1])
pages = PdfReader(path).pages
simulation = str(UUID(path.stem.removeprefix("MKR-")))
body = " ".join(" ".join(page.extract_text().split()) for page in pages[:-1])
for expected in ("Consigne en écart 1 bar", "Vitesse maximale de commande 0,5 %/s."):
    if expected not in body:
        raise ValueError(f"Missing saved condition {expected!r} in {body!r}")
if simulation not in body or simulation not in pages[-1].extract_text():
    raise ValueError("The report, plot and CSV must identify the same simulation")
