"""Analyse de l'étude selon la règle d'adoption PRÉ-ENREGISTRÉE (docs/science/lot4/protocole-validation-critere.md §7). Aucun seuil n'est choisi ici après coup :
  * définitions d'« apte » : M_lim dans {1,7 ; 2,0 (primaire) ; 2,5 ; 3,0}, stable exigé ; sensibilité secondaire : + perte ITAE <= 2 ;
  * D est calibré sur le jeu de CALIBRATION (indices < 1000) puis évalué sur le jeu d'ÉVALUATION (indices >= 1000) ;
  * intervalles : bootstrap de cas (B = 2000, graine fixe), différence appariée avec A, niveau de Bonferroni 0,05 / k.
python analysis.py results_T1.jsonl [--out rapport.json]"""
import argparse
import json
import math
import sys

import numpy as np

M_LIMITS = (1.7, 2.0, 2.5, 3.0)
PRIMARY = 2.0
ALPHA_RISK = 0.05                # risque d'acceptation d'un modèle inapte : décision d'ingénierie du protocole §6
BOOTSTRAP = 2000
BASE_NAMES = ("A", "A1", "A2", "B", "C", "D", "A_or_D")
CANDIDATES = ("A1", "A2", "B", "C", "D", "A_or_D")          # comparés à A : k = 6


def load(path):
    rows = [json.loads(line) for line in open(path, encoding="utf-8")]
    return rows


def blocking(gate):
    return any(r not in ("validation_rmse", "validation_residuals") for r in gate["reasons"])


def decisions(row, s_d):
    g, s = row["gate"], row["stats"]
    block = blocking(g)
    rmse_ok = "validation_rmse" not in g["reasons"]
    out = {}
    out["A"] = bool(g["accepted"])
    out["A1"] = bool(rmse_ok and not block)
    out["A2"] = bool("validation_residuals" not in g["reasons"] and not block)
    out["B"] = bool(rmse_ok and s["ljung_box_p"] >= 0.01 and not block)
    out["C"] = bool(rmse_ok and s["cross_max"] <= s["cross_bound"] and not block)
    out["D"] = bool(s["nse"] is not None and s_d is not None and s["nse"] <= s_d and not block)
    out["A_or_D"] = bool(out["A"] or out["D"])
    return out


def label(row, m_lim, itae_factor=None):
    t = row.get("truth")
    if t is None:
        return None
    fit = bool(t["stable"] and t["ms"] is not None and t["ms"] <= m_lim)
    if fit and itae_factor is not None:
        ref = row.get("reference")
        if not ref or not ref.get("stable") or not ref.get("itae") or not t.get("itae"):
            return None
        fit = t["itae"] <= itae_factor * ref["itae"]
    return fit


def calibrate_d(rows):
    """s_D = plus grand seuil NSE tel que P(accepter | inapte) <= ALPHA_RISK sur le jeu de calibration (définition primaire)."""
    unfit = [r["stats"]["nse"] for r in rows if label(r, PRIMARY) is False and r["stats"]["nse"] is not None and not blocking(r["gate"])]
    if len(unfit) < 20:
        return None, len(unfit)
    unfit = np.sort(unfit)
    k = int(math.floor(ALPHA_RISK * len(unfit)))
    return (float(unfit[k - 1]) if k >= 1 else 0.0), len(unfit)


def rates(accept, fit):
    accept, fit = np.asarray(accept), np.asarray(fit)
    unfit_idx, fit_idx = ~fit, fit
    unsafe = float(accept[unfit_idx].mean()) if unfit_idx.any() else float("nan")
    false_rej = float((~accept[fit_idx]).mean()) if fit_idx.any() else float("nan")
    return unsafe, false_rej


def bootstrap_differences(acc_a, acc_x, fit, rng):
    n = len(fit)
    idx = rng.integers(0, n, size=(BOOTSTRAP, n))
    d_unsafe, d_rej = [], []
    for sample in idx:
        f = fit[sample]
        a, x = acc_a[sample], acc_x[sample]
        ua, ra = rates(a, f)
        ux, rx = rates(x, f)
        d_unsafe.append(ux - ua)
        d_rej.append(rx - ra)
    return np.array(d_unsafe), np.array(d_rej)


def evaluate(rows_eval, s_d, m_lim, itae_factor=None, seed=20261127):
    usable = [r for r in rows_eval if label(r, m_lim, itae_factor) is not None]
    fit = np.array([label(r, m_lim, itae_factor) for r in usable])
    dec = [decisions(r, s_d) for r in usable]
    acc = {name: np.array([d[name] for d in dec]) for name in BASE_NAMES}
    out = dict(n=len(usable), n_fit=int(fit.sum()), n_unfit=int((~fit).sum()), by={})
    rng = np.random.default_rng(seed)
    k = len(CANDIDATES)
    z = _z(1 - 0.05 / k / 2)
    for name in BASE_NAMES:
        u, r = rates(acc[name], fit)
        out["by"][name] = dict(unsafe_acceptance=u, false_rejection=r)
    for name in CANDIDATES:
        du, dr = bootstrap_differences(acc["A"], acc[name], fit, rng)
        lo_u, hi_u = np.quantile(du, [0.05 / k / 2, 1 - 0.05 / k / 2])
        lo_r, hi_r = np.quantile(dr, [0.05 / k / 2, 1 - 0.05 / k / 2])
        out["by"][name].update(diff_unsafe=dict(est=float(np.mean(du)), lo=float(lo_u), hi=float(hi_u)),
                               diff_false_rejection=dict(est=float(np.mean(dr)), lo=float(lo_r), hi=float(hi_r)))
    return out


def _z(p):
    from scipy.stats import norm
    return float(norm.ppf(p))


def adopt(table):
    """Règle §7 i-iii pour chaque candidat ; table = {définition: évaluation}."""
    verdict = {}
    for name in CANDIDATES:
        passes = {}
        for definition, ev in table.items():
            by = ev["by"][name]
            ok_unsafe = (by["diff_unsafe"]["est"] <= 0.01 and by["unsafe_acceptance"] <= ALPHA_RISK) if not math.isnan(by["unsafe_acceptance"]) else False
            ok_gain = by["diff_false_rejection"]["hi"] < 0.0
            passes[definition] = bool(ok_unsafe and ok_gain)
        primary = passes.get("M<=2.0", False)
        others = sum(v for k, v in passes.items() if k in ("M<=1.7", "M<=2.5", "M<=3.0"))
        verdict[name] = dict(passes=passes, adopted=bool(primary and others >= 2))
    return verdict


def per_population(rows_eval, s_d, m_lim):
    out = {}
    pops = sorted({r["info"]["population"] for r in rows_eval})
    for pop in pops:
        sub = [r for r in rows_eval if r["info"]["population"] == pop and label(r, m_lim) is not None]
        if not sub:
            continue
        fit = np.array([label(r, m_lim) for r in sub])
        dec = [decisions(r, s_d) for r in sub]
        entry = dict(n=len(sub), n_fit=int(fit.sum()), n_unfit=int((~fit).sum()))
        for name in BASE_NAMES:
            acc = np.array([d[name] for d in dec])
            entry[name] = dict(accept_fit=float(acc[fit].mean()) if fit.any() else None, accept_unfit=float(acc[~fit].mean()) if (~fit).any() else None)
        out[pop] = entry
    return out


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("results")
    parser.add_argument("--out")
    options = parser.parse_args()
    rows = load(options.results)
    valid = [r for r in rows if "gate" in r]
    skipped = dict(total=len(rows), crash=sum("crash" in r for r in rows), identification_error=sum("identification_error" in r for r in rows),
                   no_tuning=sum("no_tuning" in r for r in rows), no_truth=sum("truth" not in r for r in valid))
    calibration = [r for r in valid if r["index"] < 1000]
    evaluation = [r for r in valid if r["index"] >= 1000]
    s_d, n_cal_unfit = calibrate_d(calibration)
    table = {f"M<={m}": evaluate(evaluation, s_d, m) for m in M_LIMITS}
    table["M<=2.0+ITAE<=2"] = evaluate(evaluation, s_d, PRIMARY, itae_factor=2.0)
    report = dict(skipped=skipped, s_D=s_d, calibration_unfit_cases=n_cal_unfit, table=table, verdict=adopt({k: v for k, v in table.items() if k.startswith("M<=") and "ITAE" not in k}),
                  per_population={m: per_population(evaluation, s_d, m) for m in (PRIMARY,)})
    text = json.dumps(report, indent=1, default=float)
    if options.out:
        open(options.out, "w", encoding="utf-8").write(text)
    print(text)


if __name__ == "__main__":
    main()
