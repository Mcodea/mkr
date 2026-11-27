"""Assistant d'essai en temps réel — tranche 1 (cahier directeur, section 42.2 à 42.4) : procédé autorégulant, boucle ouverte, échelon sur MV.

Fonction PURE et SANS ÉCRITURE : elle lit un tampon d'acquisition (temps, MV, PV, mode éventuel) et dit où en est l'essai. Elle ne commande rien,
ne propose jamais un niveau d'échelon et ne signale JAMAIS une autorisation de sécurité : `safetyAuthorization` vaut toujours False. L'état
« ready_for_step » veut seulement dire que la précondition SCIENTIFIQUE (régime initial stable) est satisfaite ; l'échelon reste soumis aux
limites d'exploitation et à la décision de l'opérateur habilité (section 42.5).

États : acquiring_initial_regime, initial_conditions_not_met, ready_for_step, step_detected, response_in_progress, more_data_needed, usable,
excitation_not_a_step, excitation_insufficient, saturation_detected, measurement_anomaly, analysis_not_supported, unusable, not_supported_yet.
Toute estimation est PROVISOIRE (provisional=True) : les résultats définitifs n'existent qu'après validation indépendante (42.3).

Critères (aucun seuil universel : tout est ramené aux tolérances déclarées ou à l'estimateur lui-même) :
  * échelon = première variation de MV supérieure à 2 x mvTolerance (même règle que fopdt.read_step et step_finder) ; amplitude <= 10 x mvTolerance :
    excitation insuffisante (même règle que fopdt.read_step) ;
  * régime initial stable = les >= 4 dernières lignes (minimum de fopdt.read_step, i >= 4) restent à moins de pvTolerance de leur moyenne ;
  * la réponse est analysée par broida.estimate en mode provisoire ; « établie » = critère de broida (reste prévu <= 2 x pvTolerance et plancher 4 tau) ;
  * durée restante = durée requise (L + tau ln(|réponse|/(2 tol)) + fenêtre, au moins L + 4 tau) - durée observée. Elle est CONDITIONNELLE au modèle
    FOPDT supposé et n'est donnée que lorsque l'estimation existe (plateau observé) : sinon `remainingSeconds` est None, jamais une fausse précision (42.4).
Procédé INTÉGRATEUR (tranche 2) : aucun plateau n'est attendu (section 42.3). La réponse est une rampe après le retard, ajustée par integrator.estimate ;
« exploitable » quand (a) le PV a varié de plus de 4 x pvTolerance en fin de fenêtre (règle d'integrator.identify), (b) le retard est localisé dans la
première moitié de l'horizon (l'estimateur signale sinon `parameter_bound`) et (c) la pente est significativement non nulle au niveau 99 % (z = 2,576) :
|pente| > 2,576 x erreur type, erreur type = sigma / racine(somme (t - t moyen)^2) sur les lignes après le retard, sigma = max(bruit de ligne de base mesuré,
tolérance PV / racine 3) (résolution déclarée). La durée restante n'est PAS estimée (`remainingSeconds` None) : sans plateau, aucune durée n'est fondée.
Limites de cette tranche : boucle fermée, grand temps mort, oscillant, réponse inverse : état `not_supported_yet` / `analysis_not_supported`."""
import math

import numpy as np

import broida
import integrator
from fopdt import Step

VERSION = "1.0.0"
MAXIMUM_ROWS = 200_000
MINIMUM_BASELINE_ROWS = 4          # fopdt.read_step : i >= 4
MINIMUM_RESPONSE_ROWS = 12         # fopdt.read_step : len(rows) - i >= 12
INSUFFICIENT_RATIO = 10            # fopdt.read_step : |delta| <= 10 x tolerance
STATES = ("acquiring_initial_regime", "initial_conditions_not_met", "ready_for_step", "step_detected", "response_in_progress", "more_data_needed",
          "usable", "excitation_not_a_step", "excitation_insufficient", "saturation_detected", "measurement_anomaly", "analysis_not_supported",
          "unusable", "not_supported_yet")
KEYS = {"seconds", "mv", "pv", "mvTolerance", "pvTolerance"}
OPTIONAL = {"sp", "manual", "loopMode", "processKind", "mvLow", "mvHigh", "maxDurationSeconds", "hasValidationTrial"}


def _vector(payload, name):
    values = payload[name]
    if not isinstance(values, list) or not 2 <= len(values) <= MAXIMUM_ROWS:
        raise ValueError("assess_data")
    if any(type(v) not in (int, float) or isinstance(v, bool) for v in values):
        raise ValueError("assess_data")
    return np.array(values, dtype=float)


def _number(value, low=1e-100, high=1e100):
    if type(value) not in (int, float) or isinstance(value, bool) or not low <= value <= high:
        raise ValueError("assess_tolerance")
    return float(value)


def _result(state, messages, rows, **extra):
    assert state in STATES
    return dict(algorithmVersion=VERSION, state=state, messages=list(messages), provisional=True, safetyAuthorization=False,
                rowsAnalysed=int(rows), stepRow=extra.pop("stepRow", None), remainingSeconds=extra.pop("remainingSeconds", None),
                remainingConditionalOnModel=extra.pop("remainingConditionalOnModel", False), estimate=extra.pop("estimate", None),
                evidence=extra.pop("evidence", None), validationTrialNeeded=extra.pop("validationTrialNeeded", None), **extra)


def _stable_tail(pv, tolerance):
    """Nombre de dernières lignes dont tous les points restent à moins de `tolerance` de leur moyenne (fenêtre la plus longue)."""
    best = 0
    for size in range(1, len(pv) + 1):
        window = pv[-size:]
        if np.max(np.abs(window - np.mean(window))) <= tolerance:
            best = size
        else:
            break
    return best


def assess(payload):
    if not isinstance(payload, dict) or not KEYS <= payload.keys() <= KEYS | OPTIONAL:
        raise ValueError("assess_mapping")
    seconds, mv, pv = (_vector(payload, k) for k in ("seconds", "mv", "pv"))
    if not len(seconds) == len(mv) == len(pv) or np.any(~np.isfinite(seconds)) or np.any(np.diff(seconds) <= 0):
        raise ValueError("assess_data")
    mv_tol, pv_tol = _number(payload["mvTolerance"]), _number(payload["pvTolerance"])
    loop_mode = payload.get("loopMode", "open_loop")
    kind = payload.get("processKind", "self_regulating")
    if loop_mode not in ("open_loop", "closed_loop") or kind not in ("self_regulating", "integrating", "unknown"):
        raise ValueError("assess_mapping")
    manual = payload.get("manual")
    if manual is not None:
        if not isinstance(manual, list) or len(manual) != len(seconds) or any(type(v) not in (int, float, bool) for v in manual):
            raise ValueError("assess_data")
    limits = [payload.get(k) for k in ("mvLow", "mvHigh")]
    for limit in limits:
        if limit is not None:
            _number(limit, -1e100, 1e100)
    max_duration = payload.get("maxDurationSeconds")
    if max_duration is not None:
        _number(max_duration)
    validated = bool(payload.get("hasValidationTrial", False))
    n = len(seconds)

    if not (np.all(np.isfinite(mv)) and np.all(np.isfinite(pv))):
        return _result("measurement_anomaly", ["non_finite_values"], n)
    if loop_mode != "open_loop" or kind not in ("self_regulating", "integrating"):
        # Boucle fermée (excitation SP), intégrateur sans plateau, grand temps mort, oscillant, réponse inverse : aucune règle justifiée ici.
        return _result("not_supported_yet", ["mode_or_process_kind_not_supported_by_this_version"], n)
    if manual is not None and any(float(v) != 1.0 for v in manual):
        return _result("initial_conditions_not_met", ["loop_not_in_manual"], n)

    jumps = np.flatnonzero(np.abs(np.diff(mv)) > 2 * mv_tol) + 1          # indices de ligne où MV a changé
    if not len(jumps):
        stable = _stable_tail(pv, pv_tol)
        if n < MINIMUM_BASELINE_ROWS:
            return _result("acquiring_initial_regime", ["acquiring_initial_regime"], n, stableRows=stable)
        if stable >= MINIMUM_BASELINE_ROWS:
            return _result("ready_for_step", ["initial_regime_stable", "scientific_precondition_only_not_a_safety_authorisation"], n,
                           stableRows=stable, stableSeconds=float(seconds[-1] - seconds[-stable]))
        return _result("initial_conditions_not_met", ["initial_regime_not_stable"], n, stableRows=stable)

    k = int(jumps[0])
    if len(jumps) > 1 and jumps[1] == k + 1:
        return _result("excitation_not_a_step", ["mv_changes_over_several_rows_ramp_or_pulse"], n, stepRow=k)
    end = int(jumps[1]) if len(jumps) > 1 else n                           # un second changement de MV termine l'essai
    notes = ["second_mv_change_ends_the_trial"] if len(jumps) > 1 else []
    if k < MINIMUM_BASELINE_ROWS:
        return _result("unusable", ["baseline_too_short_before_step"] + notes, n, stepRow=k)
    mv_before, mv_after = float(np.mean(mv[:k])), float(np.mean(mv[k:end]))
    baseline = float(np.mean(pv[:k]))
    if np.max(np.abs(pv[:k] - baseline)) > pv_tol:
        return _result("unusable", ["baseline_not_stable_before_step"] + notes, n, stepRow=k)
    if np.max(np.abs(mv[:k] - mv_before)) > mv_tol or np.max(np.abs(mv[k:end] - mv_after)) > mv_tol:
        return _result("excitation_not_a_step", ["mv_not_constant_around_the_step"] + notes, n, stepRow=k)
    amplitude = mv_after - mv_before
    if abs(amplitude) <= INSUFFICIENT_RATIO * mv_tol:
        return _result("excitation_insufficient", ["mv_step_too_small"] + notes, n, stepRow=k, amplitude=amplitude)
    low, high = limits
    if (high is not None and np.any(mv[k:end] >= high - mv_tol)) or (low is not None and np.any(mv[k:end] <= low + mv_tol)):
        return _result("saturation_detected", ["mv_at_declared_actuator_limit"] + notes, n, stepRow=k, amplitude=amplitude)
    observed = float(seconds[end - 1] - seconds[k])
    if end - k < MINIMUM_RESPONSE_ROWS:
        return _result("step_detected", ["step_detected", "response_window_too_short_to_analyse"] + notes, n, stepRow=k, amplitude=amplitude,
                       observedSeconds=observed)
    step = Step(seconds[k:end] - seconds[k], pv[k:end], baseline, amplitude, float(seconds[k] - seconds[k - 1]), None, None,
                float(np.std(pv[:k], ddof=1)), k)
    exhausted = max_duration is not None and observed >= max_duration
    if kind == "integrating":
        return _integrating(step, k, n, amplitude, observed, exhausted, validated, pv_tol, notes)
    try:
        gain, tau, delay, t28, t40, level, response, residual = broida.estimate(step, pv_tol, require_established=False)
    except ValueError as error:
        code = str(error)
        base = dict(stepRow=k, amplitude=amplitude, observedSeconds=observed)
        if code == "broida_response":
            if exhausted:
                return _result("unusable", ["excitation_insufficient_no_measurable_response_at_max_duration"] + notes, n, **base)
            return _result("response_in_progress", ["no_measurable_response_yet", "remaining_not_reliable"] + notes, n, **base)
        if code in ("broida_horizon", "broida_plateau"):
            if exhausted:
                return _result("unusable", ["response_not_settled_at_max_duration"] + notes, n, **base)
            return _result("response_in_progress", ["response_in_progress", "remaining_not_reliable"] + notes, n, **base)
        return _result("analysis_not_supported", [code, "response_shape_outside_the_two_point_method"] + notes, n, **base)
    window = float(step.times[-1] - step.times[-broida.TAIL_SIZE])
    required = max(broida.required_duration_seconds(response, pv_tol, tau, delay, window), delay + broida.LEGACY_HORIZON_TAUS * tau)
    established = observed >= required and residual <= 2 * pv_tol
    estimate = dict(gain=gain, tauSeconds=tau, delaySeconds=delay, method="broida_two_point_fopdt", provisional=True)
    evidence = dict(crossing28Seconds=t28, crossing40Seconds=t40, baselinePv=baseline, crossing28Pv=baseline + broida.FRACTIONS[0] * response,
                    crossing40Pv=baseline + broida.FRACTIONS[1] * response, terminalPv=level, requiredDurationSeconds=required,
                    observedDurationSeconds=observed, settledResidualBoundPv=residual)
    base = dict(stepRow=k, amplitude=amplitude, observedSeconds=observed, estimate=estimate, evidence=evidence)
    if established:
        return _result("usable", ["trial_usable_provisional"] + (["independent_validation_trial_needed"] if not validated else []) + notes, n,
                       validationTrialNeeded=not validated, **base)
    if exhausted:
        return _result("unusable", ["response_not_established_at_max_duration"] + notes, n, **base)
    return _result("more_data_needed", ["more_data_needed", "remaining_conditional_on_fopdt_model"] + notes, n,
                   remainingSeconds=max(0.0, required - observed), remainingConditionalOnModel=True, **base)


SLOPE_Z_99 = 2.576                 # pente significative au niveau 99 % (même niveau que fopdt.LJUNG_Z_99)


def _integrating(step, k, n, amplitude, observed, exhausted, validated, pv_tol, notes):
    """Procédé intégrateur : rampe après le retard, sans plateau. Voir le docstring du module."""
    base = dict(stepRow=k, amplitude=amplitude, observedSeconds=observed)
    y = step.observations - step.baseline
    if float(np.max(np.abs(y))) <= 4 * pv_tol:
        if exhausted:
            return _result("unusable", ["excitation_insufficient_no_measurable_response_at_max_duration"] + notes, n, **base)
        return _result("response_in_progress", ["no_measurable_response_yet", "remaining_not_reliable", "no_plateau_expected_for_integrating"] + notes, n, **base)
    try:
        gain, delay, fraction = integrator.estimate(step)
    except ValueError as error:
        return _result("analysis_not_supported", [str(error)] + notes, n, **base)
    window = max(2, len(y) // 8)
    tail_change = float(np.mean(step.observations[-window:]) - np.mean(step.observations[-2 * window:-window]))
    sign = math.copysign(1.0, gain * amplitude)
    ramp = step.times > delay
    rows = int(np.count_nonzero(ramp))
    sigma = max(step.baseline_noise, pv_tol / math.sqrt(3))
    spread = float(np.sum((step.times[ramp] - np.mean(step.times[ramp])) ** 2)) if rows >= 3 else 0.0
    slope = gain * amplitude
    significant = spread > 0 and abs(slope) > SLOPE_Z_99 * sigma / math.sqrt(spread)
    estimate = dict(gain=gain, delaySeconds=delay, method="integrator_ramp_fit", provisional=True)
    evidence = dict(delayFractionOfHorizon=fraction, slopePvPerSecond=slope, rampRows=rows, slopeSigma=sigma,
                    tailChangePv=tail_change, baselinePv=float(step.baseline))
    base.update(estimate=estimate, evidence=evidence)
    notes = notes + ["no_plateau_expected_for_integrating"]
    if sign * tail_change > 4 * pv_tol and fraction < 0.49 and significant:
        return _result("usable", ["trial_usable_provisional"] + (["independent_validation_trial_needed"] if not validated else []) + notes, n,
                       validationTrialNeeded=not validated, **base)
    if exhausted:
        return _result("unusable", ["ramp_not_established_at_max_duration"] + notes, n, **base)
    return _result("more_data_needed", ["more_data_needed", "remaining_not_reliable"] + notes, n, **base)
