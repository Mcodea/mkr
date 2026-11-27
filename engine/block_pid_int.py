"""Executable model of the Schneider PID_INT block (EcoStruxure Control Expert, standard library, family CLC_INT).

Source (only source used): Schneider Electric, « EcoStruxure Control Expert - Bibliothèque de blocs standard »,
33002520, edition 09/2020 (33002520.20), part III CLC_INT, chap. 29 (p. 129-134) and chap. 30 (p. 135-141).
PDF page numbers equal the printed page numbers (checked). The equations of p. 141 are images and were read on the
rendered page: « εt = SP − PV » ; TI = 0 : « OUT = KP [εt + Dt] / 100 + 5000 » ; with integral action :
« ΔOUT = KP [Δεt + (TS/10.TI).εt + ΔDt] / 100, OUT = OUT + ΔOUT », « Dt = action dérivée ».
The block diagram of p. 137 gives: setpoint limiter [0, 10000] before the error, ε = SP_used − PV, output limiter
OUT_MAX/OUT_MIN in AUTO only, manual switch to OUT_MAN, « suivi sans à-coups de la commande sur passage Auto -> Manu ».

DOCUMENTARY MODEL, NOT QUALIFIED: it reproduces what the manual writes, nothing more. Everything the manual leaves open
is either refused (exception) or resolved by the most literal reading, listed below and tagged « INTERPRÉTATION » in
the code.

Implemented (page):
- registers PARA[0..8] with their INT type, units and ranges (p. 140); 43-word table, shorter table => not executed (p. 133, 139);
- ε = SP − PV, SP limited to [0, 10000] (p. 137, 141, p. 136 « limites haute et basse de la consigne ») ;
- TI = 0 : positional P with +5000 offset (p. 139, 141) ; TI > 0 : incremental PI (p. 141) ;
- direction by the sign of KP (p. 140): with ε = SP − PV, KP > 0 lowers OUT when PV rises (« positif : arrière »),
  KP < 0 raises it (« négatif : avant ») ;
- AUTO output limits OUT_MIN/OUT_MAX (p. 136, 137) ; integral anti-saturation (p. 136) ;
- manual mode OUT_MAN, bumpless Auto -> Manu tracking (p. 137) ;
- cold start: manual mode, output 0, user initialisation programmed AFTER the call (p. 134, 141) ; warm restart keeps
  the state (p. 134) ;
- periodic MAST/FAST task required, cyclic MAST refused (p. 133, 136) ; real sampling period = multiple of the task
  period nearest to TS (p. 140) ; EN/ENO (p. 136).

Refused (UndocumentedBehaviour), because the manual gives no equation or no rule:
- derivative action TD <> 0 (« Dt = action dérivée », no formula, no filter; PV_DEV only selects its input) ;
- AUTO with KP = 0: p. 136 announces an « integrator mode for KP = TD = 0 » but the p. 141 equations give ΔOUT = 0
  (TI > 0) or OUT = 5000 (TI = 0): no documented equation for that mode ;
- manual -> auto transition with NO_BUMP (PARA[8].4) = 0 « par à-coups »: the effect of that bit is not described ;
- operator terminal scaling (PV_SUP, PV_INF, PV_MMI, SP_MMI, DOP_SP, p. 140-141): not modelled, words ignored.

INTERPRÉTATIONS (each one marked in the code):
 I1  « TI < 0 » (p. 141) is read as TI > 0 (TI range is 0..20000, p. 140).
 I2  « TS/10.TI » is read TS/(10·TI): TS in 0.01 s and TI in 0.1 s make it Ts/Ti, dimensionless; the left-to-right
     reading (TS/10)·TI would grow the integral action with TI.
 I3  Integer arithmetic: the manual gives no intermediate width nor rounding. The bracket is evaluated exactly and the
     result divided once and truncated toward zero (IEC 61131-3 INT division):
     TI > 0: ΔOUT = trunc(KP·(10·TI·Δε + TS·ε) / (1000·TI)) ; TI = 0: OUT = trunc(KP·ε / 100) + 5000.
     No residue is carried between samples (the 27 internal words are undocumented). Consequence: the integral action
     stalls while |KP·TS·ε| < 1000·TI (dead band, see the adapter section).
 I4  Anti-saturation: the stored OUT is the limited output, OUT = clamp(OUT + ΔOUT, OUT_MIN, OUT_MAX).
 I5  The TI = 0 output is also limited to [OUT_MIN, OUT_MAX] (the limiter of p. 137 sits after the PID action).
 I6  Manual output = OUT_MAN, NOT limited by OUT_MIN/OUT_MAX (p. 137: the limiter is upstream of the AUTO switch).
 I7  Auto -> Manu tracking: on the first manual sample after AUTO, PARA[1] (OUT_MAN) := last output.
 I8  Δε at a sample uses the ε memorised at the previous sample, whatever the mode then; at the very first sample
     (no previous ε) Δε = 0. Manual -> auto starts from OUT = current output (incremental form, NO_BUMP = 1).
 I9  Sampling: the call executes the algorithm every n task cycles, n = max(1, nearest integer to TS·10/T_task_ms),
     ties rounded up; other calls hold PID_Out. The first call after a cold start is a sample. The equation uses the
     register TS (p. 141 writes TS), not the rounded period.
 I10 Out-of-range registers, OUT_MIN > OUT_MAX, values that are not INT, strings longer than 8/6 characters: the manual
     gives the ranges (p. 139-140) but no reaction; the model refuses them (ParameterError) without changing state.
     KP bounds are strict: -10000 < KP < +10000 (p. 140). PV is not limited (no limiter on the measurement branch, p. 137).
 I11 Cold start (p. 134, 141): the first call forces In_Out_Auto = 0, OUT_MAN = 0, PID_Out = 0, then runs normally.
 I12 EN = 0 or table too short (< 43 words): not executed, PID_Out and state unchanged, ENO = 0 (IEC EN/ENO; p. 133).
 I13 A cyclic MAST task is refused at construction (p. 133/136 « inopérante »); n is never computed without a period.

Line-by-line check of the existing adapter engine/block_adapters.py:pid_int against this model:
 A1 Equation and sign: identical (ΔOUT = KP[Δε + TS/(10 TI) ε]/100, ε = SP − PV, KP > 0 = MKR Kp > 0). No sign error.
 A2 KP range: the adapter accepts |KP| = 10000; the manual writes strict -10000 < KP < +10000 => KP = ±10000 is out of
    range here (one resolution step beyond the bound).
 A3 Integer arithmetic: the adapter (and its oracle test_pidint_conversion.py, written by the same author) integrates in
    floating point. Under I3 the integral term moves by whole counts only: dead band |ε| < 1000·TI/(|KP|·TS) counts.
    For the adapter's own test case (KP = 40, TI = 200, TS = 10) that is |ε| < 500 counts = 5 % of span, far above the
    1-count resolution and of the same order as the 6 % tolerance of that test; with TS = 40 it is 125 counts.
 A4 Bounds: the adapter oracle has no OUT_MIN/OUT_MAX limiter, no [0, 10000] output format, no SP limiter and no
    anti-saturation; it starts at OUT = 0 with ε_prev = 0 (a proportional kick KP·ε/100 on the first cycle, where this
    model, after a cold start, gives Δε = 0 by I8).
 A5 TS: the adapter rounds TS to 0.01 s but does not apply the task-period multiple (warning only); with TS not a
    multiple of the task, the real sampling period differs while the equation keeps the register TS (I9): integral
    rate error = TS_register / TS_real − 1.
 A6 Register quantisation: KP, TI, TS are rounded to the nearest step (error <= half a step); TI register >= 1 enforced.
 A7 NO_BUMP is listed « to determine »: this model refuses NO_BUMP = 0 at a manual -> auto transition.
"""

INT_MIN, INT_MAX = -32768, 32767
PARA_LENGTH = 43                      # « Tableau de 43 entiers » (p. 139)
SCALE = 10000                         # analog format [0, 10000] (p. 133, 136)

# PARA indexes (p. 140)
SP, OUT_MAN, KP, TI, TD, TS, OUT_MAX, OUT_MIN, FLAGS = range(9)
PV_DEV_BIT, NO_BUMP_BIT = 0, 4        # PARA[8].0 and PARA[8].4 (p. 140)

# Documented register ranges (p. 140): (low, high, inclusive)
RANGES = {
    OUT_MAN: (0, 10000, True),        # « entre 0 et 10 000 »
    KP: (-10000, 10000, False),       # « -10 000 < KP < +10 000 », x100, signed (I10: strict)
    TI: (0, 20000, True),             # 0.1 s
    TD: (0, 10000, True),             # 0.1 s
    TS: (1, 32000, True),             # 0.01 s
    OUT_MAX: (0, 10000, True),
    OUT_MIN: (0, 10000, True),
}
UNITS = {SP: "counts [0..10000]", OUT_MAN: "counts [0..10000]", KP: "gain x100 (counts/count)", TI: "0.1 s",
         TD: "0.1 s", TS: "0.01 s", OUT_MAX: "counts [0..10000]", OUT_MIN: "counts [0..10000]", FLAGS: "bit field"}


class TaskConfigurationError(ValueError):
    """The block is not in a periodic MAST/FAST task (p. 133, 136)."""


class ParameterError(ValueError):
    """A register or input outside its documented type or range (INTERPRÉTATION I10)."""


class UndocumentedBehaviour(NotImplementedError):
    """The manual names the function but gives no equation or rule for it: refused, never guessed."""


def _is_int(value):
    return type(value) is int and INT_MIN <= value <= INT_MAX


def _trunc_div(numerator, denominator):
    """Integer division truncated toward zero (IEC 61131-3 INT DIV). INTERPRÉTATION I3."""
    quotient = abs(numerator) // abs(denominator)
    return quotient if (numerator >= 0) == (denominator > 0) else -quotient


def _clamp(value, low, high):
    return low if value < low else high if value > high else value


class PidIntModel:
    """One PID_INT instance with its parameter table, called once per task cycle by `call`."""

    def __init__(self, task_period_ms, task="MAST", periodic=True, para_length=PARA_LENGTH):
        # p. 133, 136: MAST or FAST, periodic only; « inopérante » in a cyclic MAST (INTERPRÉTATION I13).
        if task not in ("MAST", "FAST"):
            raise TaskConfigurationError("task_must_be_MAST_or_FAST")
        if not periodic:
            raise TaskConfigurationError("cyclic_task_inoperative")
        if type(task_period_ms) is not int or task_period_ms < 1:
            raise TaskConfigurationError("task_period")
        self.task_period_ms = task_period_ms
        self.para = [0] * para_length          # In_Out_Para ARRAY OF INT (p. 139); user writes PARA[0..8]
        self.auto = False                      # In_Out_Auto EBOOL: 0 manual, 1 automatic (p. 139)
        self.pid_out = 0                       # PID_Out INT [0, 10000] (p. 139)
        self.eno = False
        self.cold_start()                      # a new call is initialised like a cold start (p. 134)

    # --- start-up modes (p. 134) -------------------------------------------------------------------------------
    def cold_start(self):
        """Next call: manual mode, output 0 (p. 134, 141). INTERPRÉTATION I11."""
        self._cold_pending = True

    def warm_restart(self):
        """Warm restart: the function resumes in its state before the power cut (p. 134)."""

    # --- helpers ---------------------------------------------------------------------------------------------
    def sample_multiple(self):
        """Task cycles per sample: multiple of the task period nearest to TS (p. 140). INTERPRÉTATION I9."""
        ts_ms = self.para[TS] * 10
        n = (2 * ts_ms + self.task_period_ms) // (2 * self.task_period_ms)   # nearest, ties up
        return max(1, n)

    def _validate(self, pv, input_tag, input_unit):
        if not isinstance(input_tag, str) or len(input_tag) > 8 or not isinstance(input_unit, str) or len(input_unit) > 6:
            raise ParameterError("tag_or_unit")                       # STRING 8 / 6 characters (p. 139)
        if not _is_int(pv):
            raise ParameterError("Input_PV")                         # INT (p. 139); not limited (I10)
        if type(self.auto) is not bool:
            raise ParameterError("In_Out_Auto")
        for index in range(PARA_LENGTH):
            if not _is_int(self.para[index]):
                raise ParameterError(f"PARA[{index}]")
        for index, (low, high, inclusive) in RANGES.items():
            value = self.para[index]
            if not (low <= value <= high if inclusive else low < value < high):
                raise ParameterError(f"PARA[{index}]")
        if self.para[OUT_MIN] > self.para[OUT_MAX]:
            raise ParameterError("OUT_MIN>OUT_MAX")                   # I10
        if self.para[TD] != 0:
            raise UndocumentedBehaviour("derivative_action")          # « Dt = action dérivée », no equation (p. 141)

    # --- one task cycle --------------------------------------------------------------------------------------
    def call(self, pv, en=True, input_tag="", input_unit=""):
        """PID_INT(Input_Tag, Input_Unit, Input_PV, In_Out_Auto, In_Out_Para, PID_Out) (p. 138). Returns PID_Out."""
        if not en or len(self.para) < PARA_LENGTH:                    # p. 133 / IEC EN; INTERPRÉTATION I12
            self.eno = False
            return self.pid_out
        if self._cold_pending:                                        # p. 134, 141; INTERPRÉTATION I11
            self.auto, self.para[OUT_MAN], self.pid_out = False, 0, 0
            self._eps_prev, self._was_auto, self._counter = None, False, 0
            self._cold_pending = False
        self._validate(pv, input_tag, input_unit)
        counter = self._counter % self.sample_multiple()
        if counter != 0:
            self._counter, self.eno = counter + 1, True
            return self.pid_out                                       # held between samples (I9)

        sp_used = _clamp(self.para[SP], 0, SCALE)                     # setpoint limiter [0, 10000] (p. 136, 137)
        eps = sp_used - pv                                            # εt = SP − PV (p. 141, 137)
        if self.auto:
            kp, ti, ts = self.para[KP], self.para[TI], self.para[TS]
            if kp == 0:
                raise UndocumentedBehaviour("integrator_mode_KP_0")   # p. 136 vs p. 141
            if not self._was_auto and not (self.para[FLAGS] >> NO_BUMP_BIT) & 1:
                raise UndocumentedBehaviour("NO_BUMP_0_transition")   # PARA[8].4 = 0 not described (p. 140)
            low, high = self.para[OUT_MIN], self.para[OUT_MAX]        # AUTO limits (p. 136, 137)
            if ti == 0:
                # p. 139, 141: OUT = KP[εt + Dt]/100 + 5000, Dt = 0 (TD = 0). INTERPRÉTATION I3, I5.
                out = _clamp(_trunc_div(kp * eps, 100) + 5000, low, high)
            else:
                # p. 141 (TI > 0: INTERPRÉTATION I1): ΔOUT = KP[Δε + TS/(10·TI)·ε]/100 (I2, I3), OUT = OUT + ΔOUT.
                d_eps = 0 if self._eps_prev is None else eps - self._eps_prev        # I8
                delta = _trunc_div(kp * (10 * ti * d_eps + ts * eps), 1000 * ti)
                out = _clamp(self.pid_out + delta, low, high)        # anti-saturation (p. 136), I4
        else:
            if self._was_auto:
                self.para[OUT_MAN] = self.pid_out                     # bumpless Auto -> Manu (p. 137), I7
            out = self.para[OUT_MAN]                                  # manual, not limited (p. 137), I6
        self._eps_prev, self._was_auto, self.pid_out = eps, self.auto, out
        self._counter, self.eno = 1, True                             # state committed only after every check
        return out
