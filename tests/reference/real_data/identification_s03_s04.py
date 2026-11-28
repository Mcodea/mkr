"""Évaluation de l'identification ARX (engine/arx.py) sur S03 (Cascaded Tanks) et S04 (Coupled-Tank), selon le mode opératoire pré-enregistré
(docs/science/lot4/protocole-evaluation-donnees-reelles.md §6). Usage : python -I identification_s03_s04.py ZIP_S03 DOSSIER_S04 SORTIE.json"""
import base64, csv, hashlib, io, json, sys, zipfile
from datetime import datetime, timedelta, timezone
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "engine"))
import arx  # noqa: E402

FRACTIONS = (0.02, 0.05, 0.10, 0.20)


def raw_csv(u, y, ts):
    t0 = datetime(2000, 1, 1, tzinfo=timezone.utc)
    out = io.StringIO(); w = csv.writer(out, lineterminator="\n"); w.writerow(["t", "u", "y"])
    for k, (a, b) in enumerate(zip(u, y)):
        w.writerow([(t0 + timedelta(seconds=k * ts)).strftime("%Y-%m-%dT%H:%M:%SZ"), repr(float(a)), repr(float(b))])
    return base64.b64encode(out.getvalue().encode()).decode()


def source(u, y, ts):
    return dict(raw=raw_csv(u, y, ts), delimiter=",", timeColumn="t", mvColumn="u", pvColumn="y")


def run(label, ut, yt, uv, yv, ts, mv_tol):
    span = float(np.ptp(yv)); rows = []
    for f in FRACTIONS:
        try:
            r = arx.identify(dict(training=source(ut, yt, ts), validation=source(uv, yv, ts), mvTolerance=mv_tol, maximumRmse=f * span, loop="open"))
            sel = r.get("selected")
            best = min((c for c in r["candidates"] if c["validationSimulationRmse"] is not None), key=lambda c: c["validationSimulationRmse"], default=None)
            rows.append(dict(fraction_of_span=f, decision=r["decision"], selected=None if not sel else dict(na=sel["na"], nb=sel["nb"], nk=sel["nk"], estimator=sel["estimator"],
                             simulation_rmse=sel["validationSimulationRmse"], nrmse=sel["validationSimulationRmse"] / span),
                             reasons=r.get("reasons"), best_candidate_rmse=None if not best else best["validationSimulationRmse"],
                             best_candidate_nrmse=None if not best else best["validationSimulationRmse"] / span))
        except ValueError as e:
            rows.append(dict(fraction_of_span=f, error=str(e)))
    return dict(label=label, validation_span=span, constant_predictor_rmse=float(np.std(yv)), samples_training=len(yt), samples_validation=len(yv), ts=ts, sweep=rows)


def main(zip_s03, dir_s04, out):
    results = dict(engine="arx " + arx.VERSION, runs=[])
    z = zipfile.ZipFile(zip_s03)
    lines = z.read("CascadedTanksFiles/dataBenchmark.csv").decode().splitlines()
    data = [[float(x) for x in r[:4]] for r in csv.reader(lines[1:]) if r and r[0]]
    uEst, uVal, yEst, yVal = (np.array([r[i] for r in data]) for i in range(4))
    results["s03_sha256"] = hashlib.sha256(open(zip_s03, "rb").read()).hexdigest()
    results["runs"].append(run("S03 cascaded tanks : estimation -> test (auteurs)", uEst, yEst, uVal, yVal, 4.0, 1e-3))
    c = np.array([[float(x) for x in r] for r in list(csv.reader(open(Path(dir_s04) / "complete_data.csv")))[1:]])
    h1, h2, u = c[:, 1], c[:, 2], c[:, 3]
    results["s04_sha256"] = hashlib.sha256(open(Path(dir_s04) / "complete_data.csv", "rb").read()).hexdigest()
    for k, (a, b) in enumerate(((1, 7), (2, 8), (3, 9))):
        sa, sb = 476 + 425 * (a - 1), 476 + 425 * (b - 1)
        for name, y in (("h1", h1), ("h2", h2)):
            results["runs"].append(run(f"S04 u -> {name} : répétition {a} (A) -> {b} (C)", u[sa:sa + 81], y[sa:sa + 81], u[sb:sb + 81], y[sb:sb + 81], 4.0, 1e-3))
    Path(out).write_text(json.dumps(results, indent=1, sort_keys=True, default=float))
    for r in results["runs"]:
        print(r["label"], "| étendue %.3f | prédicteur constant RMSE %.3f" % (r["validation_span"], r["constant_predictor_rmse"]))
        for s in r["sweep"]:
            print("   ", s.get("fraction_of_span"), s.get("decision", s.get("error")), s.get("selected") and (s["selected"]["na"], s["selected"]["nb"], s["selected"]["nk"], s["selected"]["estimator"], round(s["selected"]["nrmse"], 4)), "meilleur candidat nrmse=", s.get("best_candidate_nrmse") and round(s["best_candidate_nrmse"], 4), s.get("reasons"))


if __name__ == "__main__":
    main(*sys.argv[1:4])
