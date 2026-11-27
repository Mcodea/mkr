"""Behaviour of the PID_INT model (Schneider 33002520 09/2020, p. 133-141) against INDEPENDENT oracles.

No expected value below is produced by block_pid_int: every oracle is a closed form written here from the manual's
equations in physical units (Kp = KP/100, Ti = TI/10 s, Ts = TS/100 s), evaluated with exact fractions, then quantised
SEPARATELY to the INT registers (truncation toward zero, interpretation I3 of the model). Tolerances: 0 when the
oracle is exact in counts; otherwise at most 1 count (the PID_Out resolution) per executed sample, because each sample
truncates once.
"""
import math
import unittest
from fractions import Fraction

from block_pid_int import (ParameterError, PidIntModel, TaskConfigurationError, UndocumentedBehaviour)

NO_BUMP = 1 << 4                                    # PARA[8].4 (p. 140)


def trunc(x):
    """Toward-zero quantisation of an exact rational to an INT count (computed apart from the model)."""
    return math.trunc(Fraction(x))


def configured(kp, ti, ts=10, sp=5000, out_man=3000, out_min=0, out_max=10000, flags=NO_BUMP, task_ms=100, pv=None):
    """Model after its cold-start call, initialised AFTER the call as p. 141 requires, then one manual sample."""
    model = PidIntModel(task_ms)
    model.para[0:9] = [sp, out_man, kp, ti, 0, ts, out_max, out_min, flags]   # all parameters filled (p. 133)
    model.call(0)                                   # cold start call: manual, OUT_MAN := 0, output 0 (p. 134, 141)
    model.para[1] = out_man                         # initialisation programmed after the call (p. 141)
    for _ in range(nearest_multiple(ts, task_ms)):  # the last of these calls is the next sample (manual)
        model.call(sp if pv is None else pv)
    return model


def nearest_multiple(ts_hundredths, task_ms):
    """Task cycles per sample, nearest multiple of the task period to TS (p. 140), computed apart."""
    return max(1, math.floor(Fraction(ts_hundredths * 10, task_ms) + Fraction(1, 2)))


class Integral(unittest.TestCase):
    def ideal_increment(self, kp, ti, ts, eps):
        """Continuous ideal PI, rectangular integral over one sample: Kp * Ts/Ti * e (counts)."""
        return Fraction(kp, 100) * Fraction(ts, 100) / Fraction(ti, 10) * eps

    def test_constant_error_exact_ramp(self):
        # KP = 2.00, Ti = 5.0 s, Ts = 0.1 s, e = 1000 counts: 2 * 0.1/5 * 1000 = 40 counts per sample, exact.
        model = configured(200, 50, pv=4000)
        self.assertEqual(model.pid_out, 3000)
        model.auto = True
        step = self.ideal_increment(200, 50, 10, 1000)
        self.assertEqual(step, 40)
        for k in range(1, 31):
            self.assertEqual(model.call(4000), 3000 + 40 * k)

    def test_constant_error_quantised_ramp(self):
        # Kp = 1.23, Ti = 7 s, Ts = 0.1 s, e = +-333: 5.8513 counts/sample ideally, 5 after truncation toward zero.
        for eps in (333, -333):
            with self.subTest(eps=eps):
                model = configured(123, 70, sp=5000, pv=5000 - eps)
                model.auto = True
                ideal = self.ideal_increment(123, 70, 10, eps)
                quantised = trunc(ideal)
                self.assertEqual(quantised, 5 if eps > 0 else -5)
                for k in range(1, 41):
                    out = model.call(5000 - eps)
                    self.assertEqual(out, 3000 + quantised * k)
                    self.assertLessEqual(abs(out - (3000 + ideal * k)), k)     # <= 1 count per sample

    def test_setpoint_step_closed_form(self):
        # Velocity PI, e(-1) = 0, e(k) = E for k >= 0: u_k = u0 + Kp*E + Kp*Ts/Ti*E*(k+1).
        model = configured(200, 50, sp=4000, pv=4000)
        model.auto = True
        self.assertEqual(model.call(4000), 3000)       # e = 0: no move
        model.para[0] = 5000
        E = 1000
        for k in range(10):
            expected = 3000 + trunc(Fraction(200, 100) * E + self.ideal_increment(200, 50, 10, E)) \
                + trunc(self.ideal_increment(200, 50, 10, E)) * k
            self.assertEqual(model.call(4000), expected)
        self.assertEqual(expected, 3000 + 2040 + 40 * 9)

    def test_dead_band_of_the_integer_integral(self):
        # Adapter test case registers: KP = 40, TI = 200, TS = 10. Static plant PV = OUT.
        # Fixed point of the truncated integral: it stalls iff |KP*TS*e| < 1000*TI, i.e. |e| < 500 counts.
        model = configured(40, 200, sp=5000, out_man=2000, pv=2000)
        model.auto = True
        outputs = []
        pv = 2000
        for _ in range(3000):
            out = model.call(pv)
            outputs.append(out)
            pv = out
        final_error = 5000 - outputs[-1]
        self.assertEqual(len(set(outputs[-100:])), 1)                        # stalled
        self.assertLess(abs(final_error) * 40 * 10, 1000 * 200)             # inside the analytic dead band
        self.assertGreater(abs(final_error), 1)                              # steady error >> 1 count resolution


class Saturation(unittest.TestCase):
    def test_output_limited_in_auto_and_no_windup(self):
        # e = 100: +4 counts/sample (2 * 0.1/5 * 100). OUT_MAX = 3020 reached after 5 samples.
        model = configured(200, 50, sp=5000, pv=4900, out_max=3020)
        model.auto = True
        outs = [model.call(4900) for _ in range(55)]
        self.assertEqual(outs[:6], [3004, 3008, 3012, 3016, 3020, 3020])
        self.assertTrue(all(o == 3020 for o in outs[4:]))
        # Error reverses to -100: delta e = -200. Without windup: OUT_MAX + Kp*de + Kp*Ts/Ti*e.
        expected = 3020 + trunc(Fraction(2) * -200 + Fraction(2) * Fraction(1, 50) * -100)
        self.assertEqual(expected, 3020 - 404)
        self.assertEqual(model.call(5100), expected)
        # An unbounded accumulator would give 3000 + 4*55 - 404 = 2816: the model must not.
        self.assertNotEqual(expected, 2816)
        self.assertEqual(model.call(5100), expected - 4)

    def test_lower_limit(self):
        model = configured(200, 50, sp=5000, pv=6000, out_min=2900)
        model.auto = True
        outs = [model.call(6000) for _ in range(5)]
        self.assertEqual(outs, [2960, 2920, 2900, 2900, 2900])

    def test_manual_output_is_not_limited(self):
        model = configured(200, 50, out_man=9000, out_max=5000, out_min=4000)
        self.assertEqual(model.call(5000), 9000)
        model.para[1] = 100
        self.assertEqual(model.call(5000), 100)


class Proportional(unittest.TestCase):
    def test_ti_zero_offset_5000(self):
        # p. 139/141: TI = 0 -> OUT = KP*e/100 + 5000; e = 0 gives exactly 5000.
        model = configured(150, 0, sp=5000, pv=5000)
        model.auto = True
        self.assertEqual(model.call(5000), 5000)
        self.assertEqual(model.call(4000), 5000 + trunc(Fraction(150, 100) * 1000))       # 6500
        self.assertEqual(model.call(5333), 5000 + trunc(Fraction(150, 100) * -333))       # -499.5 -> -499
        self.assertEqual(model.call(5333), 4501)                                           # positional: no memory

    def test_ti_zero_output_limited(self):
        model = configured(150, 0, out_max=6000, out_min=4800)
        model.auto = True
        self.assertEqual(model.call(4000), 6000)
        self.assertEqual(model.call(6000), 4800)

    def test_direction_by_sign_of_kp(self):
        # p. 140: KP > 0 « arrière » (PV up -> OUT down), KP < 0 « avant » (PV up -> OUT up), with e = SP - PV.
        for kp, sign in ((100, -1), (-100, +1)):
            with self.subTest(kp=kp):
                model = configured(kp, 0)
                model.auto = True
                low, high = model.call(4000), model.call(6000)
                self.assertEqual((high - low) * sign, abs(kp) * 2000 // 100)

    def test_setpoint_limiter(self):
        # p. 136/137: SP limited to [0, 10000] before the error.
        model = configured(100, 0, sp=12000, out_max=10000)
        model.auto = True
        self.assertEqual(model.call(9000), 5000 + (10000 - 9000))
        model.para[0] = -500
        self.assertEqual(model.call(1000), 5000 + (0 - 1000))


class Modes(unittest.TestCase):
    def test_auto_to_manual_bumpless_tracking(self):
        model = configured(200, 50, pv=4000)
        model.auto = True
        for _ in range(10):
            last = model.call(4000)
        self.assertEqual(last, 3400)
        model.auto = False
        self.assertEqual(model.call(4000), 3400)        # p. 137: no jump on Auto -> Manu
        self.assertEqual(model.para[1], 3400)
        self.assertEqual(model.call(1234), 3400)        # manual: PV ignored

    def test_manual_to_auto_starts_from_manual_output(self):
        model = configured(200, 50, sp=4000, out_man=4321, pv=4000)
        model.auto = True
        self.assertEqual(model.call(4000), 4321)        # e = 0 and de = 0: no bump

    def test_no_bump_zero_is_refused_at_transition(self):
        model = configured(200, 50, flags=0)
        model.auto = True
        with self.assertRaises(UndocumentedBehaviour):
            model.call(5000)

    def test_cold_start_forces_manual_zero(self):
        model = PidIntModel(100)
        model.para[0:9] = [5000, 7000, 200, 50, 0, 10, 10000, 0, NO_BUMP]
        model.auto = True
        self.assertEqual(model.call(4000), 0)
        self.assertFalse(model.auto)
        self.assertEqual(model.para[1], 0)
        model.auto, model.para[1] = True, 3000           # initialisation programmed after the call (p. 141)
        self.assertEqual(model.call(4000), 0 + 40)        # auto from the 0 output: +40 (de = 0)
        model.cold_start()
        model.auto = True
        self.assertEqual(model.call(4000), 0)
        self.assertFalse(model.auto)

    def test_warm_restart_keeps_state(self):
        model = configured(200, 50, pv=4000)
        model.auto = True
        model.call(4000)
        model.call(4000)
        model.warm_restart()
        self.assertTrue(model.auto)
        self.assertEqual(model.call(4000), 3120)


class Sampling(unittest.TestCase):
    def test_ts_rounded_to_task_multiple(self):
        for ts, task, n in ((50, 100, 5), (44, 100, 4), (46, 100, 5), (3, 100, 1), (10, 7, 14)):
            with self.subTest(ts=ts, task=task):
                self.assertEqual(nearest_multiple(ts, task), n)
                model = configured(200, 50, ts=ts, task_ms=task, pv=4000)
                model.auto = True
                # integral step uses the register TS: 2 * (ts/100)/5 * 1000 = 4*ts counts
                step = trunc(Fraction(200 * ts * 1000, 1000 * 50))
                outs = [model.call(4000) for _ in range(3 * n)]
                # the configuring manual call was a sample: next samples at calls n, 2n, 3n (1-based)
                expected = [3000 + step * ((i + 1) // n) for i in range(3 * n)]
                self.assertEqual(outs, expected)

    def test_tie_rounds_up(self):                      # INTERPRÉTATION I9
        model = configured(200, 50, ts=45, task_ms=100)
        self.assertEqual(model.sample_multiple(), 5)


class Refusals(unittest.TestCase):
    def test_cyclic_or_unknown_task_refused(self):
        with self.assertRaises(TaskConfigurationError):
            PidIntModel(100, periodic=False)
        with self.assertRaises(TaskConfigurationError):
            PidIntModel(100, task="AUX0")
        with self.assertRaises(TaskConfigurationError):
            PidIntModel(0)

    def test_out_of_range_and_undocumented(self):
        cases = [
            (4, 1, UndocumentedBehaviour), (2, 10000, ParameterError), (2, -10000, ParameterError),
            (3, 20001, ParameterError), (3, -1, ParameterError), (5, 0, ParameterError), (5, 32001, ParameterError),
            (6, 10001, ParameterError), (7, 6000, ParameterError), (1, -1, ParameterError), (4, 10001, ParameterError),
        ]
        for index, value, error in cases:
            with self.subTest(index=index, value=value):
                model = configured(200, 50, pv=4000, out_max=5000)
                model.auto = True
                model.call(4000)
                before = (model.pid_out, list(model.para), model.auto)
                good = model.para[index]
                model.para[index] = value
                with self.assertRaises(error):
                    model.call(4000)
                self.assertEqual((model.pid_out, model.auto), before[0:3:2])
                model.para[index] = good
                self.assertEqual(model.call(4000), before[0] + 40)   # state untouched by the refusal

    def test_kp_zero_in_auto_refused_but_9999_accepted(self):
        model = configured(0, 50)
        self.assertEqual(model.call(5000), 3000)             # manual: KP unused
        model.auto = True
        with self.assertRaises(UndocumentedBehaviour):
            model.call(5000)
        model.para[2] = 9999
        self.assertEqual(model.call(5000), 3000)

    def test_types_and_strings(self):
        model = configured(200, 50)
        for kwargs in (dict(pv=4000.0), dict(pv=40000), dict(pv=4000, input_tag="TAG123456"), dict(pv=4000, input_unit="degC__x")):
            with self.subTest(kwargs=kwargs), self.assertRaises(ParameterError):
                model.call(**kwargs)
        model.call(4000, input_tag="TIC_101", input_unit="degC")
        model.auto = 1
        with self.assertRaises(ParameterError):
            model.call(4000)

    def test_short_table_and_en_not_executed(self):
        model = PidIntModel(100, para_length=42)
        self.assertEqual(model.call(4000), 0)
        self.assertFalse(model.eno)
        model = configured(200, 50, pv=4000)
        model.auto = True
        self.assertEqual(model.call(4000, en=False), 3000)
        self.assertFalse(model.eno)
        self.assertEqual(model.call(4000), 3040)
        self.assertTrue(model.eno)


if __name__ == "__main__":
    unittest.main()
