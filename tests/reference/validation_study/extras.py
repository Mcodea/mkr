"""Statistiques des critères E et C7 (amendement v1.1 du protocole). Régénère le cas par sa graine (evaluate.build_case) et calcule, pour le PI recommandé sur le FOPDT :

  E  : Ms du PIRE modèle d'un ensemble « compatible avec les données » = {(K, tau, theta) : SSE_conjointe <= SSE(estimé) (1 + p/(N-p) F_{p,N-p}(0,95))} UNION le SOPDT ajusté ;
  C7 : Ms du pire coin de la boîte [K(1+-e_K)] x [tau(1+-e_tau)] x [theta +- e_theta], e = 2,576 x erreur type du moteur x b, b = max(1, sqrt(MSE_val / sigma^2_bruit)).
Ms par balayage fréquentiel écrit pour l'étude (truth.controller_response), maintien d'ordre zéro inclus (ts/2). Aucune valeur de seuil ici : les décisions sont prises à l'analyse."""
import itertools
import math

import numpy as np
from scipy import stats

import evaluate
import study
import truth
from study import AMPLITUDE, BASELINE_ROWS

import sopdt  # noqa: E402  (moteur)
import validation_gate  # noqa: E402  (moteur)

Z = 2.576
SAMPLES = 3000
W_POINTS = 4000


def fopdt_prediction(t, start, baseline, amplitude, gain, tau, delay):
    x = np.maximum(t - start - delay, 0.0)
    return baseline + gain * amplitude * (1 - np.exp(-x / tau)) * (t >= start + delay)


def frequency_responses(models, w):
    """models : liste de (gain, taus (liste), delay) -> matrice (n, len(w))."""
    s = 1j * w
    out = np.empty((len(models), len(w)), dtype=complex)
    for i, (k, taus, delay) in enumerate(models):
        g = k * np.ones_like(s)
        for tau in taus:
            g = g / (tau * s + 1)
        out[i] = g * np.exp(-s * delay)
    return out


def worst_ms(models, ts, controller):
    w = np.logspace(-4, math.log10(math.pi / ts), W_POINTS)
    c = truth.controller_response(w, controller["kp"], controller["tiSeconds"], controller.get("tdSeconds", 0.0) or 0.0, controller.get("filterSeconds", 0.0) or 0.0)
    loops = c[None, :] * frequency_responses(models, w) * np.exp(-1j * w * ts / 2)[None, :]
    return float(np.max(np.abs(1 / (1 + loops))))


def run_extras(population, snr, index):
    c = evaluate.build_case(population, snr, index)
    ts, sigma = c["ts"], c["sigma"]
    payload = dict(training=c["source_t"], validation=c["source_v"], mvTolerance=1e-3 * AMPLITUDE, pvTolerance=3 * sigma)
    row = dict(info=c["info"], snr=snr, index=index)
    try:
        criteria = validation_gate.suggest(payload)
        gate = validation_gate.assess(dict(family="FOPDT", trials=payload, maximumRmse=criteria["maximumRmse"], maximumResidualLagOne=criteria["maximumResidualLagOne"]))
        report = gate["report"]
        plan = evaluate.tuning_for(report, ts)
    except ValueError as error:
        row["extras_error"] = str(error)
        return row
    if plan.get("decision") != "recommended":
        row["extras_error"] = "no_tuning"
        return row
    controller = plan["recommendation"]["controller"]
    gain, tau, delay = report["gain"], report["tauSeconds"], report["delaySeconds"]
    # ---- E : ensemble compatible ------------------------------------------------------------------------------------------------------------------------------
    n_rows = BASELINE_ROWS + c["n_post"]
    t = np.arange(n_rows) * ts
    start = BASELINE_ROWS * ts
    base_t, base_v = float(c["pv_t"][:BASELINE_ROWS].mean()), float(c["pv_v"][:BASELINE_ROWS].mean())
    a_t, a_v = float(c["u_t"][-1]), float(c["u_v"][-1])
    post = slice(BASELINE_ROWS, None)

    def sse(k, tau_, th):
        return (np.sum((c["pv_t"][post] - fopdt_prediction(t[post], start, base_t, a_t, k, tau_, th)) ** 2) +
                np.sum((c["pv_v"][post] - fopdt_prediction(t[post], start, base_v, a_v, k, tau_, th)) ** 2))

    p, n_total = 3, 2 * c["n_post"]
    threshold = sse(gain, tau, delay) * (1 + p / (n_total - p) * stats.f.ppf(0.95, p, n_total - p))
    se = report.get("standardErrors")
    mse = float(report["validation"]["rmse"]) ** 2
    baselines = np.concatenate([c["pv_t"][:BASELINE_ROWS] - base_t, c["pv_v"][:BASELINE_ROWS] - base_v])
    sigma_pool = math.sqrt(float(np.sum(baselines ** 2)) / (len(baselines) - 2))
    birge = max(1.0, math.sqrt(mse) / sigma_pool)
    rng = np.random.default_rng([97, snr, index])
    half = [6 * birge * (se[k] if se else 0.05 * abs(v)) for k, v in (("gain", gain), ("tauSeconds", tau), ("delaySeconds", delay))] if se else [0.3 * abs(gain), 0.3 * tau, 0.5 * (delay + ts)]
    members = [(gain, [tau], delay)]
    inside = 0
    for scale in (1.0, 2.0, 4.0):
        draws = rng.uniform(-1, 1, size=(SAMPLES, 3)) * np.array(half) * scale
        for dk, dt, dd in draws:
            k_, t_, d_ = gain + dk, tau + dt, delay + dd
            if t_ <= 0 or d_ < 0 or k_ * gain <= 0:
                continue
            if sse(k_, t_, d_) <= threshold:
                members.append((k_, [t_], d_))
                inside += 1
        if inside >= 30:
            break
    sopdt_member = None
    try:
        s2 = sopdt.identify(payload)
        sopdt_member = (s2["gain"], [s2["tauSeconds"], s2["secondTauSeconds"]], s2["delaySeconds"])
        members.append(sopdt_member)
    except (ValueError, KeyError, TypeError):
        pass
    row["E"] = dict(worst_ms=worst_ms(members, ts, controller), nominal_ms=worst_ms([members[0]], ts, controller), members=len(members), inside=inside,
                    sopdt=sopdt_member is not None, birge=birge)
    # ---- C7 : coins de la boîte d'incertitude inflatée --------------------------------------------------------------------------------------------------------
    if se:
        e_k, e_t, e_d = (Z * birge * se["gain"], Z * birge * se["tauSeconds"], Z * birge * se["delaySeconds"])
        corners = []
        for sk, st_, sd in itertools.product((-1, 1), repeat=3):
            k_, t_, d_ = gain + sk * e_k, tau + st_ * e_t, max(delay + sd * e_d, 0.0)
            if t_ > 0 and k_ * gain > 0:
                corners.append((k_, [t_], d_))
        row["C7"] = dict(worst_ms=worst_ms(corners + [(gain, [tau], delay)], ts, controller), corners=len(corners), birge=birge,
                         rel=dict(gain=e_k / abs(gain), tau=e_t / tau, delay=e_d))
    else:
        row["C7"] = None
    return row
