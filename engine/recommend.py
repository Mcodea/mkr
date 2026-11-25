"""Guided PI/PID tuning from an ACCEPTED model, with explicit design objective, uncertainty and refusal.

The user states a design objective (robustness level), never a response time. MKR searches the SIMC family
(Skogestad 2003) for the FASTEST tuning whose peak sensitivity stays within the objective's limit for the nominal model
AND for every corner of the declared parameter uncertainty. When no tuning satisfies this, MKR refuses and says what
data or checks are missing: it never returns a number just to give an answer.

References (all public):
- S. Skogestad, "Simple analytic rules for model reduction and PID controller tuning", J. Process Control 13 (2003) 291-309.
  SIMC rules: Kc = tau1 / (K (tauc + theta)), tauI = min(tau1, 4 (tauc + theta)), tauD = tau2; half rule for the
  second time constant when tau2 <= theta; the effective delay includes half the sampling period.
- K. J. Astrom and T. Hagglund, Advanced PID Control, ISA (2006): the maximum sensitivity Ms is the robustness measure of a
  PID design; values between about 1.2 and 2.0 are the usual design range (lower = more robust).
  Integrating processes: Kc = 1 / (k' (tauc + theta)), tauI = 4 (tauc + theta).
- M. Morari and E. Zafiriou, Robust Process Control (1989): robustness must be assessed on the model uncertainty set.
Limits: the model is linear (FOPDT / SOPDT), the loop is the nominal ideal PI/PID (no actuator limits, no filter of the derivative
is modelled), and the uncertainty set is the box of +/- two standard errors (about 95 % of a Gaussian estimate) of the identified
parameters. This is a design aid, not a stability proof and not a qualification of a vendor block.
"""
import itertools
import math

from pi_tuning_integrator import loop_margins
from stability_margins import analyze

VERSION = "1.0.0"
# Design objectives = maximum sensitivity peak Ms accepted on every corner (Astrom & Hagglund 2006: 1.2 - 2.0 usual range).
OBJECTIVES = {"robust": 1.4, "balanced": 1.7, "performance": 2.0}
COVERAGE = 2.0                      # +/- 2 standard errors: ~95 % of a Gaussian parameter estimate
# Closed-loop time constant tauc searched as multiples of the effective delay (SIMC: tauc = theta is "tight", larger = smoother).
MULTIPLES = [0.25 * 1.25 ** i for i in range(0, 25)]          # 0.25 ... about 53 effective delays, 25 steps


def _number(x):
    return type(x) in (int, float) and math.isfinite(x) and abs(x) <= 1e100


def _simc(gain, tau1, delay, tauc, tau2=None):
    """SIMC ideal-form coefficients; tau2 given -> PID (series form converted to ideal), otherwise PI."""
    kc = tau1 / (gain * (tauc + delay))
    ti = min(tau1, 4 * (tauc + delay))
    if tau2 is None:
        return dict(structure="PI", kp=kc, tiSeconds=ti, tdSeconds=0.0)
    ti_ideal = ti + tau2
    return dict(structure="PID", kp=kc * ti_ideal / ti, tiSeconds=ti_ideal, tdSeconds=ti * tau2 / ti_ideal)


def _corners(gain, tau1, delay, tau2, errors):
    """Nominal plant plus the corners of the +/- COVERAGE sigma box (gain, slow time constant, delay)."""
    plants = [("nominal", gain, tau1, delay, tau2)]
    if errors is None:
        return plants, False
    sg, st, sd = (errors["gain"], errors["tauSeconds"], errors["delaySeconds"])
    low_tau = tau1 - COVERAGE * st
    if low_tau <= 0:
        raise ValueError("recommend_uncertainty")
    for g, t, d in itertools.product((-1, 1), repeat=3):
        k = gain + g * COVERAGE * sg
        if k * gain <= 0:
            raise ValueError("recommend_uncertainty")      # the gain sign itself is not established
        plants.append((f"gain{'+' if g > 0 else '-'},tau{'+' if t > 0 else '-'},delay{'+' if d > 0 else '-'}",
                       k, tau1 + t * COVERAGE * st, max(delay + d * COVERAGE * sd, 0.0), tau2))
    return plants, True


def _evaluate(controller, plants, sampling_delay, stop_above=None):
    """Margins on every plant; with `stop_above`, stops at the first plant whose peak sensitivity exceeds it (the tuning is then rejected)."""
    rows, worst = [], None
    for name, k, t1, d, t2 in plants:
        taus = [t1] if t2 is None else [t1, t2]
        margins = analyze(k, taus, d + sampling_delay, controller["kp"], controller["tiSeconds"], controller["tdSeconds"], 0.0)
        rows.append((name, margins))
        if worst is None or margins["peakSensitivity"] > worst[1]["peakSensitivity"]:
            worst = (name, margins)
        if stop_above is not None and margins["peakSensitivity"] > stop_above:
            break
    return rows, worst


def _is_stable(rows):
    return all("nominal_unstable" not in m["warnings"] and m["phaseMarginDegrees"] is not None and m["phaseMarginDegrees"] > 0 for _, m in rows)


def calculate(payload):
    required = {"family", "gain", "tauSeconds", "delaySeconds", "secondTauSeconds", "sampleSeconds", "standardErrors", "objective"}
    if not isinstance(payload, dict) or payload.keys() != required:
        raise ValueError("recommend_input")
    family, gain, tau, delay, second, sample = (payload[k] for k in ("family", "gain", "tauSeconds", "delaySeconds", "secondTauSeconds", "sampleSeconds"))
    errors, objective = payload["standardErrors"], payload["objective"]
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
        return _integrator(gain, delay, sample, errors, objective, limit)
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
        plants, quantified = _corners(gain, tau1, theta, tau2, errors)
    except ValueError as error:
        return _refusal(family, objective, limit, ["parameter_uncertainty_excludes_the_model"] if str(error) == "recommend_uncertainty" else [str(error)],
                        ["Refaire l'identification avec un essai plus ample ou plus long pour réduire l'incertitude des paramètres."], notes)
    if not quantified:
        notes.append("uncertainty_not_quantified")

    # Phase 1: the fastest grid point that is stable with Ms within the limit on every corner (rejecting early on the first violation).
    chosen_index = None
    for index, multiple in enumerate(MULTIPLES):
        controller = _simc(gain, tau1, effective, multiple * effective, tau2)
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
        controller = _simc(gain, tau1, effective, tauc, tau2)
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
    return dict(algorithmVersion=VERSION, decision="recommended", objective=objective, maximumPeakSensitivity=limit,
                family=family, structure=chosen["controller"]["structure"], recommendation=chosen,
                uncertaintyCoverageSigma=COVERAGE if quantified else None, uncertaintyQuantified=quantified,
                effectiveDelaySeconds=effective, samplePeriodSeconds=sample,
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
                alternatives=[], recommendation=None)


def _integrator(slope, delay, sample, errors, objective, limit):
    """SIMC for an integrating process with dead time; corners on the slope and the delay (the only two parameters)."""
    sampling_delay = sample / 2
    effective = delay + sampling_delay
    notes = ["integrating_process_simc"]
    plants = [("nominal", slope, delay)]
    quantified = errors is not None
    if quantified:
        for g, d in itertools.product((-1, 1), repeat=2):
            k = slope + g * COVERAGE * errors["gain"]
            if k * slope <= 0:
                return _refusal("Integrateur", objective, limit, ["parameter_uncertainty_excludes_the_model"],
                                ["Refaire l'identification avec un essai plus ample ou plus long pour réduire l'incertitude de la pente."], notes)
            plants.append((f"slope{'+' if g > 0 else '-'},delay{'+' if d > 0 else '-'}", k, max(delay + d * COVERAGE * errors["delaySeconds"], 0.0)))
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
                            controller=dict(structure="PI", kp=kp, tiSeconds=ti, tdSeconds=0.0), stableOnAllCorners=stable,
                            worstPeakSensitivity=worst[1], worstCase=worst[0]))
    if chosen_index is None:
        best = min(entries, key=lambda e: e["worstPeakSensitivity"])
        return _refusal("Integrateur", objective, limit, ["no_tuning_meets_the_objective_on_the_uncertainty_set"],
                        ["Réduire l'incertitude (essai plus long, deuxième échelon).", "Ou choisir un objectif moins exigeant en connaissance du compromis."],
                        notes, best, entries)
    chosen = next(e for e in entries if e["multipleOfEffectiveDelay"] == MULTIPLES[chosen_index])
    return dict(algorithmVersion=VERSION, decision="recommended", objective=objective, maximumPeakSensitivity=limit, family="Integrateur",
                structure="PI", recommendation=chosen, uncertaintyCoverageSigma=COVERAGE if quantified else None, uncertaintyQuantified=quantified,
                effectiveDelaySeconds=effective, samplePeriodSeconds=sample, alternatives=_alternatives(entries, chosen), warnings=notes,
                qualification="model_based_unqualified",
                references=["Skogestad 2003, doi:10.1016/S0959-1524(02)00062-8", "Astrom & Hagglund 2006, Advanced PID Control"])
