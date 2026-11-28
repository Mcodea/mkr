"""Analyse de l'étude selon la règle d'adoption PRÉ-ENREGISTRÉE (docs/science/lot4/protocole-validation-critere.md §7, amendements v1.1 et v1.2). Aucun seuil n'est choisi après coup :
  * définitions d'« apte » (réglage) : stable ET Ms vrai <= M_lim, M_lim dans {1,7 ; 2,0 (primaire) ; 2,5 ; 3,0} ; sensibilité secondaire : + perte ITAE <= 2 ;
  * seuils de D, E, C7 : calibrés sur le jeu de CALIBRATION (indices < 1000) pour P(accepter | réglage inapte) <= alpha, puis évalués sur le jeu d'ÉVALUATION (indices >= 1000) ;
  * alpha = 5 % (primaire, validé provisoirement par le client : objectif d'ingénierie, pas garantie de sécurité) et 1 % (conservateur) ;
  * intervalles : bootstrap de cas (B = 2000, graine fixe) ; différence appariée avec A au niveau de Bonferroni 0,05 / k, k = 10 candidats ;
  * règle §7 : i non-infériorité de sécurité, ii gain (IC de la différence exclut 0), iii robustesse à la définition, iv données réelles, v reproductibilité. Sans jeu réel offrant une
    vérité de commande, iv ne peut pas être satisfait : l'adoption est alors rapportée « démontrée sur données synthétiques seulement » et la porte actuelle est CONSERVÉE.
python analysis.py results_T1.jsonl --extras extras_T1.jsonl [--out rapport.json]"""
import argparse
import json
import math

import numpy as np

M_LIMITS = (1.7, 2.0, 2.5, 3.0)
PRIMARY = 2.0
ALPHAS = (0.05, 0.01)
BOOTSTRAP = 2000
IMPERFECT = ("P2", "P3", "P5", "P6", "P7")          # procédé vrai qui n'est pas un FOPDT exact ou dont le gain varie entre essais
FIXED = ("A", "A1", "A2", "B", "C")                  # critères sans seuil à calibrer
CALIBRATED = ("D", "E", "C7")
COMBINED = ("A_or_D", "A_or_E", "A_and_E", "A_or_C7")
CANDIDATES = ("A1", "A2", "B", "C", "D", "E", "C7", "A_or_D", "A_or_E", "A_and_E", "A_or_C7")      # comparés à A
# k = 11 candidats comparés à A (A∪C7 compte : le protocole v1.1 en annonçait 10) : correction de Bonferroni plus stricte que celle annoncée, par prudence
ALL = ("A",) + CANDIDATES


def load(results, extras=None):
    rows = [json.loads(line) for line in open(results, encoding="utf-8")]
    if extras:
        by_key = {}
        for line in open(extras, encoding="utf-8"):
            r = json.loads(line)
            by_key[(r["info"]["population"], r["snr"], r["index"])] = r
        for r in rows:
            e = by_key.get((r["info"]["population"], r["snr"], r["index"]))
            if e:
                r["E"], r["C7"] = e.get("E"), e.get("C7")
    return rows


def blocking(gate):
    return any(r not in ("validation_rmse", "validation_residuals") for r in gate["reasons"])


def statistic(row, name):
    if name == "D":
        return row["stats"]["nse"]
    if name == "E":
        return (row.get("E") or {}).get("worst_ms")
    if name == "C7":
        return (row.get("C7") or {}).get("worst_ms")
    raise ValueError(name)


def decisions(row, thresholds):
    """thresholds : {'D': s, 'E': m, 'C7': m} (None = non calibré -> jamais accepté)."""
    g, s = row["gate"], row["stats"]
    block = blocking(g)
    rmse_ok = "validation_rmse" not in g["reasons"]
    out = dict(A=bool(g["accepted"]))
    out["A1"] = bool(rmse_ok and not block)
    out["A2"] = bool("validation_residuals" not in g["reasons"] and not block)
    out["B"] = bool(rmse_ok and s["ljung_box_p"] >= 0.01 and not block)
    out["C"] = bool(rmse_ok and s["cross_max"] <= s["cross_bound"] and not block)
    for name in CALIBRATED:
        value, limit = statistic(row, name), thresholds.get(name)
        ok = value is not None and limit is not None and value <= limit and not block
        out[name] = bool(ok and (rmse_ok if name != "D" else True))
    out["A_or_D"] = out["A"] or out["D"]
    out["A_or_E"] = out["A"] or out["E"]
    out["A_and_E"] = out["A"] and out["E"]
    out["A_or_C7"] = out["A"] or out["C7"]
    return out


def label(row, m_lim, itae_factor=None):
    """Le RÉGLAGE recommandé est-il apte sur le procédé vrai ? (None : pas de vérité)."""
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


def calibrate(rows, name, alpha):
    """Plus grand seuil tel que P(accepter | réglage inapte) <= alpha sur la calibration (définition primaire). (seuil, nombre de cas inaptes)."""
    values = [statistic(r, name) for r in rows if label(r, PRIMARY) is False and statistic(r, name) is not None and not blocking(r["gate"])]
    if len(values) < 20:
        return None, len(values)
    values = np.sort(values)
    k = int(math.floor(alpha * len(values)))
    return (float(values[k - 1]) if k >= 1 else float(values[0]) - 1e-12), len(values)


def rates(accept, fit):
    accept, fit = np.asarray(accept), np.asarray(fit)
    unsafe = float(accept[~fit].mean()) if (~fit).any() else float("nan")
    false_rejection = float((~accept[fit]).mean()) if fit.any() else float("nan")
    return unsafe, false_rejection


def interval(samples, level=0.95):
    lo, hi = np.quantile(samples, [(1 - level) / 2, 1 - (1 - level) / 2])
    return float(lo), float(hi)


def evaluate(rows_eval, thresholds, m_lim, itae_factor=None, seed=20261128):
    usable = [r for r in rows_eval if label(r, m_lim, itae_factor) is not None and r.get("E") is not None and r.get("C7") is not None]
    fit = np.array([label(r, m_lim, itae_factor) for r in usable])
    stable = np.array([bool(r["truth"]["stable"]) for r in usable])
    imperfect = np.array([r["info"]["population"] in IMPERFECT for r in usable])
    acc = {name: np.array([decisions(r, thresholds)[name] for r in usable]) for name in ALL}
    n = len(usable)
    out = dict(n=n, n_fit=int(fit.sum()), n_unfit=int((~fit).sum()), n_unstable=int((~stable).sum()), by={}, cross={}, stability={})
    rng = np.random.default_rng(seed)
    idx = rng.integers(0, n, size=(BOOTSTRAP, n)) if n else np.zeros((BOOTSTRAP, 0), dtype=int)
    level = 1 - 0.05 / len(CANDIDATES)
    for name in ALL:
        u, r = rates(acc[name], fit)
        boot_u, boot_r, boot_du, boot_dr = [], [], [], []
        for sample in idx:
            ub, rb = rates(acc[name][sample], fit[sample])
            ua, ra = rates(acc["A"][sample], fit[sample])
            boot_u.append(ub)
            boot_r.append(rb)
            boot_du.append(ub - ua)
            boot_dr.append(rb - ra)
        entry = dict(unsafe_acceptance=u, unsafe_ci=interval(np.array(boot_u)[~np.isnan(boot_u)]) if not math.isnan(u) else None,
                     false_rejection=r, false_rejection_ci=interval(np.array(boot_r)[~np.isnan(boot_r)]) if not math.isnan(r) else None)
        if name != "A":
            entry["diff_unsafe"] = dict(est=float(np.nanmean(boot_du)), ci=interval(np.array(boot_du)[~np.isnan(boot_du)], level))
            entry["diff_false_rejection"] = dict(est=float(np.nanmean(boot_dr)), ci=interval(np.array(boot_dr)[~np.isnan(boot_dr)], level))
        out["by"][name] = entry
        a = acc[name]
        out["cross"][name] = {
            "imparfait_reglage_apte_accepte": int((a & imperfect & fit).sum()), "imparfait_reglage_apte_total": int((imperfect & fit).sum()),
            "imparfait_reglage_inapte_accepte": int((a & imperfect & ~fit).sum()), "imparfait_reglage_inapte_total": int((imperfect & ~fit).sum()),
            "exact_reglage_inapte_accepte": int((a & ~imperfect & ~fit).sum()), "exact_reglage_inapte_total": int((~imperfect & ~fit).sum()),
        }
        out["stability"][name] = dict(accepted=int(a.sum()), accepted_unstable=int((a & ~stable).sum()))
    return out


def adopt(table):
    """Règle §7 i-iii pour chaque candidat, α primaire ; table = {définition: évaluation}. iv (données réelles) ne peut pas être satisfait : adoption finale = False."""
    verdict = {}
    for name in CANDIDATES:
        passes = {}
        for definition, ev in table.items():
            by = ev["by"][name]
            if math.isnan(by["unsafe_acceptance"]):
                passes[definition] = False
                continue
            safe = by["diff_unsafe"]["est"] <= 0.01 and by["unsafe_acceptance"] <= ALPHAS[0]
            gain = by["diff_false_rejection"]["ci"][1] < 0.0
            passes[definition] = bool(safe and gain)
        primary = passes.get(f"M<={PRIMARY}", False)
        others = sum(v for k, v in passes.items() if k in ("M<=1.7", "M<=2.5", "M<=3.0"))
        verdict[name] = dict(passes=passes, demonstrated_on_synthetic_only=bool(primary and others >= 2), adopted=False,
                             reason_not_adopted="iv : aucune donnée réelle offrant une vérité de commande" )
    return verdict


def per_population(rows_eval, thresholds, m_lim):
    out = {}
    for pop in sorted({r["info"]["population"] for r in rows_eval}):
        sub = [r for r in rows_eval if r["info"]["population"] == pop and label(r, m_lim) is not None and r.get("E") is not None and r.get("C7") is not None]
        if not sub:
            continue
        fit = np.array([label(r, m_lim) for r in sub])
        dec = [decisions(r, thresholds) for r in sub]
        entry = dict(n=len(sub), n_fit=int(fit.sum()), n_unfit=int((~fit).sum()))
        for name in ALL:
            a = np.array([d[name] for d in dec])
            entry[name] = dict(accept_fit=float(a[fit].mean()) if fit.any() else None, accept_unfit=float(a[~fit].mean()) if (~fit).any() else None)
        out[pop] = entry
    return out


def analyse(rows):
    valid = [r for r in rows if "gate" in r]
    skipped = dict(total=len(rows), crash=sum("crash" in r for r in rows), identification_error=sum("identification_error" in r for r in rows),
                   no_tuning=sum("no_tuning" in r for r in rows), no_truth=sum("truth" not in r for r in valid), no_extras=sum(r.get("E") is None for r in valid))
    calibration = [r for r in valid if r["index"] < 1000 and r.get("E") is not None]
    evaluation = [r for r in valid if r["index"] >= 1000]
    report = dict(skipped=skipped, alphas={})
    for alpha in ALPHAS:
        thresholds, calibration_cases = {}, {}
        for name in CALIBRATED:
            thresholds[name], calibration_cases[name] = calibrate(calibration, name, alpha)
        table = {f"M<={m}": evaluate(evaluation, thresholds, m) for m in M_LIMITS}
        table["M<=2.0+ITAE<=2"] = evaluate(evaluation, thresholds, PRIMARY, itae_factor=2.0)
        report["alphas"][f"{alpha:.2f}"] = dict(thresholds=thresholds, calibration_unfit_cases=calibration_cases, table=table,
                                                 verdict=adopt({k: v for k, v in table.items() if k.startswith("M<=") and "ITAE" not in k}),
                                                 per_population=per_population(evaluation, thresholds, PRIMARY))
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("results")
    parser.add_argument("--extras")
    parser.add_argument("--out")
    options = parser.parse_args()
    report = analyse(load(options.results, options.extras))
    text = json.dumps(report, indent=1, default=float)
    if options.out:
        open(options.out, "w", encoding="utf-8").write(text)
    print(text[:3000])


if __name__ == "__main__":
    main()
