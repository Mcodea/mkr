"""Diagnostic des données sur le jeu S01 (Two-Tank, v2.0), ensemble A UNIQUEMENT (protocole pré-enregistré, docs/science/lot4/protocole-evaluation-donnees-reelles.md).

Compare ce que détecte le moteur ORIAS (chemin de l'application : engine/csv_quality.process_csv) et la bibliothèque mkr_data_import.signal_quality
aux anomalies ANNOTÉES par les auteurs (Sensor_Dropout) et aux saturations de la commande (Control_Signal_pct >= 100). Aucune donnée n'est modifiée ;
les fichiers (30 600 lignes) dépassent les limites de l’import du moteur (10 000 lignes ET 1 MiB, c’est la taille en octets qui borne ici) et sont donc lus par fenêtres contiguës de 6 000 lignes (transformation
documentée, sans changer une valeur).

Usage : python -I s01_diagnostic.py CHEMIN/Dataset_v2.zip SORTIE.json
"""
import base64, csv, hashlib, io, json, sys, zipfile
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "engine")); sys.path.insert(0, str(ROOT))
from csv_quality import process_csv          # noqa: E402
from mkr_data_import.signal_quality import assess_signal  # noqa: E402

WINDOW = 6000
A_FILES = [line.split("\t")[0] for line in (ROOT / "docs/science/lot4/repartition-S01.tsv").read_text().splitlines() if line.endswith("\tA")]


def windows(lines):
    header, body = lines[0], lines[1:]
    for start in range(0, len(body), WINDOW):
        yield start, [header] + body[start:start + WINDOW]


def main(zip_path, out_path):
    z = zipfile.ZipFile(zip_path)
    report = dict(files={}, window_rows=WINDOW, a_files=A_FILES, zip_sha256=hashlib.sha256(open(zip_path, "rb").read()).hexdigest())
    for name in A_FILES:
        lines = z.read("Dataset/" + name).decode("utf-8").splitlines()
        rows = list(csv.DictReader(lines))
        dropout = [int(float(r["Sensor_Dropout"])) == 1 for r in rows]
        level = [Decimal(r["Level_pct"]) for r in rows]
        mv = [Decimal(r["Control_Signal_pct"]) for r in rows]
        entry = dict(rows=len(rows), annotated_dropouts=sum(dropout), mv_at_100=sum(v >= 100 for v in mv), mv_at_0=sum(v <= 0 for v in mv),
                     level_exact_zero=sum(v == 0 for v in level))
        # (1) chemin de l'application : qualité du CSV (fenêtres de 6 000 lignes)
        engine = dict(missing=0, invalid=0, invalid_timestamps=0, duplicates=0, reversed=0, max_step=0.0, min_step=1e9, windows=0)
        for _, chunk in windows(lines):
            raw = base64.b64encode("\n".join(chunk).encode()).decode()
            inspected = process_csv(dict(raw=raw, delimiter=","), True)
            payload = dict(raw=raw, delimiter=",", timeColumn="Timestamp", valueColumn="Level_pct", role="PV", unit="%", format=dict(decimal=inspected["suggestedFormat"]["decimal"], time=dict(pattern=inspected["suggestedFormat"]["timePattern"], zone="UTC")))
            q = process_csv(payload, False)["quality"]
            engine["windows"] += 1
            engine["missing"] += q["missingValues"]; engine["invalid"] += q["invalidValues"]; engine["invalid_timestamps"] += q["invalidTimestamps"]
            engine["duplicates"] += q["duplicateTimestamps"]; engine["reversed"] += q["reversedTimestamps"]
            engine["max_step"] = max(engine["max_step"], q["maxPositiveStepSeconds"] or 0); engine["min_step"] = min(engine["min_step"], q["minPositiveStepSeconds"] or 1e9)
        entry["engine_csv_quality"] = engine
        # (2) bibliothèque signal_quality : limites d'échelle déclarées a priori (0-100 %, comme les connaît un automaticien) ; AUCUN seuil réglé sur ces données
        sig_level = assess_signal(level, flat_tolerance=Decimal(0), saturation=(Decimal(0), Decimal(100)))
        sig_mv = assess_signal(mv, flat_tolerance=Decimal(0), saturation=(Decimal(0), Decimal(100)))
        flagged_level = set(sig_level.saturated or ())
        true_dropout = {i for i, d in enumerate(dropout) if d}
        entry["signal_quality"] = dict(
            level_flagged_at_scale_limit=len(flagged_level), dropouts_among_flagged=len(true_dropout & flagged_level), dropouts_missed=len(true_dropout - flagged_level),
            level_flagged_not_annotated=len(flagged_level - true_dropout), mv_flagged_saturated=len(sig_mv.saturated or ()), mv_at_100_or_0=sum(v >= 100 or v <= 0 for v in mv),
            level_frozen_whole_signal=sig_level.frozen)
        report["files"][name] = entry
    Path(out_path).write_text(json.dumps(report, indent=1, sort_keys=True))
    print(json.dumps(report["files"], indent=1)[:4000])


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
