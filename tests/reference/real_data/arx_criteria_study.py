"""Évaluation de la règle d'erreur ARX automatique (engine/arx_criteria.py) contre des limites fixes, sur populations synthétiques à vérité de structure connue.
Protocole pré-enregistré : docs/science/lot4/protocole-critere-arx-automatique.md. Usage : python -I arx_criteria_study.py SORTIE.json"""
import base64, csv, io, json, sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
import numpy as np
from scipy.stats import beta

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "engine"))
import arx, arx_criteria  # noqa: E402

SIGMAS = (0.01, 0.04, 0.10); SEEDS = range(1000, 1200); FIXED = (0.05, 0.10, 0.20); N = 600
ADEQUATE, INADEQUATE = ("P1", "P2"), ("P3", "P4")


def prbs(n, rng, levels=(-1.0, 1.0), p=0.05):
    u = np.empty(n); v = levels[1]
    for k in range(n):
        if rng.random() < p: v = levels[int(rng.integers(len(levels)))]
        u[k] = v
    return u


def lag(u, tau, theta, gain=1.0):
    a = np.exp(-1.0 / tau); y = np.zeros(len(u)); x = 0.0
    for k in range(len(u)):
        x = a * x + gain * (1 - a) * (u[k - theta] if k >= theta else 0.0); y[k] = x
    return y


def lag_chain(u, tau, theta, n_lags, gain=1.0):
    y = u.copy()
    for _ in range(n_lags): y = lag(y, tau, 0)
    out = np.zeros(len(u)); out[theta:] = y[:len(u) - theta] if theta else y
    return gain * out


def case(pop, seed, sigma):
    rng = np.random.default_rng(seed * 7919 + int(sigma * 1000))
    K = rng.uniform(0.5, 2.0); tau = float(np.exp(rng.uniform(np.log(5), np.log(30)))); theta = int(rng.integers(1, 9))
    if pop == "P1": f = lambda u, kk=1.0: lag(u, tau, theta, K * kk); levels = (-1.0, 1.0)
    elif pop == "P2":
        tau2 = tau * rng.uniform(0.1, 0.5); f = lambda u, kk=1.0: lag(lag(u, tau, theta, K * kk), tau2, 0); levels = (-1.0, 1.0)
    elif pop == "P3": f = lambda u, kk=1.0: lag(u + 0.6 * u ** 3, tau, theta, K); levels = (-1.0, -0.5, 0.5, 1.0)
    elif pop == "P4": f = lambda u, kk=1.0: lag(u, tau, theta, K * kk); levels = (-1.0, 1.0)
    elif pop == "P5": f = lambda u, kk=1.0: lag_chain(u, 10.0, theta, 4, K); levels = (-1.0, 1.0)
    ut, uv = prbs(N, rng, levels), prbs(N, rng, levels)
    yt = f(ut) + rng.normal(0, sigma, N); yv = f(uv, 1.25 if pop == "P4" else 1.0) + rng.normal(0, sigma, N)
    return ut, yt, uv, yv


def src(u, y):
    t0 = datetime(2000, 1, 1, tzinfo=timezone.utc); out = io.StringIO(); w = csv.writer(out, lineterminator="\n"); w.writerow(["t", "u", "y"])
    for k, (a, b) in enumerate(zip(u, y)): w.writerow([(t0 + timedelta(seconds=k)).strftime("%Y-%m-%dT%H:%M:%SZ"), repr(float(a)), repr(float(b))])
    return dict(raw=base64.b64encode(out.getvalue().encode()).decode(), delimiter=",", timeColumn="t", mvColumn="u", pvColumn="y")


def candidates(ut, yt, uv, yv):
    try:
        r = arx.identify(dict(training=src(ut, yt), validation=src(uv, yv), mvTolerance=1e-3, maximumRmse=1e100, loop="open"))
    except ValueError:
        return []
    return [(c["parameters"], c["validationSimulationRmse"], [x for x in c["reasons"] if x != "validation_simulation_rmse"]) for c in r["candidates"] if c["validationSimulationRmse"] is not None]


def accepted(cands, limit):
    return any(not other and rmse <= limit for _, rmse, other in cands)


def cp(k, n):
    if n == 0: return None
    lo = 0.0 if k == 0 else float(beta.ppf(0.025, k, n - k + 1)); hi = 1.0 if k == n else float(beta.ppf(0.975, k + 1, n - k)); up = 1.0 if k == n else float(beta.ppf(0.95, k + 1, n - k))
    return dict(k=k, n=n, rate=k / n, ci95=[lo, hi], upper95_onesided=up)


def main(out):
    rules = ["auto_3sigma"] + [f"fixed_{int(f * 100)}pct" for f in FIXED]
    table = {}
    for pop in ("P1", "P2", "P3", "P4", "P5"):
        for sigma in SIGMAS:
            counts = {r: 0 for r in rules}; total = 0; est = []
            for seed in SEEDS:
                ut, yt, uv, yv = case(pop, seed, sigma)
                cands = candidates(ut, yt, uv, yv); total += 1
                limit_auto, s_hat = arx_criteria.maximum_rmse(yt, yv); est.append(s_hat / sigma)
                span = float(np.ptp(yv))
                counts["auto_3sigma"] += accepted(cands, limit_auto)
                for f in FIXED: counts[f"fixed_{int(f * 100)}pct"] += accepted(cands, f * span)
            table[f"{pop}|{sigma}"] = dict(n=total, sigma_hat_over_sigma_median=float(np.median(est)), **{r: cp(counts[r], total) for r in rules})
    verdict = {}
    for r in rules:
        fp = {sg: cp(sum(table[f"{p}|{sg}"][r]["k"] for p in INADEQUATE), sum(table[f"{p}|{sg}"][r]["n"] for p in INADEQUATE)) for sg in SIGMAS}
        power = {sg: cp(sum(table[f"{p}|{sg}"][r]["k"] for p in ADEQUATE), sum(table[f"{p}|{sg}"][r]["n"] for p in ADEQUATE)) for sg in SIGMAS}
        verdict[r] = dict(false_positive_structure=fp, power_adequate=power, criterion_i=all(v["upper95_onesided"] <= 0.05 for v in fp.values()))
    Path(out).write_text(json.dumps(dict(table=table, verdict=verdict, arx=arx.VERSION, criteria=arx_criteria.VERSION), indent=1, sort_keys=True))
    for r in rules:
        v = verdict[r]; print(r, "(i)", v["criterion_i"])
        for sg in SIGMAS: print("   sigma", sg, "FP structure %.3f (up95 %.3f)" % (v["false_positive_structure"][sg]["rate"], v["false_positive_structure"][sg]["upper95_onesided"]), "| puissance %.3f" % v["power_adequate"][sg]["rate"])
    for key, e in table.items(): print(key, "sigma_hat/sigma=%.2f" % e["sigma_hat_over_sigma_median"], {r: round(e[r]["rate"], 3) for r in rules})


if __name__ == "__main__":
    main(sys.argv[1])
