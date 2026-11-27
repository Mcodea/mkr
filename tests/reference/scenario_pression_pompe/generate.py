"""DONNÉES SYNTHÉTIQUES — scénario de référence ORIAS « régulation de pression par pompe centrifuge à variateur ».

Générateur autonome (numpy + scipy uniquement). Il n'importe RIEN du moteur d'ORIAS (engine/) ni de src/ :
le procédé, le capteur, le variateur, le PI et l'ajustement des paramètres théoriques sont écrits ici, indépendamment
du code d'identification qu'ils servent à éprouver.

Aucune valeur produite n'est une mesure industrielle. Tous les CSV et manifestes sont SYNTHÉTIQUES.

Usage (depuis la racine du dépôt) :
    python tests/reference/scenario_pression_pompe/generate.py            # écrit dans data/ à côté de ce fichier
    python tests/reference/scenario_pression_pompe/generate.py --out DIR  # écrit ailleurs
    python tests/reference/scenario_pression_pompe/generate.py --check    # génère deux fois en temporaire et compare

Modèle (voir docs/science/scenario-pression-pompe-m580/parametres-theoriques.md) :
    variateur : consigne de vitesse MV [%] -> retard de communication (1 période) -> rampe ACC/DEC -> retard
                mécanique du 1er ordre -> vitesse relative n = N/N0
    pompe     : H_p(Q, n) = H0·n² − a·Q²            (lois de similitude : Q ∝ n, H ∝ n²)
    conduite  : (l/(g·A))·dQ/dt = H_p(Q, n) − r·Q² − h,  clapet anti-retour Q ≥ 0
    collecteur: C_h(h)·dh/dt = Q − Q_d,  C_h = ρ·g·V_gaz/(κ·p_abs)  (réservoir hydropneumatique, gaz polytropique)
    demande   : Q_d = Cq·u_v·√h   (vanne/consommateurs aval, ouverture u_v)
    capteur   : 1er ordre τ_s, bruit gaussien, quantification, écrêtage à l'étendue 0–10 bar, échantillonnage Ts
"""
import argparse
import csv
import hashlib
import io
import json
import math
import sys
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path

import numpy as np
import scipy
from scipy.optimize import brentq, least_squares

GENERATOR_VERSION = "1.0.0"
LABEL = "DONNÉES SYNTHÉTIQUES — générées par simulation, jamais mesurées sur une installation réelle"

# ---------------------------------------------------------------------------------------------------------------
# Paramètres du procédé. « source » : valeur reprise d'une référence ; « hypothèse » : choix de scénario.
# ---------------------------------------------------------------------------------------------------------------
G = 9.80665            # m/s², pesanteur normale (constante conventionnelle)
RHO = 998.2            # kg/m³, eau à 20 °C (valeur usuelle ; hypothèse : eau claire)
P_ATM = 101325.0       # Pa, pression atmosphérique normale (hypothèse : site au niveau de la mer)
BULK = 2.2e9           # Pa, module de compressibilité de l'eau (ordre de grandeur usuel ; hypothèse)

PUMP = dict(
    H0=72.0,           # m, hauteur à débit nul à n = 1 (hypothèse : 1,24 × H_BEP)
    Q_bep=16.0,        # m³/h, débit au point de meilleur rendement (ordre de grandeur : Khayatzadeh & Ghafouri 2015)
    H_bep=58.0,        # m, hauteur au point de meilleur rendement (même source, ordre de grandeur)
    N0=2900.0,         # tr/min à 50 Hz (hypothèse : moteur 2 pôles)
)
PUMP["a"] = (PUMP["H0"] - PUMP["H_bep"]) / PUMP["Q_bep"] ** 2      # m/(m³/h)²

PIPE = dict(
    length=30.0,       # m, refoulement pompe -> collecteur (hypothèse)
    diameter=0.065,    # m, DN65 (hypothèse)
    wall=0.005,        # m, épaisseur acier (hypothèse)
    young=2.0e11,      # Pa, module d'Young de l'acier (ordre de grandeur usuel)
    r_loss=3.0 / 16.0 ** 2,   # m/(m³/h)², pertes de charge : 3 m à 16 m³/h (hypothèse)
)
PIPE["area"] = math.pi / 4 * PIPE["diameter"] ** 2
# Célérité des ondes (formule de Korteweg pour conduite élastique à paroi mince) : ordre de grandeur 1,4 km/s.
PIPE["wave_speed"] = math.sqrt(BULK / RHO / (1 + BULK * PIPE["diameter"] / (PIPE["young"] * PIPE["wall"])))

TANK = dict(
    p_ref_bar=4.0,     # bar relatif où le volume de gaz vaut V_ref (hypothèse)
    V_ref=0.050,       # m³ de gaz à p_ref (hypothèse : réservoir de ~70 L)
    p_pre_bar=1.0,     # bar relatif, pression de prégonflage : sous cette valeur le réservoir est vide (hypothèse)
    kappa=1.0,         # exposant polytropique : 1 isotherme ... 1,4 adiabatique (hypothèse nominale : 1,0)
)

DRIVE = dict(
    mv_min=30.0,       # %, vitesse basse (LSP) — hypothèse de scénario
    mv_max=100.0,      # %, vitesse haute (HSP) — hypothèse de scénario
    ramp=10.0,         # %/s : ACC = DEC = 10 s pour 0 -> 100 % (hypothèse ; les paramètres ACC/dEC existent, FA335688)
    tau_mech=0.5,      # s, retard du 1er ordre de la vitesse réelle sur la sortie de rampe (hypothèse)
    comm_delay_samples=1,  # retard de transmission de la consigne de vitesse : 1 période Ts (hypothèse)
)

SENSOR = dict(
    low=0.0, high=10.0,    # bar relatif, étendue du transmetteur (hypothèse ; 4–20 mA)
    tau=0.2,               # s, amortissement du transmetteur (hypothèse)
    resolution=0.001,      # bar : 0–10 bar sur 10000 points (hypothèse de chaîne d'acquisition)
)

TS = 0.1          # s, période d'échantillonnage / tâche automate (hypothèse)
SUBSTEPS = 20     # pas d'intégration RK4 = TS / 20 = 5 ms
OPERATING = dict(mv=85.0, valve=0.5, p_bar=4.0)   # point de fonctionnement nominal (hypothèse)


def bar_to_head(p_bar):
    return p_bar * 1e5 / (RHO * G)


def head_to_bar(h):
    return h * RHO * G / 1e5


H_REF = bar_to_head(TANK["p_ref_bar"])
H_PRE = bar_to_head(TANK["p_pre_bar"])


def pump_flow(n, h):
    """Débit stationnaire [m³/h] de la pompe à vitesse relative n contre la charge h [m] (conduite incluse)."""
    drive = PUMP["H0"] * n * n - h
    return math.sqrt(drive / (PUMP["a"] + PIPE["r_loss"])) if drive > 0 else 0.0


def calibrate_valve():
    """Cq tel que p = 4,0 bar au point nominal (n = 0,85, u_v = 0,5) à l'équilibre."""
    h = bar_to_head(OPERATING["p_bar"])
    return pump_flow(OPERATING["mv"] / 100, h) / (OPERATING["valve"] * math.sqrt(h))


CQ = calibrate_valve()


def demand(u_v, h):
    return CQ * u_v * math.sqrt(h) if h > 0 else 0.0


def capacitance(h):
    """Capacité hydraulique [m³/m] du collecteur : gaz du réservoir + compressibilité de l'eau des conduites."""
    if h < H_PRE:
        raise RuntimeError("scenario_outside_validity: pression sous le prégonflage, réservoir vide (modèle non valide)")
    p_abs = RHO * G * h + P_ATM
    p_ref_abs = RHO * G * H_REF + P_ATM
    k = TANK["kappa"]
    v_gas = TANK["V_ref"] * (p_ref_abs / p_abs) ** (1 / k)
    c_gas = RHO * G * v_gas / (k * p_abs)
    c_liquid = PIPE["area"] * PIPE["length"] * G / PIPE["wave_speed"] ** 2
    return c_gas + c_liquid


def equilibrium(n, u_v):
    """Charge h [m] d'équilibre : débit pompe = débit demandé."""
    top = PUMP["H0"] * n * n
    return brentq(lambda h: pump_flow(n, h) - demand(u_v, h), 1e-9, top - 1e-12, xtol=1e-13)


INERTANCE = PIPE["length"] / (G * PIPE["area"])        # s²/m² : (l/(g·A))


def derivatives(x, n_ramp, u_v):
    n, q, h, yf = x
    dn = (n_ramp - n) / DRIVE["tau_mech"]
    head = PUMP["H0"] * n * n - (PUMP["a"] + PIPE["r_loss"]) * q * abs(q) - h
    dq = 3600.0 * head / INERTANCE                       # q en m³/h
    if q <= 0.0 and dq < 0.0:
        dq = 0.0                                          # clapet anti-retour fermé
    dh = (q - demand(u_v, h)) / 3600.0 / capacitance(h)
    dy = (head_to_bar(h) - yf) / SENSOR["tau"]
    return np.array([dn, dq, dh, dy])


class Plant:
    """Procédé continu intégré par RK4 à pas fixe, avec rampe du variateur et retard de communication."""

    def __init__(self, mv, u_v):
        n = mv / 100
        h = equilibrium(n, u_v)
        self.x = np.array([n, pump_flow(n, h), h, head_to_bar(h)])
        self.n_ramp = n
        self.delay = [mv] * DRIVE["comm_delay_samples"]

    def step(self, mv_command, u_v):
        """Applique la commande de vitesse pendant une période TS. Renvoie la vitesse relative appliquée."""
        self.delay.append(mv_command)
        reference = min(max(self.delay.pop(0), DRIVE["mv_min"]), DRIVE["mv_max"]) / 100   # LSP/HSP du variateur
        dt = TS / SUBSTEPS
        rate = DRIVE["ramp"] / 100 * dt
        for _ in range(SUBSTEPS):
            self.n_ramp += min(max(reference - self.n_ramp, -rate), rate)
            x = self.x
            k1 = derivatives(x, self.n_ramp, u_v)
            k2 = derivatives(x + dt / 2 * k1, self.n_ramp, u_v)
            k3 = derivatives(x + dt / 2 * k2, self.n_ramp, u_v)
            k4 = derivatives(x + dt * k3, self.n_ramp, u_v)
            self.x = x + dt / 6 * (k1 + 2 * k2 + 2 * k3 + k4)
            if self.x[1] < 0.0:
                self.x[1] = 0.0
        return self.x[0]

    def true_pressure(self):
        return head_to_bar(self.x[2])


def measure(plant, rng, noise):
    y = plant.x[3] + (rng.normal(0.0, noise) if noise > 0 else 0.0)
    y = min(max(y, SENSOR["low"]), SENSOR["high"])
    return round(y / SENSOR["resolution"]) * SENSOR["resolution"]


# ---------------------------------------------------------------------------------------------------------------
# Essais
# ---------------------------------------------------------------------------------------------------------------
def open_loop(duration, mv_schedule, valve_schedule, noise, seed):
    rng = np.random.default_rng(seed)
    plant = Plant(mv_schedule(0.0), valve_schedule(0.0))
    rows = []
    for k in range(int(round(duration / TS)) + 1):
        t = k * TS
        pv = measure(plant, rng, noise)
        mv = mv_schedule(t)
        rows.append((k, OPERATING["p_bar"], pv, mv, 1, plant.true_pressure()))
        plant.step(mv, valve_schedule(t))
    return rows


def simc_pi(gain, tau, delay, tau_c):
    """Règle SIMC (Skogestad 2003) pour un FOPDT : Kc = τ / (K (τc + θ)), Ti = min(τ, 4 (τc + θ))."""
    return tau / (gain * (tau_c + delay)), min(tau, 4 * (tau_c + delay))


def closed_loop(duration, sp_schedule, valve_schedule, kc, ti, noise, seed, manual_until):
    """PI idéal discret (forme position, intégration d'Euler avant), anti-saturation par blocage conditionnel,
    passage manuel -> automatique sans à-coup (l'intégrale est initialisée sur la sortie manuelle).
    Ce PI est un régulateur GÉNÉRIQUE de scénario : il n'est PAS un modèle du PIDFF ni du PID_INT."""
    rng = np.random.default_rng(seed)
    mv_manual = OPERATING["mv"]
    plant = Plant(mv_manual, valve_schedule(0.0))
    integral = mv_manual
    rows = []
    for k in range(int(round(duration / TS)) + 1):
        t = k * TS
        pv = measure(plant, rng, noise)
        sp = sp_schedule(t)
        manual = t < manual_until
        error = sp - pv
        if manual:
            mv = mv_manual
            integral = mv_manual - kc * error            # suivi : sortie inchangée au passage en automatique
        else:
            unclamped = kc * error + integral
            mv = min(max(unclamped, DRIVE["mv_min"]), DRIVE["mv_max"])
            # blocage de l'intégrale si la sortie est saturée et que l'erreur pousse dans le même sens
            if not ((unclamped >= DRIVE["mv_max"] and error > 0) or (unclamped <= DRIVE["mv_min"] and error < 0)):
                integral += kc * TS / ti * error
        rows.append((k, sp, pv, mv, 1 if manual else 0, plant.true_pressure()))
        plant.step(mv, valve_schedule(t))
    return rows


def step_at(t0, before, after):
    return lambda t: after if t >= t0 - 1e-9 else before


# ---------------------------------------------------------------------------------------------------------------
# Paramètres théoriques : linéarisation, règle de la moitié, ajustement FOPDT/SOPDT d'une réponse sans bruit
# ---------------------------------------------------------------------------------------------------------------
def linearization(mv, u_v):
    n = mv / 100
    h = equilibrium(n, u_v)
    x0 = np.array([n, pump_flow(n, h), h, head_to_bar(h)])
    jac = np.zeros((4, 4))
    for j in range(4):
        e = np.zeros(4)
        e[j] = 1e-6 * max(1.0, abs(x0[j]))
        jac[:, j] = (derivatives(x0 + e, n, u_v) - derivatives(x0 - e, n, u_v)) / (2 * e[j])
    b = np.zeros(4)
    b[0] = 1 / DRIVE["tau_mech"]
    # gain statique dPV/dMV [bar/%] : -C A^-1 B, sortie = yf (état 4), entrée = n_ramp en fraction (÷100 pour %)
    gain = float(-np.linalg.solve(jac, b)[3]) / 100
    eig = np.linalg.eigvals(jac)
    # Analytique pour contrôle : conductances locales pompe et vanne
    qp = pump_flow(n, h)
    dqp_dn = PUMP["H0"] * n / ((PUMP["a"] + PIPE["r_loss"]) * qp)
    dqp_dh = -1 / (2 * (PUMP["a"] + PIPE["r_loss"]) * qp)
    dqd_dh = qp / (2 * h)
    gain_analytic = head_to_bar(dqp_dn / (dqd_dh - dqp_dh)) / 100
    tau_tank = capacitance(h) / ((dqd_dh - dqp_dh) / 3600)
    return dict(mv=mv, valve=u_v, p_bar=head_to_bar(h), flow_m3h=qp, gain_bar_per_pct=gain,
                gain_analytic_bar_per_pct=gain_analytic, tau_tank_s=tau_tank,
                time_constants_s=sorted((float(-1 / ev.real) for ev in eig), reverse=True),
                eigen_imag_max=float(max(abs(ev.imag) for ev in eig)),
                capacitance_m3_per_m=capacitance(h), conductance_m3h_per_m=dqd_dh - dqp_dh)


def fit_models(rows, step_index, du):
    t = np.array([r[0] for r in rows[step_index:]]) * TS - step_index * TS
    y = np.array([r[2] for r in rows[step_index:]]) - rows[step_index - 1][2]

    def fopdt(p):
        k, tau, th = p
        return np.where(t > th, k * du * (1 - np.exp(-(t - th) / tau)), 0.0)

    def sopdt(p):
        k, t1, t2, th = p
        s = np.clip(t - th, 0, None)
        if abs(t1 - t2) < 1e-6:
            t2 = t1 * (1 + 1e-6)
        return np.where(t > th, k * du * (1 - (t1 * np.exp(-s / t1) - t2 * np.exp(-s / t2)) / (t1 - t2)), 0.0)

    f = least_squares(lambda p: fopdt(p) - y, [0.09, 5.0, 0.8], bounds=([0, 0.05, 0], [1, 100, 10]))
    s = least_squares(lambda p: sopdt(p) - y, [0.09, 5.0, 0.5, 0.3], bounds=([0, 0.05, 0.01, 0], [1, 100, 20, 10]))
    sk, s1, s2, sth = s.x
    if s2 > s1:
        s1, s2 = s2, s1
    return (dict(gain_bar_per_pct=f.x[0], tau_s=f.x[1], delay_s=f.x[2], rmse_bar=float(np.sqrt(np.mean(f.fun ** 2)))),
            dict(gain_bar_per_pct=sk, tau1_s=s1, tau2_s=s2, delay_s=sth, rmse_bar=float(np.sqrt(np.mean(s.fun ** 2)))))


def half_rule(lin, step_pct):
    """Règle de la moitié (Skogestad 2003) sur les constantes de temps du linéarisé + retards purs du scénario."""
    taus = lin["time_constants_s"]
    ramp_delay = step_pct / DRIVE["ramp"] / 2                    # rampe d'amplitude Δ : retard équivalent ≈ Δ/(2·pente)
    pure = DRIVE["comm_delay_samples"] * TS + TS / 2 + ramp_delay   # transmission + bloqueur/échantillonnage + rampe
    fopdt = dict(tau_s=taus[0] + taus[1] / 2, delay_s=pure + taus[1] / 2 + sum(taus[2:]))
    sopdt = dict(tau1_s=taus[0], tau2_s=taus[1] + taus[2] / 2, delay_s=pure + taus[2] / 2 + sum(taus[3:]))
    return dict(pure_delays_s=dict(communication=DRIVE["comm_delay_samples"] * TS, sampling_half_period=TS / 2,
                                   ramp_equivalent=ramp_delay), fopdt=fopdt, sopdt=sopdt)


# ---------------------------------------------------------------------------------------------------------------
# Écriture
# ---------------------------------------------------------------------------------------------------------------
def csv_bytes(rows, start, with_manual=True):
    out = io.StringIO(newline="")
    w = csv.writer(out, lineterminator="\n")
    w.writerow(["timestamp", "SP", "PV", "MV"] + (["MANUAL"] if with_manual else []))
    for k, sp, pv, mv, manual, _ in rows:
        stamp = (start + timedelta(milliseconds=100 * k)).isoformat(timespec="milliseconds")
        w.writerow([stamp, f"{sp:.3f}", f"{pv:.3f}", f"{mv:.3f}"] + ([manual] if with_manual else []))
    return out.getvalue().encode("utf-8")


def common():
    return dict(
        _DONNEES_SYNTHETIQUES="DONNÉES SYNTHÉTIQUES — aucune mesure industrielle ; ne pas présenter comme telles.",
        label=LABEL, generator="tests/reference/scenario_pression_pompe/generate.py", generatorVersion=GENERATOR_VERSION,
        numpyVersion=np.__version__, scipyVersion=scipy.__version__, rng="numpy.random.default_rng (PCG64)",
        samplePeriodSeconds=TS, integration=f"RK4 pas fixe {TS / SUBSTEPS:.3f} s",
        units=dict(timestamp="ISO 8601 UTC, millisecondes", SP="bar (relatif)", PV="bar (relatif)",
                   MV="% de la vitesse nominale (100 % = 50 Hz = N0)", MANUAL="1 = manuel (boucle ouverte), 0 = automatique"),
        csvFormat=dict(delimiter=",", decimal=".", timeColumn="timestamp", mvColumn="MV", pvColumn="PV", spColumn="SP",
                       modeColumn="MANUAL", strptime="%Y-%m-%dT%H:%M:%S.%f%z"),
        operatingPoint=OPERATING,
    )


def build():
    files = {}
    lin = linearization(OPERATING["mv"], OPERATING["valve"])
    t0 = datetime(2026, 3, 2, 8, 0, 0, tzinfo=timezone.utc)

    # Paramètres « vrais » de référence : FOPDT/SOPDT ajustés sur la réponse SANS bruit ni quantification à +5 %.
    # Vue capteur sans bruit (filtre du transmetteur inclus), quantification neutralisée le temps de cette simulation.
    saved = SENSOR["resolution"]
    SENSOR["resolution"] = 1e-9
    try:
        noiseless = open_loop(80.0, step_at(20.0, 85.0, 90.0), lambda t: 0.5, 0.0, 0)
    finally:
        SENSOR["resolution"] = saved
    fopdt_fit, sopdt_fit = fit_models(noiseless, 200, 5.0)
    lin_up = linearization(90.0, 0.5)
    lin_down = linearization(80.0, 0.5)
    rule = half_rule(lin, 5.0)
    kc, ti = simc_pi(fopdt_fit["gain_bar_per_pct"], fopdt_fit["tau_s"], fopdt_fit["delay_s"], 2 * fopdt_fit["delay_s"])
    theory = dict(common(), purpose="Paramètres théoriques de référence du scénario (vérité terrain synthétique).",
                  constants=dict(g=G, rho=RHO, p_atm_Pa=P_ATM, bulk_modulus_Pa=BULK),
                  pump=PUMP, pipe=PIPE, tank=TANK, drive=DRIVE, sensor=SENSOR, valveCq_m3h_per_sqrt_m=CQ,
                  linearizationNominal=lin, linearization90=lin_up, linearization80=lin_down, halfRule=rule,
                  fopdtFitNoiseless=fopdt_fit, sopdtFitNoiseless=sopdt_fit,
                  piClosedLoop=dict(rule="SIMC, tau_c = 2·theta", Kc_pct_per_bar=kc, Ti_s=ti,
                                    note="PI générique de scénario, forme idéale u = Kc·(e + 1/Ti ∫e)."),
                  maxPressureAtFullSpeed_bar=head_to_bar(equilibrium(1.0, 0.5)))
    files["parametres_theoriques.json"] = theory

    truth = dict(gain_bar_per_pct=fopdt_fit["gain_bar_per_pct"], tau_s=fopdt_fit["tau_s"], delay_s=fopdt_fit["delay_s"],
                 sopdt=sopdt_fit, linear_local_gain_bar_per_pct=lin["gain_bar_per_pct"])

    def dataset(name, rows, start, seed, noise, intent, reason, extra, expected=None):
        files[f"{name}.csv"] = csv_bytes(rows, start)
        if "mvBefore" in extra:
            clamp = lambda mv: min(max(mv, DRIVE["mv_min"]), DRIVE["mv_max"])
            before, after = clamp(extra["mvBefore"]), clamp(extra["mvAfter"])
            valve = extra.get("valveOpening", OPERATING["valve"])
            extra["staticSecantGain_bar_per_applied_pct"] = (
                head_to_bar(equilibrium(after / 100, valve)) - head_to_bar(equilibrium(before / 100, valve))) / (after - before)
            extra["staticSecantGain_bar_per_recorded_pct"] = (
                head_to_bar(equilibrium(after / 100, valve)) - head_to_bar(equilibrium(before / 100, valve))) / (
                extra["mvAfter"] - extra["mvBefore"])
        files[f"{name}.manifest.json"] = dict(
            common(), dataset=name, seed=seed, noiseStdBar=noise, quantizationBar=SENSOR["resolution"],
            rows=len(rows), start=start.isoformat(), durationSeconds=(len(rows) - 1) * TS,
            intent=intent, intentReason=reason, trueParameters=expected if expected is not None else truth, **extra)

    nominal_noise = 0.003
    dataset("ol_nominal", open_loop(80.0, step_at(20.0, 85.0, 90.0), lambda t: 0.5, nominal_noise, 20261101),
            t0, 20261101, nominal_noise, "accepted",
            "Échelon +5 % en manuel au point nominal, bruit faible : identification FOPDT attendue proche des paramètres vrais.",
            dict(stepTimeSeconds=20.0, mvBefore=85.0, mvAfter=90.0, valveOpening=0.5, role="estimation"))
    dataset("ol_validation", open_loop(80.0, step_at(20.0, 85.0, 89.0), lambda t: 0.5, nominal_noise, 20261102),
            t0 + timedelta(hours=2), 20261102, nominal_noise, "accepted",
            "Essai indépendant de ol_nominal (autre graine, autre date, autre amplitude +4 %, même domaine) : "
            "sert UNIQUEMENT à valider le modèle estimé sur ol_nominal, jamais à l'ajuster.",
            dict(stepTimeSeconds=20.0, mvBefore=85.0, mvAfter=89.0, valveOpening=0.5, role="validation"))
    dataset("ol_validation_reverse", open_loop(80.0, step_at(20.0, 85.0, 80.0), lambda t: 0.5, nominal_noise, 20261109),
            t0 + timedelta(hours=3), 20261109, nominal_noise, "accepted_with_conditions",
            "Validation en sens inverse (−5 %) : le gain sécant 80–85 % est inférieur d'environ 6 % à celui de 85–90 % "
            "(p ∝ n² sans hauteur statique). Écart de validation attendu, dû à la non-linéarité et non au bruit.",
            dict(stepTimeSeconds=20.0, mvBefore=85.0, mvAfter=80.0, valveOpening=0.5, role="validation",
                 localGainAt80_bar_per_pct=lin_down["gain_bar_per_pct"]))
    dataset("ol_noisy", open_loop(80.0, step_at(20.0, 85.0, 90.0), lambda t: 0.5, 0.03, 20261103),
            t0 + timedelta(hours=4), 20261103, 0.03, "accepted_with_conditions",
            "Même essai, bruit σ = 0,03 bar (≈ 6 % de ΔPV) : paramètres attendus avec incertitude élargie, surtout le retard.",
            dict(stepTimeSeconds=20.0, mvBefore=85.0, mvAfter=90.0, valveOpening=0.5, role="estimation"))
    dataset("ol_disturbance", open_loop(80.0, step_at(20.0, 85.0, 90.0), step_at(35.0, 0.5, 0.6), nominal_noise, 20261104),
            t0 + timedelta(hours=6), 20261104, nominal_noise, "refused",
            "Ouverture de vanne aval 0,50 -> 0,60 à t = 35 s pendant la réponse : la PV n'est plus la réponse à MV seule ; "
            "un modèle ajusté serait biaisé (gain sous-estimé). Doit être refusé ou signalé par l'analyse des résidus.",
            dict(stepTimeSeconds=20.0, mvBefore=85.0, mvAfter=90.0, disturbance=dict(type="ouverture vanne aval",
                 timeSeconds=35.0, valveBefore=0.5, valveAfter=0.6, recorded=False), role="estimation"))
    dataset("ol_mv_saturation", open_loop(80.0, step_at(20.0, 85.0, 105.0), lambda t: 0.5, nominal_noise, 20261105),
            t0 + timedelta(hours=8), 20261105, nominal_noise, "refused",
            "La commande enregistrée passe à 105 % mais le variateur limite à HSP = 100 % : l'entrée réelle est de +15 % "
            "et non +20 % ; tout gain calculé sur la MV enregistrée est faux. Refus attendu si les limites MV 30–100 % "
            "sont déclarées ; sinon l'incohérence est indétectable à partir du seul CSV (limite documentée).",
            dict(stepTimeSeconds=20.0, mvBefore=85.0, mvAfter=105.0, appliedMvAfter=100.0,
                 declaredMvLimits=[DRIVE["mv_min"], DRIVE["mv_max"]], role="estimation"))
    dataset("ol_insufficient_excitation", open_loop(80.0, step_at(20.0, 85.0, 85.3), lambda t: 0.5, 0.01, 20261106),
            t0 + timedelta(hours=10), 20261106, 0.01, "refused",
            "Échelon de 0,3 % : ΔPV ≈ 0,03 bar pour un bruit σ = 0,01 bar et une résolution de 0,001 bar ; "
            "rapport signal/bruit insuffisant pour un retard et une constante de temps fiables.",
            dict(stepTimeSeconds=20.0, mvBefore=85.0, mvAfter=85.3, role="estimation"))
    big = linearization(55.0, 0.5)
    dataset("ol_out_of_domain", open_loop(80.0, step_at(20.0, 85.0, 55.0), lambda t: 0.5, nominal_noise, 20261107),
            t0 + timedelta(hours=12), 20261107, nominal_noise, "refused",
            "Échelon −30 % : la pression traverse une large plage, le gain local passe de "
            f"{lin['gain_bar_per_pct']:.4f} à {big['gain_bar_per_pct']:.4f} bar/% et la capacité du réservoir varie ; "
            "un FOPDT unique ne représente pas le point nominal. Doit être refusé comme modèle du point nominal "
            "(ou accepté seulement comme modèle moyen, explicitement non local).",
            dict(stepTimeSeconds=20.0, mvBefore=85.0, mvAfter=55.0, linearizationAt55=big, role="estimation"))

    sp_schedule = lambda t: 4.0 if t < 60 else 4.3 if t < 200 else 3.8 if t < 330 else 6.0 if t < 420 else 4.0
    valve_schedule = lambda t: 0.5 if t < 120 else 0.62 if t < 270 else 0.5
    cl_seed = 20261108
    dataset("cl_pi", closed_loop(600.0, sp_schedule, valve_schedule, kc, ti, nominal_noise, cl_seed, 30.0),
            t0 + timedelta(hours=14), cl_seed, nominal_noise, "accepted_with_conditions",
            "Boucle fermée sous PI (SIMC τc = 2θ), manuel 0–30 s puis automatique sans à-coup. Échelons de consigne "
            "4,0 -> 4,3 (60 s) -> 3,8 (200 s) -> 6,0 (330 s, inatteignable : MV saturée à 100 %) -> 4,0 (420 s) ; vanne aval "
            "0,50 -> 0,62 (120 s) -> 0,50 (270 s), non enregistrée. Pour les lots ultérieurs (identification en boucle "
            "fermée, évaluation du réglage, anti-saturation) : à NE PAS traiter comme un essai en boucle ouverte.",
            dict(controller=dict(type="PI idéal générique", Kc_pct_per_bar=kc, Ti_s=ti, antiWindup="blocage conditionnel",
                                 limits=[DRIVE["mv_min"], DRIVE["mv_max"]], manualUntilSeconds=30.0),
                 setpointSchedule=[[0, 4.0], [60, 4.3], [200, 3.8], [330, 6.0], [420, 4.0]],
                 valveSchedule=[[0, 0.5], [120, 0.62], [270, 0.5]], role="boucle fermée"))
    return files


def write(files, out):
    out.mkdir(parents=True, exist_ok=True)
    digests = []
    for name in sorted(files):
        content = files[name]
        data = content if isinstance(content, bytes) else (json.dumps(content, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
        if len(data) > 2_000_000:
            raise RuntimeError(f"{name} dépasse 2 Mo")
        (out / name).write_bytes(data)
        digests.append(f"{hashlib.sha256(data).hexdigest()}  {name}")
    (out / "SHA256SUMS").write_text("\n".join(digests) + "\n", encoding="utf-8")
    return digests


def main(argv=None):
    parser = argparse.ArgumentParser(description=LABEL)
    parser.add_argument("--out", type=Path, default=Path(__file__).resolve().parent / "data")
    parser.add_argument("--check", action="store_true", help="génère deux fois en temporaire et compare les empreintes")
    args = parser.parse_args(argv)
    if args.check:
        with tempfile.TemporaryDirectory() as a, tempfile.TemporaryDirectory() as b:
            first, second = write(build(), Path(a)), write(build(), Path(b))
        print("reproductible" if first == second else "NON REPRODUCTIBLE")
        return 0 if first == second else 1
    for line in write(build(), args.out):
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
