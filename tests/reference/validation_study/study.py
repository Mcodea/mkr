"""Génération des cas et mesure des statistiques de l'étude de validation (protocole docs/science/lot4/protocole-validation-critere.md). DONNÉES SYNTHÉTIQUES.

Un CAS = un procédé vrai tiré par graine + un essai d'estimation + un essai de validation indépendant, bruités. Pour chaque cas on enregistre :
  * les STATISTIQUES des critères (calculées ici, à partir des sorties du moteur et des mesures) ; les décisions dépendant d'un seuil calibré sont prises à l'analyse ;
  * la VÉRITÉ : comportement du réglage recommandé à partir du modèle identifié, sur le procédé VRAI (truth.py)."""
import base64
import math
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

import numpy as np
from scipy import stats

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent.parent.parent / "engine"))

import plants  # noqa: E402
import truth  # noqa: E402

AMPLITUDE = 5.0
BASELINE_ROWS = 20
SNRS = (20, 50, 200, 1000)
POPULATIONS_T1 = ("P1", "P2", "P3", "P4", "P6")
POPULATIONS_T2 = ("P5", "P7")


def draw_plant(population, rng):
    tau = 10 ** rng.uniform(math.log10(2), math.log10(200))
    theta = tau * 10 ** rng.uniform(math.log10(0.05), math.log10(2))
    gain = float(rng.choice([-1.0, 1.0])) * 10 ** rng.uniform(-1, 1)
    info = dict(population=population, tau=tau, theta=theta, gain=gain)
    if population in ("P1", "P4"):
        plant = plants.fopdt(gain, tau, theta)
    elif population == "P2":
        rho = 10 ** rng.uniform(math.log10(0.02), math.log10(0.5))
        plant = plants.sopdt(gain, tau, rho * tau, theta)
        info["rho"] = rho
    elif population == "P3":
        n = int(rng.choice([3, 4]))
        plant = plants.lag_chain(gain, tau, n, theta)
        info["n"] = n
    elif population == "P6":
        rho = 10 ** rng.uniform(math.log10(0.05), math.log10(0.3))
        beta = tau * 10 ** rng.uniform(math.log10(0.05), math.log10(0.5))
        plant = plants.inverse_response(gain, tau, rho * tau, beta, theta)
        info.update(rho=rho, beta=beta)
    elif population == "P5":
        kappa = float(rng.choice([-1.0, 1.0])) * rng.uniform(0.1, 0.5)
        plant = plants.hammerstein(gain, tau, theta, kappa, AMPLITUDE)
        info["kappa"] = kappa
    elif population == "P7":
        plant = plants.fopdt(gain, tau, theta)
        info["gain_change"] = float(rng.choice([-1.0, 1.0])) * rng.uniform(0.1, 0.4)
    else:
        raise ValueError(population)
    return plant, info


def noise_series(n, sigma, rng, phi=0.0):
    w = rng.standard_normal(n)
    if phi == 0.0:
        return sigma * w
    e = np.empty(n)
    e[0] = sigma * w[0]
    for k in range(1, n):
        e[k] = phi * e[k - 1] + math.sqrt(1 - phi * phi) * sigma * w[k]
    return e


def make_trial(plant, ts, n_post, amplitude, sigma, rng, phi=0.0, drift_total=0.0, day=1):
    n = BASELINE_ROWS + n_post
    u = np.zeros(n)
    u[BASELINE_ROWS:] = amplitude
    clean = plant.simulate(u, ts)
    pv = 50.0 + clean + noise_series(n, sigma, rng, phi) + drift_total * np.arange(n) / max(n - 1, 1)
    start = datetime(2026, 4, day, tzinfo=timezone.utc)
    lines = ["timestamp,mv,pv"] + [f"{(start + timedelta(seconds=i * ts)).isoformat(timespec='microseconds')},{float(u[i])!r},{float(pv[i])!r}" for i in range(n)]
    source = dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",", timeColumn="timestamp", mvColumn="mv", pvColumn="pv")
    return source, u, pv, clean


def acf(r, lag):
    r = np.asarray(r) - np.mean(r)
    d = float(np.dot(r, r))
    return float(np.dot(r[:-lag], r[lag:]) / d) if d > 0 else 0.0


def ljung_box(r, m, parameters):
    """Statistique Q de Ljung-Box et sa p-valeur (chi-deux à m - parameters degrés de liberté)."""
    n = len(r)
    q = n * (n + 2) * sum(acf(r, k) ** 2 / (n - k) for k in range(1, m + 1))
    dof = max(m - parameters, 1)
    return float(q), float(1 - stats.chi2.cdf(q, dof))


def cross_correlation_max(residual, u, max_lag):
    """Plus grand |corrélation croisée| résidu/entrée sur les retards 0..max_lag (entrée centrée) et sa borne à 99 % (2,576/racine(N))."""
    r = np.asarray(residual) - np.mean(residual)
    v = np.asarray(u) - np.mean(u)
    n = len(r)
    denominator = math.sqrt(float(np.dot(r, r) * np.dot(v, v)))
    if denominator == 0:
        return 0.0, 2.576 / math.sqrt(n)
    values = [abs(float(np.dot(r[lag:], v[:n - lag])) / denominator) for lag in range(0, max_lag + 1)]
    return max(values), 2.576 / math.sqrt(n)
