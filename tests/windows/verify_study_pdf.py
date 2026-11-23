import sys
from pathlib import Path
from uuid import UUID
from pypdf import PdfReader

path = Path(sys.argv[1])
pages = PdfReader(path).pages
simulation = str(UUID(path.stem.removeprefix("MKR-")))
body = " ".join(" ".join(page.extract_text().split()) for page in pages[:-1])
for expected in (
    "Fiche de boucle du projet au moment de l’export",
    "Repère : PIC-202",
    "Grandeur régulée : Pression de refoulement",
    "Point de fonctionnement : 5 bar",
    "Objectif : Maintenir la pression aval",
    "Description : Procédé d’essai",
    "Consigne en écart 1 bar",
    "Vitesse maximale de commande 0,5 %/s.",
):
    if expected not in body:
        raise ValueError(f"Missing saved condition {expected!r} in {body!r}")
for label, filename in (("Estimation", "essai-1.csv"), ("Validation", "essai-2.csv")):
    start = body.index(f"{label} · {filename}")
    end = body.index("SHA-256", start)
    trial = body[start:end]
    for expected in (
        "Temps : timestamp · commande MV : mv · mesure PV : pv",
        "Plage utilisée : fichier complet",
        "échantillons",
        "RMSE :",
        "erreur absolue moyenne :",
    ):
        if expected not in trial:
            raise ValueError(f"Missing {expected!r} in {label}: {trial!r}")
if simulation not in body or simulation not in pages[-1].extract_text():
    raise ValueError("The report, plot and CSV must identify the same simulation")
