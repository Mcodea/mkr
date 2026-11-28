"""Second passage de l'étude : statistiques E et C7 (amendement v1.1). python run_extras.py --results results_T1.jsonl --out extras_T1.jsonl
Reprend là où il s'est arrêté ; ne traite que les cas présents dans le fichier de résultats (mêmes (population, SNR, indice))."""
import argparse
import json
import multiprocessing as mp
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import extras  # noqa: E402


def job(item):
    key, controller = item
    try:
        return extras.run_extras(*key, controller=controller)
    except Exception as error:
        return dict(info=dict(population=key[0]), snr=key[1], index=key[2], crash=f"{type(error).__name__}: {error}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--results", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--workers", type=int, default=4)
    options = parser.parse_args()
    stored = {}
    for r in map(json.loads, Path(options.results).read_text(encoding="utf-8").splitlines()):
        if "gate" in r:
            stored[(r["info"]["population"], r["snr"], r["index"])] = r.get("controller")
    keys = list(stored)
    out = Path(options.out)
    done = set()
    if out.exists():
        done = {(r["info"]["population"], r["snr"], r["index"]) for r in map(json.loads, out.read_text(encoding="utf-8").splitlines())}
    todo = [(k, stored[k]) for k in keys if k not in done]
    print(f"{len(todo)} cas à calculer ({len(done)} déjà faits)", flush=True)
    started = time.time()
    with mp.Pool(options.workers) as pool, out.open("a", encoding="utf-8") as handle:
        for count, row in enumerate(pool.imap_unordered(job, todo, chunksize=4), 1):
            handle.write(json.dumps(row, default=float) + "\n")
            handle.flush()
            if count % 200 == 0:
                print(f"{count}/{len(todo)} en {time.time() - started:.0f} s", flush=True)


if __name__ == "__main__":
    main()
