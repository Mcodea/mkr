"""Évaluation du diagnostic automatique (engine/signal_diagnosis.py v1.0.0) sur S01, ensembles B (calibration) et C (validation intermédiaire), séparément.
Règles et paramètres fixés avant ce script (commit précédent). Aucune règle n'est ajustée ici. Usage : python -I s01_diagnosis_eval.py Dataset_v2.zip SORTIE.json"""
import csv, hashlib, json, sys, zipfile
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "engine"))
import signal_diagnosis as sd  # noqa: E402

SPLIT = dict(line.split("\t") for line in (ROOT / "docs/science/lot4/repartition-S01.tsv").read_text().splitlines())


def main(zip_path, out):
    z = zipfile.ZipFile(zip_path)
    result = dict(engine="signal_diagnosis 1.0.0", zip_sha256=hashlib.sha256(open(zip_path, "rb").read()).hexdigest(), sets={})
    for subset in ("B", "C"):
        files, agg = {}, dict(tp=0, fn=0, fp=0, truth=0)
        for name in sorted(n for n, s in SPLIT.items() if s == subset):
            rows = list(csv.DictReader(z.read("Dataset/" + name).decode().splitlines()))
            level = [float(r["Level_pct"]) for r in rows]; mv = [float(r["Control_Signal_pct"]) for r in rows]
            stamps = [datetime.strptime(r["Timestamp"], "%Y-%m-%d %H:%M:%S").timestamp() for r in rows]
            truth = {i for i, r in enumerate(rows) if int(float(r["Sensor_Dropout"])) == 1}
            pv = sd.diagnose(stamps, level, "PV"); mvd = sd.diagnose(stamps, mv, "MV")
            flagged = set(pv["possibleSensorDropout"])
            tp, fn, fp = len(flagged & truth), len(truth - flagged), len(flagged - truth)
            true_sat = sum(v >= 100 or v <= 0 for v in mv) / len(mv)
            bounds = mvd["saturation"].get("bounds", [])
            files[name] = dict(rows=len(rows), annotated_dropouts=len(truth), dropout_tp=tp, dropout_fn=fn, dropout_fp=fp,
                               mv_true_fraction_at_bound=round(true_sat, 4), mv_reported_fraction=round(mvd["saturation"]["fraction"], 4),
                               mv_reported_levels=[(b["side"], b["level"]) for b in bounds], pv_reported_saturation_fraction=round(pv["saturation"]["fraction"], 4),
                               duplicate_timestamps=pv["cadence"]["duplicate_timestamps"], clock_coarser=pv["cadence"]["clock_resolution_coarser_than_sampling"])
            for k, v in (("tp", tp), ("fn", fn), ("fp", fp), ("truth", len(truth))): agg[k] += v
        result["sets"][subset] = dict(files=files, dropout_totals=agg)
    Path(out).write_text(json.dumps(result, indent=1, sort_keys=True))
    for subset, d in result["sets"].items():
        a = d["dropout_totals"]; print(subset, a, "recall=%.4f" % (a["tp"] / a["truth"]) if a["truth"] else "", "precision=%.4f" % (a["tp"] / (a["tp"] + a["fp"])) if a["tp"] + a["fp"] else "")
        for n, f in d["files"].items(): print("  ", n.split("/")[1], {k: f[k] for k in ("annotated_dropouts", "dropout_tp", "dropout_fn", "dropout_fp", "mv_true_fraction_at_bound", "mv_reported_fraction", "mv_reported_levels", "duplicate_timestamps")})


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
