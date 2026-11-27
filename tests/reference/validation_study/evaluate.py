"""Évaluation d'UN cas : identification par le moteur, statistiques des critères, réglage recommandé, vérité sur le procédé vrai."""
import math

import numpy as np

import study
import truth
from study import AMPLITUDE, BASELINE_ROWS, SNRS  # noqa: F401

import fopdt  # noqa: E402  (moteur : identification)
import recommend  # noqa: E402  (moteur : réglage)
import validation_gate  # noqa: E402  (moteur : critère actuel)

PHI = {"P4": (0.5, 0.9)}


def case_rng(population, snr, index):
    """Graine déterministe par cas (population, SNR, indice). Calibration : indice < 1000 ; évaluation : indice >= 1000 (jeux disjoints)."""
    return np.random.default_rng([sum(map(ord, population)), snr, index])


def tuning_for(report, ts):
    payload = dict(family="FOPDT", gain=report["gain"], tauSeconds=report["tauSeconds"], delaySeconds=report["delayIntervalSeconds"][1], secondTauSeconds=None,
                   sampleSeconds=ts, standardErrors=report["standardErrors"], objective="balanced", derivativeFilterRatio=None,
                   processVariationPercent=None, optimise=False)
    return recommend.calculate(payload)


def behaviour(plant, ts, controller):
    kp, ti = controller["kp"], controller["tiSeconds"]
    td, tf = controller.get("tdSeconds", 0.0) or 0.0, controller.get("filterSeconds", 0.0) or 0.0
    if ti is None or not math.isfinite(ti) or ti <= 0:
        return dict(stable=False, ms=None, itae=None)
    stable, itae, _ = truth.closed_loop(plant, ts, kp, ti, td, tf)
    return dict(stable=stable, ms=truth.true_ms(plant, ts, kp, ti, td, tf), itae=itae)


def build_case(population, snr, index):
    """Construit un cas (procédé vrai, essais bruités) de façon DÉTERMINISTE ; l'ordre de consommation du générateur aléatoire est figé (ne pas le modifier :
    l'identité des cas entre exécutions en dépend)."""
    rng = case_rng(population, snr, index)
    plant, info = study.draw_plant(population, rng)
    ts = max(round(info["tau"] / 50, 3), 0.04)
    n_post = max(int(math.ceil((8 * info["tau"] + info["theta"]) / ts)), 40)
    secant = abs(info["gain"])
    sigma = secant * AMPLITUDE / snr
    phi = float(rng.choice(PHI["P4"])) if population == "P4" else 0.0
    drift = float(rng.choice([-1.0, 1.0])) * 0.05 * secant * AMPLITUDE if population == "P4" else 0.0
    ratio = float(10 ** rng.uniform(math.log10(0.5), math.log10(2)))
    validation_plant = plant
    if population == "P7":
        validation_plant = plants.fopdt(info["gain"] * (1 + info["gain_change"]), info["tau"], info["theta"])
    source_t, u_t, pv_t, _ = study.make_trial(plant, ts, n_post, AMPLITUDE, sigma, rng, phi, drift, day=1)
    source_v, u_v, pv_v, _ = study.make_trial(validation_plant, ts, n_post, -AMPLITUDE * ratio, sigma, rng, phi, -drift, day=2)
    return dict(rng=rng, plant=plant, info=info, ts=ts, n_post=n_post, secant=secant, sigma=sigma, phi=phi, ratio=ratio, validation_plant=validation_plant,
                source_t=source_t, u_t=u_t, pv_t=pv_t, source_v=source_v, u_v=u_v, pv_v=pv_v)


def run_case(population, snr, index):
    c = build_case(population, snr, index)
    rng, plant, info, ts, n_post, secant, sigma, phi, ratio = (c[k] for k in ("rng", "plant", "info", "ts", "n_post", "secant", "sigma", "phi", "ratio"))
    validation_plant = c["validation_plant"]
    source_t, u_t, pv_t, source_v, u_v, pv_v = (c[k] for k in ("source_t", "u_t", "pv_t", "source_v", "u_v", "pv_v"))
    tolerance = 3 * sigma
    payload = dict(training=source_t, validation=source_v, mvTolerance=1e-3 * AMPLITUDE, pvTolerance=tolerance)
    row = dict(info=info, snr=snr, index=index, ts=ts, sigma=sigma, phi=phi)
    try:
        criteria = validation_gate.suggest(payload)
        gate = validation_gate.assess(dict(family="FOPDT", trials=payload, maximumRmse=criteria["maximumRmse"], maximumResidualLagOne=criteria["maximumResidualLagOne"]))
    except ValueError as error:
        row["identification_error"] = str(error)
        return row
    report, assessment = gate["report"], gate["assessment"]
    valid = report["validation"]
    residual = np.array([p["observed"] - p["predicted"] for p in valid["points"]])
    n = len(residual)
    baselines = np.concatenate([pv_t[:BASELINE_ROWS] - pv_t[:BASELINE_ROWS].mean(), pv_v[:BASELINE_ROWS] - pv_v[:BASELINE_ROWS].mean()])
    sigma_pooled = float(math.sqrt(np.sum(baselines ** 2) / (len(baselines) - 2)))
    response = abs(report["gain"] * valid["inputStep"])
    mse = float(np.mean(residual ** 2))
    m = min(20, n // 4)
    q, p_lb = study.ljung_box(residual, m, 3)
    full = np.concatenate([pv_v[:BASELINE_ROWS] - pv_v[:BASELINE_ROWS].mean(), residual])
    cc, cc_bound = study.cross_correlation_max(full, u_v, m)
    row.update(
        gate=dict(accepted=assessment["accepted"], reasons=assessment["reasons"], rmse=valid["rmse"], lag1=valid.get("residualLagOne"),
                  max_rmse=criteria["maximumRmse"], max_lag1=criteria["maximumResidualLagOne"]),
        stats=dict(n=n, mse=mse, sigma_pooled=sigma_pooled, response=response, nse=max(0.0, mse - sigma_pooled ** 2) / response ** 2 if response > 0 else None,
                   ljung_box_q=q, ljung_box_p=p_lb, cross_max=cc, cross_bound=cc_bound, lags=m),
        model=dict(gain=report["gain"], tau=report["tauSeconds"], delay=report["delaySeconds"]))
    try:
        plan = tuning_for(report, ts)
    except ValueError as error:
        row["no_tuning"] = str(error)
        return row
    if plan.get("decision") != "recommended":
        row["no_tuning"] = plan.get("decision")
        return row
    controller = plan["recommendation"]["controller"]
    row["controller"] = controller
    row["truth"] = behaviour(plant if population != "P7" else plants.fopdt(info["gain"], info["tau"], info["theta"]), ts, controller)
    # réglage de RÉFÉRENCE : identification sans bruit du même procédé (même plan d'essais)
    try:
        clean_t, *_ = study.make_trial(plant, ts, n_post, AMPLITUDE, 0.0, rng, day=1)
        clean_v, *_ = study.make_trial(validation_plant, ts, n_post, -AMPLITUDE * ratio, 0.0, rng, day=2)
        ref_payload = dict(training=clean_t, validation=clean_v, mvTolerance=1e-3 * AMPLITUDE, pvTolerance=1e-6 * secant * AMPLITUDE)
        ref_report = fopdt.identify(ref_payload)
        ref_controller = tuning_for(ref_report, ts)["recommendation"]["controller"]
        row["reference"] = behaviour(plant, ts, ref_controller)
    except (ValueError, KeyError, TypeError) as error:
        row["reference_error"] = str(error)
    return row
