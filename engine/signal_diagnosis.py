"""Diagnostic AUTOMATIQUE d'un signal enregistré : saturation, perte de capteur possible, cadence. Aucun seuil n'est saisi par l'utilisateur.

Règles (fixées AVANT l'évaluation sur les jeux B et C de S01 ; paramètres a priori, jamais ajustés sur ces données) :
  * SATURATION — plateau exact au maximum ou au minimum OBSERVÉ du signal pendant au moins MIN_RUN échantillons consécutifs. Un actionneur saturé reste
    exactement à sa borne ; un signal continu bruité ne répète pas exactement son extremum sur MIN_RUN échantillons. MIN_RUN = 3 est le plus petit nombre
    qui distingue un plateau d'une répétition fortuite de deux valeurs quantifiées voisines. Le diagnostic rapporte les INTERVALLES et la part de temps
    saturée : c'est à l'identification d'exclure ces fenêtres, pas à une règle globale de refus.
  * PERTE DE CAPTEUR POSSIBLE (signal mesuré) — échantillons exactement égaux au minimum observé (ou au maximum) qui sont atteints par un SAUT aberrant depuis
    leur voisin : score Z modifié de la différence > 3,5, seuil et statistique de Iglewicz & Hoaglin (1993), « How to detect and handle outliers », ASQC,
    fondés sur la médiane et la MAD (robustes). Un zéro réel au démarrage qui n'est pas atteint par un saut n'est pas signalé. C'est une SUSPICION : aucune valeur
    n'est supprimée ni remplacée.
  * CADENCE — horodatages dupliqués (fait, sans seuil) et dispersion des pas positifs : un horodatage dupliqué signifie que la résolution de l'horloge est plus
    grossière que l'échantillonnage réel (la période déduite des horodatages est alors fausse).
La sortie est descriptive et motivée ; elle ne modifie jamais les données.
"""
import math
from statistics import median

MIN_RUN = 3
ROBUST_Z = 3.5                       # Iglewicz & Hoaglin 1993
MAD_TO_SIGMA = 0.6745                # constante du Z modifié : 0,6745 (Iglewicz & Hoaglin)


def _runs(flags):
    """Intervalles [début, fin] (inclus) des True consécutifs."""
    out, start = [], None
    for i, f in enumerate(flags):
        if f and start is None:
            start = i
        elif not f and start is not None:
            out.append((start, i - 1)); start = None
    if start is not None:
        out.append((start, len(flags) - 1))
    return out


def saturation(values):
    finite = [v for v in values if v is not None and math.isfinite(v)]
    if len(finite) < 2 * MIN_RUN:
        return dict(intervals=[], fraction=0.0, level=None)
    result = []
    for side, level in (("max", max(finite)), ("min", min(finite))):
        runs = [(a, b) for a, b in _runs([v == level for v in values]) if b - a + 1 >= MIN_RUN]
        if runs:
            result.append(dict(side=side, level=level, intervals=runs, samples=sum(b - a + 1 for a, b in runs)))
    samples = sum(r["samples"] for r in result)
    return dict(bounds=result, fraction=samples / len(values))


def possible_sensor_dropout(values):
    """Indices d'échantillons suspects de perte de capteur (valeur exactement à un extremum observé, atteinte par un saut aberrant)."""
    finite = [v for v in values if v is not None and math.isfinite(v)]
    if len(finite) < 8:
        return []
    jumps = [abs(b - a) for a, b in zip(values, values[1:]) if a is not None and b is not None and math.isfinite(a) and math.isfinite(b)]
    mid = median(jumps)
    mad = median(abs(j - mid) for j in jumps)
    scale = mad if mad > 0 else (sum(jumps) / len(jumps) if sum(jumps) > 0 else 0.0)
    if scale == 0:
        return []
    flagged = set()
    for level in (min(finite), max(finite)):
        for a, b in _runs([v == level for v in values]):
            # saut d'entrée ou de sortie du plateau, mesuré avec le voisin hors plateau
            edges = []
            if a > 0 and values[a - 1] is not None: edges.append(abs(values[a] - values[a - 1]))
            if b + 1 < len(values) and values[b + 1] is not None: edges.append(abs(values[b + 1] - values[b]))
            if edges and max(0.6745 * (e - mid) / scale if mad > 0 else (e - mid) / scale for e in edges) > ROBUST_Z and (b - a + 1) < len(values) // 2:
                flagged.update(range(a, b + 1))
    return sorted(flagged)


def cadence(timestamps_s):
    steps = [b - a for a, b in zip(timestamps_s, timestamps_s[1:])]
    positive = [s for s in steps if s > 0]
    duplicates = sum(1 for s in steps if s == 0)
    return dict(samples=len(timestamps_s), duplicate_timestamps=duplicates, duplicate_fraction=duplicates / max(1, len(steps)),
                median_step_s=median(positive) if positive else None, min_step_s=min(positive) if positive else None, max_step_s=max(positive) if positive else None,
                reversed=sum(1 for s in steps if s < 0),
                clock_resolution_coarser_than_sampling=duplicates > 0)


def diagnose(timestamps_s, values, role):
    """role : 'MV' (commande : la saturation compte) ou 'PV' (mesure : la saturation et la perte de capteur comptent)."""
    if role not in ("MV", "PV") or len(timestamps_s) != len(values):
        raise ValueError("signal_diagnosis_input")
    out = dict(algorithmVersion="1.0.0", saturation=saturation(values), cadence=cadence(timestamps_s))
    if role == "PV":
        out["possibleSensorDropout"] = possible_sensor_dropout(values)
    return out
