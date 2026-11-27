"""Executable model of the Schneider Electric PIDFF block (EcoStruxure Control Expert, Control library).

Source (only source used; page numbers are the printed pages of this edition):
    Schneider Electric, "EcoStruxure Control Expert - Process Control - Block Library", 33002535, edition .16, 09/2020,
    chapter 17 "PIDFF: Complete PID controller", p. 163-189, plus the library-wide rules of p. 34-35 (status word)
    and the error table p. 354. The equations of the manual are images: they were read on the rendered pages.
This model is NOT qualified against a real controller: it reproduces what the manual writes, and every place where
the manual is silent, ambiguous or self-contradictory is listed below and marked "INTERPRETATION" in the code.

Implemented
    * Para_PIDFF (p. 167-168) with the substitutions of p. 188-189 (bit 4): kp, dband, gain_kp, outrate < 0 -> 0;
      kd < 1 with td <> 0 -> 1; ovs_att clamped to [0, 1]; out_min/out_max clamped to [out_inf, out_sup];
      outbias, otff_inf, otff_sup clamped to [out_min - out_max, out_max - out_min].
    * Absolute algorithm, ti = 0 (p. 169, 178); incremental algorithm, ti > 0, aw_type 0 and 1 (p. 179-180);
      integral mode kp = 0 (p. 181-182); mixed structure; parallel structure only where it is consistent (below).
    * Derivative on PV or on dev (pv_dev, p. 178), first-order filter kd (p. 174, 178), setpoint weighting ovs_att
      (p. 173, 177), dead band dband/gain_kp (p. 174), feed forward (p. 175, 179), outbias (p. 169, 175, 178),
      outrate (p. 168, 171, 175), en_rcpy (p. 173, 179-180), limiter out_min/out_max in automatic mode and
      out_inf/out_sup in manual and tracking modes, OUTD not limited (p. 175-176), modes and bump (p. 176),
      SP clamped to [pv_inf, pv_sup] (p. 175, 188), status bits 1, 3, 4, 5, 6, 7 (p. 35, 188), pv_inf = pv_sup and
      non-finite inputs leave OUT, OUTD, MA_O and INFO unchanged (p. 188).

Refused (ValueError), because the manual is contradictory or silent
    * mix_par = 1 with ti > 0 and kp > 0: p. 172 and p. 177 say that in the parallel structure kp does not act on the
      integral and derivative components (their gain is K = alpha), but the detailed TermI of p. 179-180 contains kp.
      Both cannot hold. (mix_par = 1 with ti = 0 is consistent: TermP = kp x dev and TermD uses K = alpha, which is
      the parallel transfer function of p. 169; mix_par = 1 with kp = 0 is the integral mode of p. 181, gain alpha.)
    * bump = 1 with ti > 0: p. 176 says only "the changeover has a bump"; the incremental equations of p. 179-182
      contain no bump term, so the size of the bump is not documented.
    * Non-finite or ill-typed parameters, ti or td < 0, pv_inf > pv_sup, out_inf > out_sup, dt <= 0: no documented
      behaviour (a real block measures dt itself, p. 177).

INTERPRETATIONS (none of them is a fact of the manual)
    I1  TermD memory. p. 178-180 write TermD = sense x [td x TermD(old) + K td kd dVAR] / (kd dt + td), inside a
        Delta[...] in the incremental form. Read literally with sense = -1 the memory changes sign every cycle (pole
        -td/(kd dt + td)), which contradicts the transfer function of p. 169. The model keeps the unsigned filtered
        component X(k) = [td X(k-1) + K td kd (VAR(k) - VAR(k-1))] / (kd dt + td); absolute TermD = sense X(k);
        incremental TermD = sense (X(k) - X(k-1)). This equals the literal text for sense = +1.
    I2  First execution: every (old) value equals the current one (all Delta = 0), X(old) = 0, OUT(old) = OUTc(old)
        = OUTP(old) = the initial value of the OUT in/out parameter (constructor argument out_init).
    I3  Dead band. p. 174: inside the band the deviation is multiplied by gain_kp and feeds P and I; the detailed
        equations p. 178-182 do not show it. The model tests |dev| < dband (strict: "lies below dband") and applies
        the factor gain_kp both to dev (TermI; TermP of the absolute form) and to DEV_WGH (TermP of the incremental
        form, the diagram p. 171 shows a dead-band block on DEV_WGH). The derivative uses the raw VAR (p. 178).
    I4  outrate = 0 means "no rate limit", by analogy with VEL_LIM p. 111 ("RATE = 0, becomes OUT = IN"); the PIDFF
        chapter gives no neutral value. The rate limit (|OUT(new) - OUT(old)| <= outrate x dt) acts in automatic mode
        only, between the computed value and the out_min/out_max limiter, as on the diagram p. 171. It does not act on
        OUTD, OUTc or OUTP.
    I5  Order "limiter then OUT(old) + OUTD" of p. 179: OUT(old) is the limited value of the previous cycle (standard
        incremental anti-windup); OUTD is the unlimited sum of the terms (p. 175).
    I6  en_rcpy = 1 with aw_type = 0: OUT = RCPY + OUTD is not limited (note p. 173), not rate-limited, and bits 5/6
        stay 0. With aw_type = 1, RCPY only enters TermAW (p. 180) and OUT = limiter(OUTc) as written.
    I7  Absolute OUTD (p. 178 "OUTD = OUTP(new) - OUTP(old)", the minus sign is missing on the page; p. 169 says
        OUT(new) - OUT(old)): OUTP is the computed value before rate limit and limiter, so OUTD is not limited (p. 175).
    I8  Manual and tracking modes: OUTD = OUT(new) - OUT(old) (definition p. 166). The memories DEV_WGH(old), VAR(old),
        out_ff(old) and X keep being updated every cycle; OUTc and OUTP follow OUT. Hence the first automatic cycle
        uses one-cycle differences: bumpless for ti > 0. If no manual value is written, OUT keeps its last value.
    I9  ti = 0, bump = 0, Manual/Tracking -> Automatic: outbias is recalculated on the first automatic cycle so that
        OUTP = OUT(old) (p. 176), and the recalculated value replaces outbias afterwards (not clamped). bump = 1: the
        parameter outbias is used as is, the output jumps.
    I10 Bits 5/6: in automatic mode they are set when OUT equals (has "reached") out_min / out_max; in manual and
        tracking modes when the requested value is strictly outside [out_inf, out_sup] (note p. 188), so they drop
        at the next cycle. If out_min > out_max after substitution, OUT is set to out_max (p. 35, note 2) and both bits
        are set.
    I11 Feed forward: no sense factor (out_ff is added after the reverse/direct block, p. 171, 175, 178-179). When
        ff_sup = ff_inf, TermFF = 0 and Info.out_ff = 0.
    I12 Error cycle (non-finite input -> bit 1; pv_inf = pv_sup -> bit 7, code -30121 E_ERR_NULL_INPUT_SCALE p. 354;
        non-finite result -> bit 3): OUT, OUTD, MA_O, INFO and every internal memory are left unchanged; STATUS
        holds the error bit (plus bit 4 if a parameter is substituted). Every REAL input is checked, used or not.
    I13 Integral mode (kp = 0, ti > 0, p. 181-182): no TermP and no TermD, td is ignored; TermI uses alpha.
    I14 rev_dir: the labels of p. 167 ("0: opposite", "1: direct") contradict p. 178; the model uses p. 178:
        sense = +1 for rev_dir = 1 (PV - SP > 0 raises the output), -1 for rev_dir = 0.

Comparison with engine/pidff_reference.py (project-written oracle, read line by line)
    * Same TermP, TermI, TermD (same interpretation I1), DEV_WGH, VAR and sense for its domain (ti > 0, kp > 0,
      aw_type = 0, en_rcpy = 0, mixed, no dead band, no FF, no limits): the two models agree there (checked in a test).
    * It cites the French edition 33002536 (p. 173/177/182/183); this model cites 33002535 (p. 169/177/178/179).
    * It refuses kp < 0 and kd < 1 (ValueError); the block substitutes 0 and 1 with bit 4 (p. 188), as here.
    * It accepts kp = 0 with ti > 0 and then computes TermI with kp, i.e. no action at all; p. 181 says the block runs
      in integral mode with gain alpha. Different behaviour.
    * It has no limiter, outrate, aw_type 1, en_rcpy, dead band, feed forward, outbias, absolute form, SP clamp,
      modes, status or error handling; its OUT is unbounded. Its prime() is I2 with explicit previous values.

Not documented (left out or refused): bump size for ti > 0; meaning of outrate = 0 (I4); exact rounding of TIME
(1 ms) and of REAL (32-bit floats on the PLC, Python floats here); floating-point error codes other than bits 1/3;
the id element (autotuning) and the AUTOTUNE coupling.
"""

import math
from dataclasses import dataclass

BIT_INVALID_INPUT = 1 << 1      # p. 35: unauthorized (non-finite) value on a floating point input
BIT_OVERFLOW = 1 << 3           # p. 35: capacity overflow during floating point calculation
BIT_CLAMPED = 1 << 4            # p. 35, 188
BIT_LOW = 1 << 5                # p. 35, 188
BIT_HIGH = 1 << 6               # p. 35, 188
BIT_NULL_SCALE = 1 << 7         # p. 35, 188
E_ERR_NULL_INPUT_SCALE = -30121  # p. 354

REAL_KEYS = ("pv_inf", "pv_sup", "out_inf", "out_sup", "kp", "ti", "td", "kd", "dband", "gain_kp", "ovs_att",
             "outbias", "out_min", "out_max", "outrate", "ff_inf", "ff_sup", "otff_inf", "otff_sup")
BOOL_KEYS = ("rev_dir", "mix_par", "aw_type", "en_rcpy", "pv_dev", "bump")


@dataclass(frozen=True)
class PidffResult:
    out: float
    outd: float
    ma_o: bool
    dev: float          # Info_PIDFF.dev = PV - SP (p. 168), SP after clamping
    out_ff: float       # Info_PIDFF.out_ff (p. 168)
    status: int
    error: object = None  # None, "FP_ERROR" or E_ERR_NULL_INPUT_SCALE


def _real(name, value):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"pidff_parameter_{name}")
    return float(value)


def _bool(name, value):
    if isinstance(value, bool):
        return value
    if isinstance(value, int) and value in (0, 1):
        return bool(value)
    raise ValueError(f"pidff_parameter_{name}")


def _clamp(value, low, high):
    return low if value < low else high if value > high else value


class PidffModel:
    def __init__(self, para, out_init=0.0):
        if not isinstance(para, dict):
            raise ValueError("pidff_parameters")
        expected = set(REAL_KEYS) | set(BOOL_KEYS)
        if not expected <= para.keys() <= expected | {"id"}:
            raise ValueError("pidff_parameters")
        p = {k: _real(k, para[k]) for k in REAL_KEYS}
        p.update({k: _bool(k, para[k]) for k in BOOL_KEYS})
        if p["ti"] < 0 or p["td"] < 0:                      # TIME values; negative durations are not documented
            raise ValueError("pidff_parameter_time")
        if p["pv_inf"] > p["pv_sup"]:                       # not documented (pv_inf = pv_sup is a runtime error, p. 188)
            raise ValueError("pidff_parameter_pv_range")
        if p["out_inf"] > p["out_sup"]:                     # not documented
            raise ValueError("pidff_parameter_out_range")
        self.substitutions = []
        # p. 188-189: negative kp, dband, gain_kp, outrate -> 0 (bit 4)
        for key in ("kp", "dband", "gain_kp", "outrate"):
            if p[key] < 0:
                p[key] = 0.0
                self.substitutions.append(key)
        if p["td"] != 0 and p["kd"] < 1:                    # p. 188: kd < 1 with td <> 0 -> 1
            p["kd"] = 1.0
            self.substitutions.append("kd")
        if not 0 <= p["ovs_att"] <= 1:                      # p. 188: ovs_att clamped to [0, 1]
            p["ovs_att"] = _clamp(p["ovs_att"], 0.0, 1.0)
            self.substitutions.append("ovs_att")
        for key in ("out_min", "out_max"):                  # p. 188: clamped to [out_inf, out_sup]
            if not p["out_inf"] <= p[key] <= p["out_sup"]:
                p[key] = _clamp(p[key], p["out_inf"], p["out_sup"])
                self.substitutions.append(key)
        low, high = p["out_min"] - p["out_max"], p["out_max"] - p["out_min"]
        for key in ("outbias", "otff_inf", "otff_sup"):     # p. 188: clamped to [out_min - out_max, out_max - out_min]
            if p[key] < low or p[key] > high:
                p[key] = low if p[key] < low else high
                self.substitutions.append(key)
        if p["mix_par"] and p["ti"] > 0 and p["kp"] > 0:
            raise ValueError("pidff_parallel_incremental_contradictory")   # p. 172/177 vs p. 179-180
        if p["bump"] and p["ti"] > 0:
            raise ValueError("pidff_bump_incremental_undocumented")        # p. 176 vs p. 179-182
        self.p = p
        self.sense = 1.0 if p["rev_dir"] else -1.0          # p. 178 (INTERPRETATION I14 on the p. 167 labels)
        self._outbias = p["outbias"]
        out_init = _real("out_init", out_init)
        # Outputs and memories (INTERPRETATION I2)
        self.out = out_init
        self.outd = 0.0
        self.ma_o = False
        self.dev = 0.0
        self.out_ff = 0.0
        self._outc = out_init       # OUTc(old), aw_type = 1 (p. 180)
        self._outp = out_init       # OUTP(old), absolute form (p. 178)
        self._memory = None         # (DEV_WGH, VAR, out_ff, X) of the previous cycle
        self._mode = None

    def _status(self):
        return BIT_CLAMPED if self.substitutions else 0

    def _unchanged(self, bits, error):
        # p. 188: OUT, OUTD, MA_O and INFO remain unchanged (INTERPRETATION I12: memories too)
        return PidffResult(self.out, self.outd, self.ma_o, self.dev, self.out_ff, self._status() | bits, error)

    def _auto_limit(self, value, status):
        """out_min/out_max limiter (p. 175-176); inverted thresholds -> out_max (p. 35 note 2); bits 5/6 (I10)."""
        low, high = self.p["out_min"], self.p["out_max"]
        value = high if low >= high else _clamp(value, low, high)
        if value <= low:
            status |= BIT_LOW
        if value >= high:
            status |= BIT_HIGH
        return value, status

    def _rate(self, value, dt):
        rate = self.p["outrate"]                            # INTERPRETATION I4
        if rate == 0:
            return value
        return _clamp(value, self.out - rate * dt, self.out + rate * dt)

    def step(self, pv, sp, dt, man_auto=True, ff=0.0, rcpy=0.0, tr_i=0.0, tr_s=False, out=None):
        """One execution. dt = time since the previous execution in seconds (p. 177). out = value written by the
        user into the OUT in/out parameter (used in manual mode only; None = not written)."""
        if isinstance(dt, bool) or not isinstance(dt, (int, float)) or not math.isfinite(dt) or dt <= 0:
            raise ValueError("pidff_dt")
        man_auto, tr_s = _bool("man_auto", man_auto), _bool("tr_s", tr_s)
        values = [pv, sp, ff, rcpy, tr_i] + ([] if out is None else [out])
        if any(isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) for v in values):
            return self._unchanged(BIT_INVALID_INPUT, "FP_ERROR")          # p. 35 bit 1, p. 188
        p, sense = self.p, self.sense
        if p["pv_inf"] == p["pv_sup"]:
            return self._unchanged(BIT_NULL_SCALE, E_ERR_NULL_INPUT_SCALE)  # p. 188 bit 7, p. 354
        status = self._status()
        sp_used = _clamp(float(sp), p["pv_inf"], p["pv_sup"])              # p. 175, 188: SP limited, bit 4
        if sp_used != sp:
            status |= BIT_CLAMPED
        dev = pv - sp_used                                                  # Info_PIDFF.dev = PV - SP (p. 168)
        alpha = (p["out_sup"] - p["out_inf"]) / (p["pv_sup"] - p["pv_inf"])  # p. 169, 177
        kp = p["kp"]
        gain = kp if (not p["mix_par"] and kp != 0) else alpha              # K (p. 177)
        factor = p["gain_kp"] if abs(dev) < p["dband"] else 1.0            # dead band (p. 174, INTERPRETATION I3)
        dev_m = factor * dev
        dev_wgh = factor * (pv - (1 - p["ovs_att"]) * sp_used)              # DEV_WGH (p. 177), I3
        var = pv if not p["pv_dev"] else dev                                # VAR (p. 178)
        if p["ff_sup"] != p["ff_inf"]:                                      # p. 175 (I11)
            out_ff = (ff - p["ff_inf"]) * (p["otff_sup"] - p["otff_inf"]) / (p["ff_sup"] - p["ff_inf"]) + p["otff_inf"]
        else:
            out_ff = 0.0
        old_wgh, old_var, old_ff, old_x = self._memory or (dev_wgh, var, out_ff, 0.0)   # I2
        td, kd = p["td"], p["kd"]
        # Filtered derivative component, unsigned (p. 178-180, INTERPRETATION I1)
        x = (td * old_x + gain * td * kd * (var - old_var)) / (kd * dt + td) if td > 0 else 0.0
        mode = "track" if tr_s else ("auto" if man_auto else "manual")      # p. 176: tracking has priority
        outc, outp, outbias = self._outc, self._outp, self._outbias
        if mode != "auto":
            requested = tr_i if mode == "track" else (self.out if out is None else out)
            new_out = _clamp(float(requested), p["out_inf"], p["out_sup"])  # p. 176: out_inf/out_sup
            if requested < p["out_inf"]:
                status |= BIT_LOW                                           # note p. 188 (I10)
            if requested > p["out_sup"]:
                status |= BIT_HIGH
            outd = new_out - self.out                                       # p. 166 (I8)
            outc = outp = new_out                                           # I8
        elif p["ti"] == 0:
            # Absolute algorithm (p. 169, 178)
            term_p = sense * kp * dev_m
            term_d = sense * x
            if self._mode in ("manual", "track") and not p["bump"]:
                outbias = self.out - (term_p + term_d + out_ff)             # p. 176 (INTERPRETATION I9)
            outp = term_p + term_d + out_ff + outbias                       # OUT = TermP + TermD + TermFF + outbias
            outd = outp - self._outp                                        # OUTD = OUTP(new) - OUTP(old) (I7)
            new_out, status = self._auto_limit(self._rate(outp, dt), status)  # OUT = limiter(OUT)
            outc = new_out
        else:
            # Incremental algorithms (p. 169, 179-182)
            term_i = sense * (kp if kp > 0 else alpha) * dt / p["ti"] * dev_m  # p. 179-180 (kp); p. 181-182 (alpha)
            if kp > 0:
                term_p = sense * kp * (dev_wgh - old_wgh)                   # TermP = sense kp Delta(DEV_WGH)
                term_d = sense * (x - old_x)                                # TermD = Delta[sense X] (I1)
            else:
                term_p = term_d = 0.0                                       # integral mode (I13)
            term_ff = out_ff - old_ff                                       # TermFF = Delta[out_ff]
            if not p["aw_type"]:
                outd = term_p + term_i + term_d + term_ff                   # p. 179 / 181
                if p["en_rcpy"]:
                    new_out = rcpy + outd                                   # OUT = RCPY + OUTD, not limited (p. 173, I6)
                else:
                    new_out, status = self._auto_limit(self._rate(self.out + outd, dt), status)  # I5
                outc = new_out
            else:
                reference = rcpy if p["en_rcpy"] else self.out
                term_aw = dt / p["ti"] * (reference - self._outc)           # TermAW (p. 180 / 182)
                outd = term_p + term_i + term_d + term_ff + term_aw
                outc = self._outc + outd                                    # OUTc = OUTc(old) + OUTD(new)
                new_out, status = self._auto_limit(self._rate(outc, dt), status)  # OUT = limiter(OUTc)
            outp = new_out
        results = (new_out, outd, outc, outp, outbias, x, dev_wgh, var, out_ff, dev)
        if not all(math.isfinite(v) for v in results):
            return self._unchanged(BIT_OVERFLOW, "FP_ERROR")                # p. 35 bit 3, I12
        # Commit
        self.out, self.outd, self.ma_o, self.dev, self.out_ff = new_out, outd, mode == "auto", dev, out_ff
        self._outc, self._outp, self._outbias = outc, outp, outbias
        self._memory = (dev_wgh, var, out_ff, x)
        self._mode = mode
        return PidffResult(self.out, self.outd, self.ma_o, self.dev, self.out_ff, status)
