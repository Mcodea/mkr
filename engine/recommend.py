"""Guided PI/PID tuning from an ACCEPTED model, with explicit design objective, uncertainty and refusal.

The user states a design objective (robustness level), never a response time. MKR searches the SIMC family
(Skogestad 2003) for the FASTEST tuning whose peak sensitivity stays within the objective's limit for the nominal model
AND for every corner of the declared parameter uncertainty. When no tuning satisfies this, MKR refuses and says what
data or checks are missing: it never returns a number just to give an answer.

References (all public):
- S. Skogestad, "Simple analytic rules for model reduction and PID controller tuning", J. Process Control 13 (2003) 291-309.
  SIMC rules: Kc = tau1 / (K (tauc + theta)), tauI = min(tau1, 4 (tauc + theta)), tauD = tau2; half rule for the
  second time constant when tau2 <= theta; the effective delay includes half the sampling period.
  Derivative filter: Tf = Td / N, N = 10 by default (usual range 2-20), included in the margins of the loop that is judged.
- K. J. Astrom and T. Hagglund, Advanced PID Control, ISA (2006): the maximum sensitivity Ms is the robustness measure of a
  PID design; values between about 1.2 and 2.0 are the usual design range (lower = more robust).
  Integrating processes: Kc = 1 / (k' (tauc + theta)), tauI = 4 (tauc + theta).
- M. Morari and E. Zafiriou, Robust Process Control (1989): robustness must be assessed on the model uncertainty set.
Limits: the model is linear (FOPDT / SOPDT), the loop is the nominal ideal PI/PID (no actuator limits, no filter of the derivative
is modelled), and the uncertainty set is the box of +/- two standard errors (about 95 % of a Gaussian estimate) of the identified
parameters, widened to the process variation (percent of gain, time constant and delay) that the user declares for the operating range. This is a design aid, not a stability proof and not a qualification of a vendor block.
"""
import itertools
import math

from pi_tuning_integrator import loop_margins
from stability_margins import analyze

VERSION = "1.0.0"
# Design objectives = maximum sensitivity peak Ms accepted on every corner (Astrom & Hagglund 2006: 1.2 - 2.0 usual range).
OBJECTIVES = {"robust": 1.4, "balanced": 1.7, "performance": 2.0}
# Derivative filter Tf = Td / N: N between 2 and 20 is the usual range (Astrom & Hagglund 2006); 10 is the default when the user gives none.
DEFAULT_FILTER_RATIO = 10.0
COVERAGE = 2.0                      # +/- 2 standard errors: ~95 % of a Gaussian parameter estimate
# Closed-loop time constant tauc searched as multiples of the effective delay (SIMC: tauc = theta is "tight", larger = smoother).
MULTIPLES = [0.25 * 1.25 ** i for i in range(0, 25)]          # 0.25 ... about 53 effective delays, 25 steps


def _number(x):
    return type(x) in (int, float) and math.isfinite(x) and abs(x) <= 1e100


def _simc(gain, tau1, delay, tauc, tau2=None, filter_ratio=DEFAULT_FILTER_RATIO):
    """SIMC ideal-form coefficients; tau2 given -> PID (series form converted to ideal), otherwise PI."""
    kc = tau1 / (gain * (tauc + delay))
    ti = min(tau1, 4 * (tauc + delay))
    if tau2 is None:
        return dict(structure="PI", kp=kc, tiSeconds=ti, tdSeconds=0.0, filterSeconds=0.0)
    ti_ideal = ti + tau2
    td = ti * tau2 / ti_ideal
    return dict(structure="PID", kp=kc * ti_ideal / ti, tiSeconds=ti_ideal, tdSeconds=td, filterSeconds=td / filter_ratio)


def _half_widths(gain, tau1, delay, errors, variation):
    """Half-width of the uncertainty box per parameter: the larger of +/- COVERAGE standard errors and the declared process variation."""
    sg, st, sd = (errors["gain"], errors["tauSeconds"], errors["delaySeconds"]) if errors is not None else (0.0, 0.0, 0.0)
    fraction = 0.0 if variation is None else variation / 100.0
    return (max(COVERAGE * sg, fraction * abs(gain)), max(COVERAGE * st, fraction * tau1), max(COVERAGE * sd, fraction * delay))


def _corners(gain, tau1, delay, tau2, errors, variation=None):
    """Nominal plant plus the corners of the uncertainty box (gain, slow time constant, delay)."""
    plants = [("nominal", gain, tau1, delay, tau2)]
    if errors is None and variation is None:
        return plants, False
    hg, ht, hd = _half_widths(gain, tau1, delay, errors, variation)
    if tau1 - ht <= 0 or abs(gain) - hg <= 0:
        raise ValueError("recommend_uncertainty")          # the gain sign or a positive time constant is no longer established
    for g, t, d in itertools.product((-1, 1), repeat=3):
        plants.append((f"gain{'+' if g > 0 else '-'},tau{'+' if t > 0 else '-'},delay{'+' if d > 0 else '-'}",
                       gain + g * hg, tau1 + t * ht, max(delay + d * hd, 0.0), tau2))
    return plants, True


def _evaluate(controller, plants, sampling_delay, stop_above=None):
    """Margins on every plant; with `stop_above`, stops at the first plant whose peak sensitivity exceeds it (the tuning is then rejected)."""
    rows, worst = [], None
    for name, k, t1, d, t2 in plants:
        taus = [t1] if t2 is None else [t1, t2]
        margins = analyze(k, taus, d + sampling_delay, controller["kp"], controller["tiSeconds"], controller["tdSeconds"], controller["filterSeconds"])
        rows.append((name, margins))
        if worst is None or margins["peakSensitivity"] > worst[1]["peakSensitivity"]:
            worst = (name, margins)
        if stop_above is not None and margins["peakSensitivity"] > stop_above:
            break
    return rows, worst


def _is_stable(rows):
    return all("nominal_unstable" not in m["warnings"] and m["phaseMarginDegrees"] is not None and m["phaseMarginDegrees"] > 0 for _, m in rows)


def calculate(payload):
    required = {"family", "gain", "tauSeconds", "delaySeconds", "secondTauSeconds", "sampleSeconds", "standardErrors", "objective", "derivativeFilterRatio",
                "processVariationPercent", "optimise"}
    if not isinstance(payload, dict) or payload.keys() != required:
        raise ValueError("recommend_input")
    family, gain, tau, delay, second, sample = (payload[k] for k in ("family", "gain", "tauSeconds", "delaySeconds", "secondTauSeconds", "sampleSeconds"))
    errors, objective = payload["standardErrors"], payload["objective"]
    ratio = payload["derivativeFilterRatio"]
    if ratio is not None and (not _number(ratio) or not 2 <= ratio <= 100):
        raise ValueError("recommend_input")
    ratio = DEFAULT_FILTER_RATIO if ratio is None else float(ratio)
    if type(payload["optimise"]) is not bool:
        raise ValueError("recommend_input")
    optimise = payload["optimise"]
    variation = payload["processVariationPercent"]
    if variation is not None and (not _number(variation) or not 0 < variation <= 50):
        raise ValueError("recommend_input")
    if family not in ("FOPDT", "SOPDT", "Integrateur") or objective not in OBJECTIVES:
        raise ValueError("recommend_input")
    if family == "Integrateur":
        if tau is not None or second is not None or not all(_number(x) for x in (gain, delay, sample)):
            raise ValueError("recommend_input")
        tau = 1.0                       # unused placeholder for the shared checks below
        if errors is not None and errors.get("tauSeconds") != 0:
            raise ValueError("recommend_input")
    elif not all(_number(x) for x in (gain, tau, delay, sample)) or (family == "SOPDT") != (second is not None):
        raise ValueError("recommend_input")
    if second is not None and not _number(second):
        raise ValueError("recommend_input")
    if errors is not None and (not isinstance(errors, dict) or errors.keys() != {"gain", "tauSeconds", "delaySeconds"} or
                               not all(_number(v) and v >= 0 for v in errors.values())):
        raise ValueError("recommend_input")
    if gain == 0 or tau <= 0 or delay < 0 or sample <= 0 or (second is not None and not 0 < second <= tau):
        raise ValueError("recommend_domain")

    limit = OBJECTIVES[objective]
    if family == "Integrateur":
        return _integrator(gain, delay, sample, errors, objective, limit, variation, optimise)
    notes, tau1, tau2, theta = [], tau, None, delay
    if family == "SOPDT":
        if second > delay:
            tau2 = second                  # PID: the second time constant is worth cancelling (Skogestad 2003)
            notes.append("pid_second_pole_exceeds_delay")
        else:
            tau1, theta = tau + second / 2, delay + second / 2          # half rule (Skogestad 2003)
            notes.append("half_rule_second_pole_not_above_delay")
    else:
        notes.append("first_order_model_pi_is_the_rule")
    sampling_delay = sample / 2        # effective delay includes half the sampling period (Skogestad 2003)
    effective = theta + sampling_delay
    try:
        plants, quantified = _corners(gain, tau1, theta, tau2, errors, variation)
    except ValueError as error:
        return _refusal(family, objective, limit, ["parameter_uncertainty_excludes_the_model"] if str(error) == "recommend_uncertainty" else [str(error)],
                        ["Refaire l'identification avec un essai plus ample ou plus long pour réduire l'incertitude des paramètres."], notes)
    if not quantified:
        notes.append("uncertainty_not_quantified")
    elif family == "SOPDT":
        notes.append("second_pole_uncertainty_not_propagated")

    # Phase 1: the fastest grid point that is stable with Ms within the limit on every corner (rejecting early on the first violation).
    chosen_index = None
    for index, multiple in enumerate(MULTIPLES):
        controller = _simc(gain, tau1, effective, multiple * effective, tau2, ratio)
        try:
            rows, worst = _evaluate(controller, plants, sampling_delay, stop_above=limit)
        except ValueError:
            continue
        if _is_stable(rows) and len(rows) == len(plants) and worst[1]["peakSensitivity"] <= limit:
            chosen_index = index
            break
    # Phase 2: complete evidence (all corners) for the chosen tuning and its neighbours on the speed / robustness trade-off.
    window = range(len(MULTIPLES) - 3, len(MULTIPLES)) if chosen_index is None else range(max(0, chosen_index - 2), min(len(MULTIPLES), chosen_index + 4))
    evaluated, chosen = [], None
    for index in window:
        multiple = MULTIPLES[index]
        tauc = multiple * effective
        controller = _simc(gain, tau1, effective, tauc, tau2, ratio)
        try:
            rows, worst = _evaluate(controller, plants, sampling_delay)
        except ValueError:
            continue
        entry = dict(responseSeconds=tauc, multipleOfEffectiveDelay=multiple, controller=controller, stableOnAllCorners=_is_stable(rows),
                     worstPeakSensitivity=worst[1]["peakSensitivity"], worstCase=worst[0], nominalMargins=rows[0][1])
        evaluated.append(entry)
        if index == chosen_index:
            chosen = entry
    if chosen is None:
        best = min((e for e in evaluated), key=lambda e: e["worstPeakSensitivity"], default=None)
        reasons = ["no_tuning_meets_the_objective_on_the_uncertainty_set"]
        actions = ["Réduire l'incertitude (essai d'identification plus long, deuxième échelon, bruit plus faible).",
                   "Ou choisir un objectif moins exigeant (« équilibré » ou « performance ») en connaissance du compromis.",
                   "Ou envisager une structure différente (compensation du retard) hors du périmètre de MKR."]
        return _refusal(family, objective, limit, reasons, actions, notes, best, evaluated)

    warnings = list(notes)
    if chosen["responseSeconds"] > tau1:
        warnings.append("closed_loop_slower_than_the_process")
    optimized = None
    if optimise and tau2 is None:
        optimized = optimise_pi(family, gain, tau1, effective, plants, limit, sampling_delay)
        if optimized is not None:
            c = chosen["controller"]
            optimized["integralGainRatio"] = optimized["integralGain"] / (abs(c["kp"]) / c["tiSeconds"])
    elif optimise:
        warnings.append("optimisation_covers_pi_only")
    return dict(algorithmVersion=VERSION, decision="recommended", objective=objective, maximumPeakSensitivity=limit,
                family=family, structure=chosen["controller"]["structure"], recommendation=chosen,
                uncertaintyCoverageSigma=COVERAGE if errors is not None else None, uncertaintyQuantified=quantified,
                processVariationPercent=variation, effectiveDelaySeconds=effective, samplePeriodSeconds=sample,
                optimized=optimized, derivativeFilterRatio=ratio if chosen["controller"]["tdSeconds"] > 0 else None,
                alternatives=_alternatives(evaluated, chosen), warnings=warnings, qualification="model_based_unqualified",
                references=["Skogestad 2003, doi:10.1016/S0959-1524(02)00062-8", "Astrom & Hagglund 2006, Advanced PID Control",
                            "Morari & Zafiriou 1989, Robust Process Control"])


def _alternatives(evaluated, chosen):
    """The chosen tuning and its neighbours on the speed / robustness trade-off, for the user to compare."""
    index = evaluated.index(chosen)
    picks = [evaluated[i] for i in range(max(0, index - 2), min(len(evaluated), index + 4))]
    return [dict(responseSeconds=e["responseSeconds"], controller=e["controller"], worstPeakSensitivity=e["worstPeakSensitivity"],
                 stableOnAllCorners=e["stableOnAllCorners"]) for e in picks]


def _refusal(family, objective, limit, reasons, actions, notes, best=None, evaluated=()):
    return dict(algorithmVersion=VERSION, decision="refused", objective=objective, maximumPeakSensitivity=limit, family=family,
                reasons=reasons, actions=actions, closestTuning=best, warnings=notes, qualification="none",
                alternatives=[], recommendation=None, optimized=None)


def _integrator(slope, delay, sample, errors, objective, limit, variation=None, optimise=False):
    """SIMC for an integrating process with dead time; corners on the slope and the delay (the only two parameters)."""
    sampling_delay = sample / 2
    effective = delay + sampling_delay
    notes = ["integrating_process_simc"]
    plants = [("nominal", slope, delay)]
    quantified = errors is not None or variation is not None
    if quantified:
        fraction = 0.0 if variation is None else variation / 100.0
        hs = max(COVERAGE * (errors["gain"] if errors is not None else 0.0), fraction * abs(slope))
        hd = max(COVERAGE * (errors["delaySeconds"] if errors is not None else 0.0), fraction * delay)
        if abs(slope) - hs <= 0:
            return _refusal("Integrateur", objective, limit, ["parameter_uncertainty_excludes_the_model"],
                            ["Refaire l'identification avec un essai plus ample ou plus long pour réduire l'incertitude de la pente."], notes)
        for g, d in itertools.product((-1, 1), repeat=2):
            plants.append((f"slope{'+' if g > 0 else '-'},delay{'+' if d > 0 else '-'}", slope + g * hs, max(delay + d * hd, 0.0)))
    else:
        notes.append("uncertainty_not_quantified")

    def evaluate(tauc, stop=None):
        kp, ti = 1 / (slope * (tauc + effective)), 4 * (tauc + effective)
        worst, stable = ("nominal", 0.0), True
        for name, k, d in plants:
            gain_margin, phase_margin, peak = loop_margins(k, d + sampling_delay, kp, ti)
            stable = stable and phase_margin is not None and phase_margin > 0 and (gain_margin is None or gain_margin > 1)
            if peak > worst[1]:
                worst = (name, peak)
            if stop is not None and peak > stop:
                break
        return kp, ti, worst, stable

    chosen_index = None
    for index, multiple in enumerate(MULTIPLES):
        # tauc is measured from the effective delay: effective delay + tauc = (1 + multiple) * effective ... keep the SIMC definition
        _, _, worst, stable = evaluate(multiple * effective, stop=limit)
        if stable and worst[1] <= limit:
            chosen_index = index
            break
    window = range(len(MULTIPLES) - 3, len(MULTIPLES)) if chosen_index is None else range(max(0, chosen_index - 2), min(len(MULTIPLES), chosen_index + 4))
    entries = []
    for index in window:
        tauc = MULTIPLES[index] * effective
        kp, ti, worst, stable = evaluate(tauc)
        entries.append(dict(responseSeconds=tauc, multipleOfEffectiveDelay=MULTIPLES[index],
                            controller=dict(structure="PI", kp=kp, tiSeconds=ti, tdSeconds=0.0, filterSeconds=0.0), stableOnAllCorners=stable,
                            worstPeakSensitivity=worst[1], worstCase=worst[0]))
    if chosen_index is None:
        best = min(entries, key=lambda e: e["worstPeakSensitivity"])
        return _refusal("Integrateur", objective, limit, ["no_tuning_meets_the_objective_on_the_uncertainty_set"],
                        ["Réduire l'incertitude (essai plus long, deuxième échelon).", "Ou choisir un objectif moins exigeant en connaissance du compromis."],
                        notes, best, entries)
    chosen = next(e for e in entries if e["multipleOfEffectiveDelay"] == MULTIPLES[chosen_index])
    optimized = None
    if optimise:
        optimized = optimise_pi("Integrateur", slope, 1.0, effective, [(n, k, 1.0, d, None) for n, k, d in plants], limit, sampling_delay)
        if optimized is not None:
            optimized["integralGainRatio"] = optimized["integralGain"] / (abs(chosen["controller"]["kp"]) / chosen["controller"]["tiSeconds"])
    return dict(algorithmVersion=VERSION, decision="recommended", objective=objective, maximumPeakSensitivity=limit, family="Integrateur",
                structure="PI", recommendation=chosen, uncertaintyCoverageSigma=COVERAGE if errors is not None else None, uncertaintyQuantified=quantified,
                processVariationPercent=variation, optimized=optimized, effectiveDelaySeconds=effective, samplePeriodSeconds=sample, alternatives=_alternatives(entries, chosen), warnings=notes,
                qualification="model_based_unqualified",
                references=["Skogestad 2003, doi:10.1016/S0959-1524(02)00062-8", "Astrom & Hagglund 2006, Advanced PID Control"])


# ---------------------------------------------------------------------------------------------------------------------------------
# Constrained optimisation of the PI integral gain (Astrom, Panagopoulos and Hagglund, "Design of PI controllers based on non-convex
# optimization", Automatica 34 (1998) 585-601): maximise Ki = Kp / Ti, which sets the integrated error of a load disturbance
# (IAE ~ 1/Ki for a well damped loop), under the maximum sensitivity constraint on EVERY corner of the uncertainty box.
# ---------------------------------------------------------------------------------------------------------------------------------
import numpy as np

_W = np.logspace(-4, 2, 2500)           # coarse grid used inside the search; the result is verified with the full-resolution margins


def _loop(w, kp, ti, plants, integrating):
    s = 1j * w[None, :]
    gains = np.array([p[1] for p in plants], dtype=float)[:, None]
    delays = np.array([p[3] for p in plants], dtype=float)[:, None]
    process = gains / s if integrating else gains / (np.array([p[2] for p in plants], dtype=float)[:, None] * s + 1)
    return kp * (1 + 1 / (ti * s)) * process * np.exp(-s * delays)


def _feasible(kp, ti, plants, limit, integrating, w):
    """Stable on every plant and peak sensitivity <= limit (coarse grid)."""
    loop = _loop(w, kp, ti, plants, integrating)
    sensitivity = 1 / np.abs(1 + loop)
    if float(sensitivity.max()) > limit:
        return False
    # exact continuous phase (no unwrapping ambiguity at low frequency): PI -atan(1/(Ti w)), process, dead time
    w2 = w[None, :]
    delays = np.array([p[3] for p in plants], dtype=float)[:, None]
    lag = -math.pi / 2 if integrating else -np.arctan(np.array([p[2] for p in plants], dtype=float)[:, None] * w2)
    phase = -np.arctan2(1.0, ti * w2) + lag - w2 * delays
    for row in range(loop.shape[0]):
        if phase[row, 0] <= -math.pi and abs(loop[row, 0]) >= 1:
            return False                    # already beyond -180 degrees where the loop gain is above one: unstable (Ti too short)
        crossing = np.flatnonzero((phase[row, :-1] > -math.pi) & (phase[row, 1:] <= -math.pi))
        if len(crossing) and abs(loop[row, crossing[0]]) >= 1:
            return False
    return True




def _max_kp(ti, plants, limit, integrating, w, x_grid):
    """Largest Kp for which the loop is stable with Ms <= limit on every plant (scan, then bisection against the next infeasible point)."""
    scale = 1.0 / max(abs(plants[0][1]), 1e-300)
    feasible = [bool(_feasible(x * scale, ti, plants, limit, integrating, w)) for x in x_grid]
    indices = [i for i, ok in enumerate(feasible) if ok]
    if not indices or indices[-1] == len(x_grid) - 1:
        return 0.0                  # nothing feasible, or still feasible at the end of the physical range: do not trust an open end
    low, high = x_grid[indices[-1]] * scale, x_grid[indices[-1] + 1] * scale
    for _ in range(30):
        mid = (low + high) / 2
        low, high = (mid, high) if _feasible(mid, ti, plants, limit, integrating, w) else (low, mid)
    return float(low)


def _nominal_overshoot(gain, tau, delay, kp, ti, integrating):
    """Overshoot (percent of the unit set-point step) of the nominal loop: explicit Euler simulation, delay buffer."""
    scale = max(ti, delay, tau if not integrating else 0.0, 1e-9)
    dt = max(min(scale, delay if delay > 0 else scale) / 40, scale / 4000)
    steps = int(min(60 * scale, 5e5 * dt) / dt)
    lag = int(round(delay / dt))
    buffer, state, integral, output, peak = [0.0] * (lag + 1), 0.0, 0.0, 0.0, 0.0
    for _ in range(steps):
        error = 1.0 - output
        integral += error * dt
        buffer.append(kp * (error + integral / ti))
        delayed = buffer.pop(0)
        if integrating:
            state += dt * gain * delayed
        else:
            state += dt * (gain * delayed - state) / tau
        output = state
        peak = max(peak, output)
    return max(0.0, (peak - 1.0) * 100.0)


def optimise_pi(family, gain, tau1, theta_total, plants, limit, sampling_delay):
    """Best PI (largest Ki) for the corner set, or None. `plants` use positive gains; delays include the sampling delay."""
    integrating = family == "Integrateur"
    scaled = [(n, abs(k), t1, d + sampling_delay, t2) for n, k, t1, d, t2 in plants]
    scale = max(theta_total, 1e-9)
    # frequency grid and gain range cover every crossover a stable loop can have (below about 20 / theta_total)
    w = np.logspace(math.log10(1e-3 / max(tau1 if not integrating else 20 * scale, scale)), math.log10(100 / scale), 3000)
    x_grid = np.geomspace(1e-3 / scale, 20 / scale, 160) if integrating else np.geomspace(1e-3, 20 * max(1.0, tau1 / scale), 160)
    ti_grid = np.geomspace(0.05 * tau1, 3 * tau1, 18) if not integrating else np.geomspace(0.5 * scale, 400 * scale, 22)
    best = None
    for ti in ti_grid:
        kp = _max_kp(ti, scaled, limit, integrating, w, x_grid)
        if kp > 0 and (best is None or kp / ti > best[0]):
            best = (kp / ti, kp, ti)
    if best is None:
        return None
    # local refinement of Ti around the best grid point
    for factor in np.geomspace(0.8, 1.25, 9):
        ti = best[2] * factor
        kp = _max_kp(ti, scaled, limit, integrating, w, x_grid)
        if kp > 0 and kp / ti > best[0]:
            best = (kp / ti, kp, ti)
    _, kp, ti = best
    # full-resolution verification (the module's own margin search); back off on the coarse-grid error
    for _ in range(40):
        try:
            peaks = []
            for _, k, t1, d, t2 in scaled:
                if integrating:
                    _, pm, peak = loop_margins(k, d, kp, ti)
                    ok = pm is not None and pm > 0
                else:
                    m = analyze(k, [t1], d, kp, ti, 0.0, 0.0)
                    peak, ok = m["peakSensitivity"], "nominal_unstable" not in m["warnings"] and m["phaseMarginDegrees"] is not None and m["phaseMarginDegrees"] > 0
                if not ok:
                    peak = math.inf
                peaks.append(peak)
        except ValueError:
            return None
        if max(peaks) <= limit:
            nominal = scaled[0]
            return dict(controller=dict(structure="PI", kp=math.copysign(kp, gain), tiSeconds=ti, tdSeconds=0.0, filterSeconds=0.0),
                        integralGain=kp / ti, worstPeakSensitivity=max(peaks),
                        nominalOvershootPercent=_nominal_overshoot(nominal[1], nominal[2] if not integrating else 0.0, nominal[3], kp, ti, integrating))
        kp *= 0.995
    return None
