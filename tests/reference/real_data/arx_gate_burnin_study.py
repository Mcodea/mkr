"""Vérification de la correction F-ARX-2 (arx 1.1.0 : amorçage de simulation écarté des tests de résidus) sur graines NEUVES 3000-3199, contre arx 1.0.0 (BURN_IN_DECAY = None).
Protocole : docs/science/lot4/protocole-critere-arx-automatique.md (addendum). Usage : python -I arx_gate_burnin_study.py ZIP_S03 SORTIE.json"""
import json, sys
from pathlib import Path
import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE)); sys.path.insert(0, str(HERE.parents[2] / "engine"))
import arx, arx_criteria, arx_criteria_study as st  # noqa: E402

SEEDS = range(3000, 3200)


def run_version(burn):
    arx.BURN_IN_DECAY = burn
    table = {}
    for pop in ("P1", "P2", "P3", "P4"):
        for sigma in st.SIGMAS:
            k_auto = k_fixed = 0
            for seed in SEEDS:
                ut, yt, uv, yv = st.case(pop, seed, sigma)
                cands = st.candidates(ut, yt, uv, yv)
                k_auto += st.accepted(cands, arx_criteria.maximum_rmse(yt, yv)[0]); k_fixed += st.accepted(cands, 0.10 * float(np.ptp(yv)))
            table[f"{pop}|{sigma}"] = dict(n=len(SEEDS), auto=st.cp(k_auto, len(SEEDS)), fixed10=st.cp(k_fixed, len(SEEDS)))
    return table


def s03(zip_path, burn):
    arx.BURN_IN_DECAY = burn
    import identification_s03_s04 as idn, zipfile, csv
    z = zipfile.ZipFile(zip_path)
    rows = [[float(x) for x in r[:4]] for r in csv.reader(z.read("CascadedTanksFiles/dataBenchmark.csv").decode().splitlines()[1:]) if r and r[0]]
    uEst, uVal, yEst, yVal = (np.array([r[i] for r in rows]) for i in range(4))
    return [dict(fraction=s["fraction_of_span"], decision=s.get("decision", s.get("error"))) for s in idn.run("S03", uEst, yEst, uVal, yVal, 4.0, 1e-3)["sweep"]]


def main(zip_s03, out):
    result = {}
    for name, burn in (("arx_1.0.0_sans_amorcage", None), ("arx_1.1.0_amorcage_1pct", 0.01)):
        result[name] = dict(table=run_version(burn), s03=s03(zip_s03, burn))
    arx.BURN_IN_DECAY = 0.01
    Path(out).write_text(json.dumps(result, indent=1, sort_keys=True))
    for name, d in result.items():
        print(name, "S03:", [x["decision"] for x in d["s03"]])
        for key, e in d["table"].items(): print("  ", key, "auto %.3f" % e["auto"]["rate"], "fixe10 %.3f" % e["fixed10"]["rate"])


if __name__ == "__main__":
    main(*sys.argv[1:3])
