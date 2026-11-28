"""Analyse de l'étude (docs/science/lot4/protocole-validation-critere.md, amendements v1.1, v1.2 et v1.3). Aucun seuil n'est choisi après coup.

DEUX PHASES, séparées par un fichier de seuils GELÉ avec son empreinte SHA-256 (v1.3 §4) :
  python analysis.py --phase calibration --results R --extras E --thresholds-out T.json   (lit UNIQUEMENT les cas de calibration, indices < 1000)
  python analysis.py --phase final --results R --extras E --thresholds T.json --out rapport.json   (lit les cas d'évaluation, indices >= 1000 ; refuse un fichier de seuils altéré)

Définitions (v1.3 §1) : faux positif = modèle réellement INAPTE accepté ; taux = inaptes acceptés / tous les inaptes. INAPTE = le réglage recommandé, sur le procédé vrai, est instable (par l'une des
DEUX méthodes, ou si elles divergent) ou a un Ms vrai > M_lim. Intervalles : Clopper-Pearson exact à 95 %. Un objectif alpha n'est DÉMONTRÉ que si la BORNE SUPÉRIEURE à 95 % (unilatérale) du taux
de faux positifs est <= alpha ; un taux observé <= alpha ne suffit pas. L'adoption finale reste refusée tant que la règle iv (données réelles avec vérité de commande) n'est pas satisfaite."""
import argparse
import hashlib
import json
import math

import numpy as np
from scipy.stats import beta

M_LIMITS = (1.7, 2.0, 2.5, 3.0)
PRIMARY = 2.0
ALPHAS = (0.05, 0.01)
BOOTSTRAP = 2000
IMPERFECT = ("P2", "P3", "P5", "P6", "P7")
CALIBRATED = ("D", "E", "C7")
CANDIDATES = ("A1", "A2", "B", "C", "D", "E", "C7", "A_or_D", "A_or_E", "A_and_E", "A_or_C7")
ALL = ("A",) + CANDIDATES
CONFIDENCE = 0.95
SAFETY_MARGIN = 0.01                       # marge de non-infériorité de sécurité du protocole v1.3 (§7 i) ; appliquée à la BORNE, plus à l'estimation (amendement v1.4)


def adoption_level():
    """Niveau unilatéral des décisions d'adoption : 95 % corrigé de Bonferroni par le nombre de candidats comparés (amendement v1.4)."""
    return 1 - (1 - CONFIDENCE) / len(CANDIDATES)


# ---------------------------------------------------------------------------------------------------------------------------------- statistiques exactes
def clopper_pearson(k, n, level=CONFIDENCE):
    """Intervalle exact bilatéral du taux k/n."""
    if n == 0:
        return None
    lo = 0.0 if k == 0 else float(beta.ppf((1 - level) / 2, k, n - k + 1))
    hi = 1.0 if k == n else float(beta.ppf(1 - (1 - level) / 2, k + 1, n - k))
    return lo, hi


def upper_bound(k, n, level=CONFIDENCE):
    """Borne supérieure UNILATÉRALE exacte du taux k/n (sert à juger un objectif)."""
    if n == 0:
        return None
    return 1.0 if k == n else float(beta.ppf(level, k + 1, n - k))


def lower_bound(k, n, level=CONFIDENCE):
    """Borne inférieure UNILATÉRALE exacte (Clopper-Pearson) du taux k/n."""
    if n == 0:
        return None
    return 0.0 if k == 0 else float(beta.ppf(1 - level, k, n - k + 1))


def paired_difference_upper(b, c, n, level):
    """Borne supérieure unilatérale, de niveau AU MOINS `level`, de la différence appariée P(b) - P(c) (b = cas où seul le candidat « bascule », c = seulement la référence),
    sur n unités. Borne de Bonferroni de deux intervalles exacts de Clopper-Pearson (U(b) au niveau 1 - (1-level)/2, moins L(c) au même niveau) : valide sans approximation
    normale, y compris pour des événements rares ou ABSENTS (b = c = 0 donne environ 3/n au lieu d'une certitude de 0, ce que donnerait un bootstrap par percentiles dégénéré).
    Conservatrice par construction. Références : Clopper & Pearson 1934 ; Newcombe 1998 (Stat. Med. 17:2635) pour les différences de proportions appariées ; Fagerland, Lydersen & Laake 2013 (BMC Med. Res. Methodol. 13:91)."""
    if n == 0:
        return None
    half = 1 - (1 - level) / 2
    return upper_bound(b, n, half) - lower_bound(c, n, half)


def inaptes_needed(alpha, level=CONFIDENCE):
    """Plus petit nombre d'inaptes pour lequel ZÉRO faux positif donne une borne supérieure <= alpha (arithmétique de la v1.3 §3)."""
    return int(math.ceil(math.log(1 - level) / math.log(1 - alpha)))


# ---------------------------------------------------------------------------------------------------------------------------------- données
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
                for key in ("E", "C7", "stability", "robust", "alt_sopdt"):
                    r[key] = e.get(key)
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


def tuning_fit(behaviour, m_lim):
    """Un réglage est apte : stable par les DEUX méthodes qui s'accordent, et Ms vrai <= M_lim. Désaccord ou indécidable : inapte (prudent)."""
    if not behaviour or not behaviour.get("agree"):
        return False
    return bool(behaviour["time_stable"] and behaviour["nyquist_stable"] and behaviour["ms"] is not None and behaviour["ms"] <= m_lim)


def label(row, m_lim, itae_factor=None):
    """Le RÉGLAGE recommandé à partir du modèle est-il apte sur le procédé vrai ? (None : pas de vérité)."""
    stability = row.get("stability")
    if stability is None:
        return None
    fit = tuning_fit(stability, m_lim)
    if fit and itae_factor is not None:
        ref, t = row.get("reference"), row.get("truth")
        if not ref or not ref.get("stable") or not ref.get("itae") or not t or not t.get("itae"):
            return None
        fit = t["itae"] <= itae_factor * ref["itae"]
    return fit


def decisions(row, thresholds):
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


def rates(accept, fit):
    accept, fit = np.asarray(accept), np.asarray(fit)
    unsafe = float(accept[~fit].mean()) if (~fit).any() else float("nan")
    false_rejection = float((~accept[fit]).mean()) if fit.any() else float("nan")
    return unsafe, false_rejection


def calibrate(rows, name, alpha):
    """Plus grand seuil tel que P(accepter | inapte) <= alpha sur la CALIBRATION (définition primaire). (seuil, nombre d'inaptes)."""
    values = [statistic(r, name) for r in rows if label(r, PRIMARY) is False and statistic(r, name) is not None and not blocking(r["gate"])]
    if len(values) < 20:
        return None, len(values)
    values = np.sort(values)
    k = int(math.floor(alpha * len(values)))
    return (float(values[k - 1]) if k >= 1 else float(values[0]) - 1e-12), len(values)


def auc(scores_unfit, scores_fit):
    """Aire sous la courbe ROC : P(score d'un inapte > score d'un apte) + 1/2 P(égalité) ; 0,5 = aucun pouvoir discriminant."""
    a, b = np.asarray(scores_unfit, dtype=float), np.asarray(scores_fit, dtype=float)
    if len(a) == 0 or len(b) == 0:
        return None
    greater = (a[:, None] > b[None, :]).sum() + 0.5 * (a[:, None] == b[None, :]).sum()
    return float(greater / (len(a) * len(b)))


# ---------------------------------------------------------------------------------------------------------------------------------- évaluation finale
def evaluate(rows_eval, thresholds, m_lim, itae_factor=None, seed=20261128):
    usable = [r for r in rows_eval if label(r, m_lim, itae_factor) is not None and r.get("E") is not None and r.get("C7") is not None]
    fit = np.array([label(r, m_lim, itae_factor) for r in usable])
    stable = np.array([bool(r["stability"]["time_stable"] and r["stability"]["nyquist_stable"] and r["stability"]["agree"]) for r in usable])
    uncertain = np.array([not r["stability"]["agree"] for r in usable])
    imperfect = np.array([r["info"]["population"] in IMPERFECT for r in usable])
    acc = {name: np.array([decisions(r, thresholds)[name] for r in usable]) for name in ALL}
    n = len(usable)
    excluded = [r for r in rows_eval if r not in usable]        # modèle identifié mais vérité ou statistiques E/C7 absentes (biais de sélection potentiel)
    out = dict(n=n, excluded_with_model=len(excluded), n_fit=int(fit.sum()), n_unfit=int((~fit).sum()), n_unstable=int((~stable & ~uncertain).sum()), n_stability_uncertain=int(uncertain.sum()), by={}, cross={}, stability={})
    rng = np.random.default_rng(seed)
    idx = rng.integers(0, n, size=(BOOTSTRAP, n)) if n else np.zeros((BOOTSTRAP, 0), dtype=int)
    for name in ALL:
        a = acc[name]
        fp_k, fp_n = int((a & ~fit).sum()), int((~fit).sum())
        fn_k, fn_n = int((~a & fit).sum()), int(fit.sum())
        entry = dict(false_positive=dict(k=fp_k, n=fp_n, rate=(fp_k / fp_n if fp_n else None), ci95=clopper_pearson(fp_k, fp_n), upper95_onesided=upper_bound(fp_k, fp_n)),
                     false_negative=dict(k=fn_k, n=fn_n, rate=(fn_k / fn_n if fn_n else None), ci95=clopper_pearson(fn_k, fn_n)),
                     demonstrated={f"{alpha:.2f}": bool(fp_n and upper_bound(fp_k, fp_n) <= alpha) for alpha in ALPHAS})
        if name != "A":
            boot_du, boot_dr = [], []
            for sample in idx:
                ua, ra = rates(acc["A"][sample], fit[sample])
                ux, rx = rates(a[sample], fit[sample])
                boot_du.append(ux - ua)
                boot_dr.append(rx - ra)
            level = 1 - 0.05 / len(CANDIDATES)
            lo, hi = (1 - level) / 2, 1 - (1 - level) / 2
            du, dr = np.array(boot_du), np.array(boot_dr)
            entry["diff_false_positive"] = dict(est=float(np.nanmean(du)), ci=[float(np.nanquantile(du, lo)), float(np.nanquantile(du, hi))])
            entry["diff_false_negative"] = dict(est=float(np.nanmean(dr)), ci=[float(np.nanquantile(dr, lo)), float(np.nanquantile(dr, hi))])
            # Décisions d'adoption : bornes EXACTES appariées (le bootstrap ci-dessus reste descriptif ; il est dégénéré quand l'événement est rare ou absent).
            level = adoption_level()
            only_c, only_a = int((a & ~acc["A"] & ~fit).sum()), int((acc["A"] & ~a & ~fit).sum())
            fn_c, fn_a = int((~a & acc["A"] & fit).sum()), int((a & ~acc["A"] & fit).sum())
            entry["paired"] = dict(
                fp_only_candidate=only_c, fp_only_current=only_a, fp_diff_upper=paired_difference_upper(only_c, only_a, fp_n, level),
                fn_only_candidate=fn_c, fn_only_current=fn_a, fn_diff_upper=paired_difference_upper(fn_c, fn_a, fn_n, level), level=level)
        # Sensibilité à la sélection (bornes de type Manski 1989 pour données manquantes) : et si TOUS les cas exclus étaient des inaptes acceptés par ce critère ?
        entry["selection_worst_case_fp_upper_adoption"] = upper_bound(fp_k + len(excluded), fp_n + len(excluded), adoption_level())
        entry["false_positive"]["upper_adoption"] = upper_bound(fp_k, fp_n, adoption_level())
        out["by"][name] = entry
        out["cross"][name] = {
            "imparfait_reglage_apte_accepte": int((a & imperfect & fit).sum()), "imparfait_reglage_apte_total": int((imperfect & fit).sum()),
            "imparfait_reglage_inapte_accepte": int((a & imperfect & ~fit).sum()), "imparfait_reglage_inapte_total": int((imperfect & ~fit).sum()),
            "exact_reglage_inapte_accepte": int((a & ~imperfect & ~fit).sum()), "exact_reglage_inapte_total": int((~imperfect & ~fit).sum())}
        k_unstable, n_accepted = int((a & ~stable).sum()), int(a.sum())
        out["stability"][name] = dict(accepted=n_accepted, accepted_unstable_or_uncertain=k_unstable,
                                      rate_upper95=upper_bound(k_unstable, n_accepted), ci95=clopper_pearson(k_unstable, n_accepted))      # k = 0 : borne ~ 3/n, jamais « 0 % »
    return out


def discriminating_power(rows_eval, m_lim):
    """Pouvoir discriminant (AUC) de chaque statistique d'ajustement ou orientée commande vis-à-vis de l'aptitude ; 0,5 = aucun (v1.3 §7)."""
    usable = [r for r in rows_eval if label(r, m_lim) is not None and r.get("E") is not None and r.get("C7") is not None]
    fit = np.array([label(r, m_lim) for r in usable])
    stats = {
        "erreur_quadratique_validation": [r["gate"]["rmse"] for r in usable],
        "autocorrelation_lag1_absolue": [abs(r["gate"]["lag1"] or 0.0) for r in usable],
        "erreur_de_structure_relative_nse": [r["stats"]["nse"] or 0.0 for r in usable],
        "ljung_box_(1-p)": [1 - r["stats"]["ljung_box_p"] for r in usable],
        "correlation_croisee_residus_entree": [r["stats"]["cross_max"] / r["stats"]["cross_bound"] for r in usable],
        "E_pire_Ms_ensemble": [r["E"]["worst_ms"] for r in usable],
        "C7_pire_Ms_coins": [r["C7"]["worst_ms"] for r in usable],
    }
    return {name: auc([v for v, f in zip(vals, fit) if not f], [v for v, f in zip(vals, fit) if f]) for name, vals in stats.items()}


def ladder(rows_eval, m_lim):
    """Échelle de recours (v1.3 §8) : parmi les cas dont le réglage recommandé est inapte, ou refusés par A, que valent le modèle alternatif SOPDT et le réglage plus robuste ?"""
    usable = [r for r in rows_eval if label(r, m_lim) is not None]
    def part(selector):
        sub = [r for r in usable if selector(r)]
        n = len(sub)
        alt_available = sum(r.get("alt_sopdt") is not None for r in sub)
        alt_fit = sum(tuning_fit(r.get("alt_sopdt"), m_lim) for r in sub)
        robust_fit = sum(tuning_fit(r.get("robust"), m_lim) for r in sub)
        either = sum(tuning_fit(r.get("alt_sopdt"), m_lim) or tuning_fit(r.get("robust"), m_lim) for r in sub)
        return dict(n=n, alternative_available=alt_available, alternative_fit=alt_fit, robust_fit=robust_fit, either_fit=either,
                    either_fit_ci95=clopper_pearson(either, n) if n else None)
    return dict(unfit_tuning=part(lambda r: label(r, m_lim) is False), rejected_by_current_gate=part(lambda r: not r["gate"]["accepted"]),
                rejected_by_current_gate_and_original_tuning_fit=part(lambda r: not r["gate"]["accepted"] and label(r, m_lim) is True))


def adopt(table, alpha):
    """Règle §7 i-iii pour le risque `alpha` (5 % ou 1 %) — amendement v1.4 : (i) sécurité = borne supérieure appariée de la différence de faux positifs <= marge de non-infériorité
    ET borne supérieure unilatérale du taux de faux positifs (niveau de Bonferroni) <= alpha, ET la même borne tient dans le pire cas où tous les cas exclus faute de vérité seraient
    des inaptes acceptés ; (ii) gain = borne supérieure appariée de la différence de faux négatifs < 0 ; (iii) robustesse à la définition. iv (données réelles avec vérité de commande)
    non satisfait : adoption finale refusée."""
    verdict = {}
    for name in CANDIDATES:
        passes = {}
        for definition, ev in table.items():
            by = ev["by"][name]
            fp = by["false_positive"]
            if fp["n"] == 0:
                passes[definition] = False
                continue
            paired = by["paired"]
            safe = (paired["fp_diff_upper"] is not None and paired["fp_diff_upper"] <= SAFETY_MARGIN and fp["upper_adoption"] <= alpha
                    and by["selection_worst_case_fp_upper_adoption"] <= alpha)
            gain = paired["fn_diff_upper"] is not None and paired["fn_diff_upper"] < 0.0
            passes[definition] = bool(safe and gain)
        primary = passes.get(f"M<={PRIMARY}", False)
        others = sum(v for k, v in passes.items() if k in ("M<=1.7", "M<=2.5", "M<=3.0"))
        verdict[name] = dict(alpha=alpha, passes=passes, demonstrated_on_synthetic_only=bool(primary and others >= 2), adopted=False,
                             reason_not_adopted="iv : aucune donnée réelle offrant une vérité de commande")
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
            entry[name] = dict(accepted_fit=int((a & fit).sum()), fit=int(fit.sum()), accepted_unfit=int((a & ~fit).sum()), unfit=int((~fit).sum()))
        out[pop] = entry
    return out


# ---------------------------------------------------------------------------------------------------------------------------------- phases
def phase_calibration(rows):
    calibration = [r for r in rows if "gate" in r and r["index"] < 1000 and r.get("E") is not None]
    body = dict(rules="v1.3", calibration_cases=len(calibration), alphas={})
    for alpha in ALPHAS:
        thresholds, counts = {}, {}
        for name in CALIBRATED:
            thresholds[name], counts[name] = calibrate(calibration, name, alpha)
        body["alphas"][f"{alpha:.2f}"] = dict(thresholds=thresholds, unfit_cases=counts)
    text = json.dumps(body, sort_keys=True, indent=1)
    return dict(body=body, sha256=hashlib.sha256(text.encode("utf-8")).hexdigest())


def check_thresholds(frozen):
    text = json.dumps(frozen["body"], sort_keys=True, indent=1)
    if hashlib.sha256(text.encode("utf-8")).hexdigest() != frozen["sha256"]:
        raise SystemExit("Fichier de seuils altéré : l'empreinte ne correspond pas. Phase finale refusée.")


def phase_final(rows, frozen):
    check_thresholds(frozen)
    valid = [r for r in rows if "gate" in r]
    evaluation = [r for r in valid if r["index"] >= 1000]
    skipped = dict(total=len(rows), crash=sum("crash" in r for r in rows), identification_error=sum("identification_error" in r for r in rows),
                   no_tuning=sum("no_tuning" in r for r in rows), no_truth=sum("truth" not in r for r in valid), no_extras=sum(r.get("stability") is None for r in valid))
    by_population = {}
    for r in [r for r in rows if r["index"] >= 1000]:
        entry = by_population.setdefault(r["info"]["population"], dict(total=0, no_model=0, with_gate=0, no_tuning=0, no_truth=0, no_extras=0))
        entry["total"] += 1
        entry["no_model"] += int("gate" not in r)
        entry["with_gate"] += int("gate" in r)
        entry["no_tuning"] += int("gate" in r and "no_tuning" in r)
        entry["no_truth"] += int("gate" in r and "truth" not in r)
        entry["no_extras"] += int("gate" in r and r.get("stability") is None)
    for entry in by_population.values():
        entry["coverage"] = (entry["with_gate"] - entry["no_extras"]) / entry["total"] if entry["total"] else None
    report = dict(coverage_evaluation=by_population, skipped=skipped, thresholds_sha256=frozen["sha256"], inaptes_needed=dict((f"{a:.2f}", inaptes_needed(a)) for a in ALPHAS), alphas={})
    for alpha in ALPHAS:
        thresholds = {k: v for k, v in frozen["body"]["alphas"][f"{alpha:.2f}"]["thresholds"].items()}
        table = {f"M<={m}": evaluate(evaluation, thresholds, m) for m in M_LIMITS}
        report["alphas"][f"{alpha:.2f}"] = dict(thresholds=thresholds, table=table, verdict=adopt(table, alpha), per_population=per_population(evaluation, thresholds, PRIMARY))
    report["discriminating_power"] = {f"M<={m}": discriminating_power(evaluation, m) for m in M_LIMITS}
    report["ladder"] = {f"M<={m}": ladder(evaluation, m) for m in M_LIMITS}
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--phase", choices=("calibration", "final"), required=True)
    parser.add_argument("--results", required=True)
    parser.add_argument("--extras")
    parser.add_argument("--thresholds")
    parser.add_argument("--thresholds-out")
    parser.add_argument("--out")
    options = parser.parse_args()
    rows = load(options.results, options.extras)
    if options.phase == "calibration":
        frozen = phase_calibration([r for r in rows if r["index"] < 1000])       # les cas d'ÉVALUATION ne sont même pas passés à la fonction
        text = json.dumps(frozen, sort_keys=True, indent=1)
        if options.thresholds_out:
            open(options.thresholds_out, "w", encoding="utf-8").write(text)
        print(text)
        return
    if not options.thresholds:
        raise SystemExit("La phase finale exige le fichier de seuils gelé (--thresholds).")
    report = phase_final(rows, json.load(open(options.thresholds, encoding="utf-8")))
    text = json.dumps(report, indent=1, default=float)
    if options.out:
        open(options.out, "w", encoding="utf-8").write(text)
    print(text[:2500])


if __name__ == "__main__":
    main()
