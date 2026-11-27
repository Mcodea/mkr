"""Exécute l'étude : python run_study.py --tranche T1 --calibration 100 --evaluation 200 --out results_T1.jsonl

Reprise possible (les cas déjà écrits sont ignorés). Sortie : une ligne JSON par cas. Calibration : indices 0..n-1 ; évaluation : indices 1000..1000+n-1."""
import argparse
import json
import multiprocessing as mp
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import evaluate  # noqa: E402
import study  # noqa: E402


def job(args):
    try:
        return evaluate.run_case(*args)
    except Exception as error:     # un cas qui plante est ENREGISTRÉ, jamais écarté en silence
        return dict(info=dict(population=args[0]), snr=args[1], index=args[2], crash=f"{type(error).__name__}: {error}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--tranche", choices=("T1", "T2"), default="T1")
    parser.add_argument("--calibration", type=int, default=100)
    parser.add_argument("--evaluation", type=int, default=200)
    parser.add_argument("--out", default="results.jsonl")
    parser.add_argument("--workers", type=int, default=4)
    options = parser.parse_args()
    populations = study.POPULATIONS_T1 if options.tranche == "T1" else study.POPULATIONS_T2
    out = Path(options.out)
    done = set()
    if out.exists():
        for line in out.read_text(encoding="utf-8").splitlines():
            r = json.loads(line)
            done.add((r["info"]["population"], r["snr"], r["index"]))
    todo = [(p, snr, i) for p in populations for snr in study.SNRS for i in list(range(options.calibration)) + list(range(1000, 1000 + options.evaluation))
            if (p, snr, i) not in done]
    print(f"{len(todo)} cas à calculer ({len(done)} déjà faits)", flush=True)
    started = time.time()
    with mp.Pool(options.workers) as pool, out.open("a", encoding="utf-8") as handle:
        for count, row in enumerate(pool.imap_unordered(job, todo, chunksize=4), 1):
            handle.write(json.dumps(row, default=float) + "\n")
            handle.flush()
            if count % 100 == 0:
                print(f"{count}/{len(todo)} en {time.time() - started:.0f} s", flush=True)


if __name__ == "__main__":
    main()
