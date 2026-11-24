"""Documented conversions from MKR's generic PID to manufacturer block parameters.

Every adapter is written ONLY from the manufacturer's official documentation, cites the page it relies on, and refuses
(never guesses) what the documentation does not settle. A conversion is documentary: it is NEVER qualified. Qualification
needs the bench tests of docs/validation/qualification-bloc-banc.md on the real controller and software version.

MKR generic form (continuous, ideal): u = Kp (e + 1/Ti integral(e) + Td de/dt), e = SP - PV, Kp signed like the process gain,
Kp in MV units per PV unit, times in seconds, derivative on the measurement with a first-order filter Tf.
"""
import math

QUALIFICATION = "non_qualified"
PIDFF_SOURCE = dict(document="Schneider Electric, EcoStruxure Control Expert - Régulation - Bibliothèque de blocs, 33002536",
                    edition="09/2020 (33002536.16)", library="Control", block="PIDFF")
PIDINT_SOURCE = dict(document="Schneider Electric, EcoStruxure Control Expert - Bibliothèque de blocs standard, 33002520",
                     edition="09/2020 (33002520.20)", library="Standard", block="PID_INT")


def _number(value, low=None, high=None):
    if type(value) not in (int, float) or not math.isfinite(value) or (low is not None and value < low) or (high is not None and value > high):
        raise ValueError("conversion_parameters")
    return float(value)


def _time_literal(seconds):
    """IEC TIME literal in milliseconds, with a warning when the 1 ms resolution changes the value noticeably."""
    milliseconds = round(seconds * 1000)
    if milliseconds < 1:
        raise ValueError("conversion_time_resolution")
    return f"T#{milliseconds}ms", abs(milliseconds / 1000 - seconds) > 1e-3 * seconds


def pidff(parameters):
    """MKR ideal PID -> PIDFF, MIXED structure (mix_par = 0), derivative on the measurement (pv_dev = 0).

    Transfer function of the mixed structure (33002536 p. 173): OUT = kp [1 + 1/(ti p) + td p / (1 + (td/kd) p)] x IN,
    IN = PV - SP, physical units. It equals MKR's ideal form with Kp = kp, Ti = ti, Td = td, filter Tf = td/kd.
    Direction (p. 182): rev_dir = 1 raises the output for a positive deviation PV - SP, rev_dir = 0 lowers it. MKR's
    u = Kp (SP - PV) with Kp > 0 lowers the output when PV rises: rev_dir = 0. A negative Kp gives rev_dir = 1.
    The block refuses a negative kp (status 080, FAQ FA410692): the sign always goes through rev_dir.
    """
    if not isinstance(parameters, dict) or not {"kp", "tiSeconds", "tdSeconds"} <= parameters.keys() <= \
            {"kp", "tiSeconds", "tdSeconds", "filterSeconds", "structure"}:
        raise ValueError("conversion_parameters")
    if parameters.get("structure", "mixed") != "mixed":
        # p. 176 says kp does not act on I and D in the parallel structure, but the detailed equation of TermI (p. 183)
        # contains kp: the documentation is not consistent, so no parallel conversion is offered.
        raise ValueError("conversion_parallel_undocumented")
    kp = _number(parameters["kp"])
    ti, td = _number(parameters["tiSeconds"], 0), _number(parameters["tdSeconds"], 0)
    if kp == 0 or ti <= 0:
        raise ValueError("conversion_parameters")
    kd = None
    if td > 0:
        tf = parameters.get("filterSeconds")
        if tf is None:
            raise ValueError("conversion_filter_required")          # the block always filters its derivative: Tf = td / kd
        kd = td / _number(tf, 0)
        if not math.isfinite(kd) or kd <= 0:
            raise ValueError("conversion_parameters")
        if kd < 1:
            raise ValueError("conversion_kd_below_one")             # the block replaces kd < 1 by 1 when td <> 0 (FA410692)
    ti_literal, ti_rounded = _time_literal(ti)
    td_literal, td_rounded = _time_literal(td) if td > 0 else ("T#0ms", False)
    warnings = ["integral_uses_current_deviation", "label_inconsistency_rev_dir", "sampled_behaviour_unqualified"]
    if ti_rounded or td_rounded:
        warnings.append("time_rounded_to_milliseconds")
    return dict(
        block="PIDFF", structure="mixed", qualification=QUALIFICATION, readyToLoad=False, source=PIDFF_SOURCE,
        parameters=dict(
            kp=abs(kp), ti=dict(seconds=ti, iec=ti_literal), td=dict(seconds=td, iec=td_literal),
            kd=kd, rev_dir=kp < 0, mix_par=False, pv_dev=False),
        neutral=dict(ovs_att=0.0, dband=0.0, ff_equal_bounds="ff_sup = ff_inf", outbias=0.0, en_rcpy=False),
        toDetermine=["pv_inf", "pv_sup", "out_inf", "out_sup", "out_min", "out_max", "outrate", "aw_type", "bump", "execution period dt"],
        equations=[
            dict(page=173, name="transfer function, mixed structure", text="OUT = kp x [1 + 1/(ti p) + td p / (1 + (td/kd) p)] x IN"),
            dict(page=182, name="direction of action", text="rev_dir = 1: PV - SP > 0 gives a higher output; rev_dir = 0: a lower output"),
            dict(page=183, name="incremental algorithm (ti > 0, aw_type = 0)",
                 text="OUTD = TermP + TermI + TermD + TermFF ; TermP = sense x kp x delta(DEV_WGH) ; TermI = sense x kp x dt/ti x dev"),
        ],
        warnings=warnings)


def pid_int(parameters):
    """MKR ideal PI -> PID_INT (INT arithmetic, signals normalised to [0..10000]).

    33002520 p. 141: DOUT = KP [de(t) + TS/(10 TI) e(t)] / 100, e = SP - PV in counts (p. 137), KP = register / 100,
    TI in tenths of a second, TS in hundredths. With e in counts, KP = Kp x pv_span / mv_span (counts of output per
    count of measurement). The derivative action Dt has no equation in the document: td > 0 is refused. Sign: a positive
    KP raises the output when PV falls, which is MKR's Kp > 0 (the manual's "positif : arriere" is consistent with it).
    """
    keys = {"kp", "tiSeconds", "tdSeconds", "tsSeconds", "pvSpan", "mvSpan"}
    if not isinstance(parameters, dict) or parameters.keys() != keys:
        raise ValueError("conversion_parameters")
    kp = _number(parameters["kp"])
    ti, td, ts = _number(parameters["tiSeconds"], 0), _number(parameters["tdSeconds"], 0), _number(parameters["tsSeconds"], 0)
    pv_span, mv_span = _number(parameters["pvSpan"], 0), _number(parameters["mvSpan"], 0)
    if kp == 0 or ti <= 0 or ts <= 0 or pv_span <= 0 or mv_span <= 0:
        raise ValueError("conversion_parameters")
    if td > 0:
        raise ValueError("conversion_derivative_undocumented")
    gain = kp * pv_span / mv_span
    registers = dict(KP=round(gain * 100), TI=round(ti * 10), TS=round(ts * 100), TD=0)
    if registers["KP"] == 0 or abs(registers["KP"]) > 10000 or not 1 <= registers["TI"] <= 20000 or not 1 <= registers["TS"] <= 32000:
        raise ValueError("conversion_range")
    warnings = ["integral_uses_current_deviation", "task_period_multiple", "integer_arithmetic_unqualified", "sampled_behaviour_unqualified",
                "document_inconsistencies"]
    rounded = [abs(registers[name] / factor - value) > 1e-3 * abs(value) for name, factor, value in
               (("KP", 100, gain), ("TI", 10, ti), ("TS", 100, ts))]
    if any(rounded):
        warnings.append("register_rounding")
    return dict(
        block="PID_INT", structure="mixed", qualification=QUALIFICATION, readyToLoad=False, source=PIDINT_SOURCE,
        parameters=dict(KP=registers["KP"], TI=registers["TI"], TD=0, TS=registers["TS"], PV_DEV=False, gain=gain,
                        units=dict(KP="gain x100", TI="0.1 s", TS="0.01 s")),
        neutral=dict(),
        toDetermine=["OUT_MAX", "OUT_MIN", "NO_BUMP", "PV_SUP", "PV_INF", "task period (TS is rounded to a multiple)",
                     "scaling of the physical MV and PV to [0..10000]"],
        equations=[
            dict(page=141, name="incremental PI, TI > 0", text="DOUT = KP [de(t) + TS/(10 TI) e(t)] / 100 ; OUT = OUT + DOUT ; e = SP - PV"),
            dict(page=141, name="TI = 0", text="OUT = KP e / 100 + 5000"),
            dict(page=140, name="units", text="KP x100, TI in 0.1 s, TS in 0.01 s")],
        warnings=warnings)
