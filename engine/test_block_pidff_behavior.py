"""Behaviour tests of engine/block_pidff.py (PIDFF, manual 33002535 ed. 16, 09/2020, chapter 17).

Every expected value is computed here from a closed-form solution of the manual's equations for a simple input
(constant deviation, ramp, step of PV, saturation, ...) or taken from the manual's own example (p. 183-184); none is
produced by the model under test. Tests of an INTERPRETATION of block_pidff.py say so in their name or docstring.

Tolerances: the closed forms are evaluated in double precision with a few dozen operations on magnitudes <= 1e3, so
an absolute tolerance of 1e-9 covers rounding with a large margin. The one comparison with a continuous-time solution
carries its own bound on the first-order discretisation error, derived in the test.
"""

import math
import unittest

from block_pidff import (BIT_CLAMPED, BIT_HIGH, BIT_INVALID_INPUT, BIT_LOW, BIT_NULL_SCALE, BIT_OVERFLOW,
                         E_ERR_NULL_INPUT_SCALE, PidffModel)
from pidff_reference import PidffReference

TOL = 1e-9


def para(**changes):
    """Neutral parameter set: wide ranges (alpha = 1), no dead band, no FF, no rate limit, mixed, aw_type 0."""
    base = dict(pv_inf=-1000.0, pv_sup=1000.0, out_inf=-1000.0, out_sup=1000.0, rev_dir=False, mix_par=False,
                aw_type=False, en_rcpy=False, kp=1.0, ti=0.0, td=0.0, kd=1.0, pv_dev=False, bump=False, dband=0.0,
                gain_kp=0.0, ovs_att=0.0, outbias=0.0, out_min=-1000.0, out_max=1000.0, outrate=0.0,
                ff_inf=0.0, ff_sup=0.0, otff_inf=0.0, otff_sup=0.0)
    base.update(changes)
    return base


class AbsoluteAlgorithm(unittest.TestCase):
    """ti = 0 (p. 169, 178): OUT = TermP + TermD + TermFF + outbias."""

    def test_proportional_sign_and_outbias(self):
        for rev_dir, sense in ((False, -1.0), (True, 1.0)):            # p. 178
            block = PidffModel(para(kp=2.0, rev_dir=rev_dir, outbias=5.0))
            r = block.step(pv=12.0, sp=10.0, dt=0.1)
            self.assertAlmostEqual(r.out, sense * 2.0 * 2.0 + 5.0, delta=TOL)
            self.assertAlmostEqual(r.dev, 2.0, delta=TOL)
            self.assertTrue(r.ma_o)
            self.assertEqual(r.status, 0)

    def test_derivative_filter_geometric_decay_on_pv_step(self):
        """X(k) = [td X(k-1) + K td kd dVAR] / (kd dt + td): after a PV step h, X(1) = K kd h td/(kd dt + td) and
        X(k) = X(1) r^(k-1), r = td/(kd dt + td). TermD = sense X. Also checks INTERPRETATION I1: with sense = -1
        the component keeps one sign (no alternation)."""
        kp, td, kd, dt, h = 1.5, 2.0, 4.0, 0.1, 3.0
        block = PidffModel(para(kp=kp, td=td, kd=kd))
        block.step(pv=0.0, sp=0.0, dt=dt)
        r_ = td / (kd * dt + td)
        x1 = kp * kd * h * td / (kd * dt + td)
        previous = None
        for k in range(1, 30):
            r = block.step(pv=h, sp=0.0, dt=dt)
            term_p = -kp * h
            expected = term_p - x1 * r_ ** (k - 1)
            self.assertAlmostEqual(r.out, expected, delta=TOL)
            if previous is not None:
                self.assertLess(r.out - term_p, 0.0)
                self.assertGreater(r.out - term_p, previous)       # monotone return towards TermP
            previous = r.out - term_p

    def test_derivative_against_continuous_transfer_function(self):
        """p. 169: D(p) = K td p / (1 + (td/kd) p) x VAR; a step h of VAR gives K kd h exp(-kd t/td).
        Discretisation error of the implicit recurrence, first order: |X_k - X(t_k)| <= K kd h (kd dt/td) (1 + ...)
        bounded here by 2 K kd h (kd dt / td)."""
        kp, td, kd, dt, h = 1.0, 5.0, 5.0, 0.001, 1.0
        block = PidffModel(para(kp=kp, td=td, kd=kd, pv_dev=True, rev_dir=True))
        block.step(pv=0.0, sp=0.0, dt=dt)
        bound = 2 * kp * kd * h * kd * dt / td
        for k in range(1, 3000):
            r = block.step(pv=h, sp=0.0, dt=dt)
            t = k * dt
            self.assertAlmostEqual(r.out - kp * h, kp * kd * h * math.exp(-kd * t / td), delta=bound)

    def test_parallel_structure_derivative_gain_alpha(self):
        """mix_par = 1, ti = 0: TermP = sense kp dev, TermD uses K = alpha = (out_sup-out_inf)/(pv_sup-pv_inf)."""
        kp, td, kd, dt, h = 0.5, 1.0, 2.0, 0.1, 1.0
        block = PidffModel(para(kp=kp, td=td, kd=kd, mix_par=True, rev_dir=True, pv_inf=0.0, pv_sup=50.0,
                                out_inf=0.0, out_sup=100.0, out_min=0.0, out_max=100.0))
        block.step(pv=10.0, sp=10.0, dt=dt)
        r = block.step(pv=10.0 + h, sp=10.0, dt=dt)
        alpha = 2.0
        self.assertAlmostEqual(r.out, kp * h + alpha * kd * h * td / (kd * dt + td), delta=TOL)

    def test_saturation_and_unlimited_outd(self):
        """p. 175: OUT capped to out_max, OUTD (I7: OUTP(new) - OUTP(old)) not capped; bit 6."""
        block = PidffModel(para(kp=10.0, rev_dir=True, out_min=0.0, out_max=100.0))
        r1 = block.step(pv=5.0, sp=0.0, dt=1.0)
        self.assertAlmostEqual(r1.out, 50.0, delta=TOL)
        r2 = block.step(pv=20.0, sp=0.0, dt=1.0)
        self.assertAlmostEqual(r2.out, 100.0, delta=TOL)
        self.assertAlmostEqual(r2.outd, 150.0, delta=TOL)
        self.assertTrue(r2.status & BIT_HIGH)
        r3 = block.step(pv=-1.0, sp=0.0, dt=1.0)
        self.assertAlmostEqual(r3.out, 0.0, delta=TOL)
        self.assertTrue(r3.status & BIT_LOW)

    def test_outrate_ramp_interpretation_i4(self):
        """outrate: |OUT(new) - OUT(old)| <= outrate dt; target 50 reached by a ramp of 4 per cycle (closed form
        min(50, 4k))."""
        block = PidffModel(para(kp=10.0, rev_dir=True, outrate=8.0))
        for k in range(1, 20):
            r = block.step(pv=5.0, sp=0.0, dt=0.5)
            self.assertAlmostEqual(r.out, min(50.0, 4.0 * k), delta=TOL)
        unlimited = PidffModel(para(kp=10.0, rev_dir=True, outrate=0.0))
        self.assertAlmostEqual(unlimited.step(pv=5.0, sp=0.0, dt=0.5).out, 50.0, delta=TOL)

    def test_dead_band_absolute(self):
        """p. 174: |dev| < dband -> gain_kp dev; outside -> dev (I3, strict inequality)."""
        block = PidffModel(para(kp=2.0, rev_dir=True, dband=1.0, gain_kp=0.25))
        self.assertAlmostEqual(block.step(pv=0.5, sp=0.0, dt=1.0).out, 2.0 * 0.25 * 0.5, delta=TOL)
        self.assertAlmostEqual(block.step(pv=-0.5, sp=0.0, dt=1.0).out, -2.0 * 0.25 * 0.5, delta=TOL)
        self.assertAlmostEqual(block.step(pv=1.0, sp=0.0, dt=1.0).out, 2.0, delta=TOL)
        self.assertAlmostEqual(block.step(pv=3.0, sp=0.0, dt=1.0).out, 6.0, delta=TOL)
        zero = PidffModel(para(kp=2.0, rev_dir=True, dband=1.0, gain_kp=0.0))
        self.assertAlmostEqual(zero.step(pv=0.9, sp=0.0, dt=1.0).out, 0.0, delta=TOL)


class FeedForwardExample(unittest.TestCase):
    """Manual example p. 183-184 (preset table): ff_inf 5, ff_sup 25, otff_inf -10, otff_sup 10.
    out_ff = (FF - ff_inf)(otff_sup - otff_inf)/(ff_sup - ff_inf) + otff_inf (p. 175): 15 -> 0 ("no effect"),
    25 -> 10, 5 -> -10. (The graph of p. 184 shows +-20 % and "10 % per 5 °C", which the preset table does not
    give: the example is not self-consistent; only the preset table is used here.)"""

    def ff_block(self, **changes):
        values = dict(ff_inf=5.0, ff_sup=25.0, otff_inf=-10.0, otff_sup=10.0, out_min=0.0, out_max=100.0,
                      outbias=50.0, kp=0.0)
        values.update(changes)
        return PidffModel(para(**values))

    def test_out_ff_values_absolute(self):
        for ff, expected in ((15.0, 0.0), (25.0, 10.0), (5.0, -10.0), (20.0, 5.0)):
            r = self.ff_block().step(pv=0.0, sp=0.0, dt=1.0, ff=ff)
            self.assertAlmostEqual(r.out_ff, expected, delta=TOL)
            self.assertAlmostEqual(r.out, 50.0 + expected, delta=TOL)   # no sense factor on FF (I11)

    def test_incremental_ff_is_a_difference(self):
        block = self.ff_block(ti=10.0, kp=1.0)
        block.step(pv=0.0, sp=0.0, dt=1.0, ff=15.0)
        r = block.step(pv=0.0, sp=0.0, dt=1.0, ff=25.0)
        self.assertAlmostEqual(r.outd, 10.0, delta=TOL)
        self.assertAlmostEqual(r.out, 10.0, delta=TOL)

    def test_equal_ff_bounds_ignore_feed_forward(self):
        block = PidffModel(para(ff_inf=5.0, ff_sup=5.0, otff_inf=-10.0, otff_sup=10.0, kp=0.0))
        r = block.step(pv=0.0, sp=0.0, dt=1.0, ff=100.0)
        self.assertEqual((r.out, r.out_ff), (0.0, 0.0))


class IncrementalAlgorithm(unittest.TestCase):
    """ti > 0 (p. 179): OUTD = TermP + TermI + TermD + TermFF, OUT = OUT(old) + OUTD."""

    def test_deviation_step_closed_form(self):
        """Constant deviation e from cycle 1: OUT(n) = sense kp e (1 + n dt/ti)."""
        kp, ti, dt, e = 2.0, 4.0, 0.5, 3.0
        for rev_dir, sense in ((False, -1.0), (True, 1.0)):
            block = PidffModel(para(kp=kp, ti=ti, rev_dir=rev_dir))
            block.step(pv=0.0, sp=0.0, dt=dt)
            for n in range(1, 40):
                r = block.step(pv=e, sp=0.0, dt=dt)
                self.assertAlmostEqual(r.out, sense * kp * e * (1 + n * dt / ti), delta=TOL)

    def test_ramp_closed_form(self):
        """dev(k) = rho k dt: OUT(n) = sense kp [rho n dt + rho dt^2/ti n(n+1)/2]."""
        kp, ti, dt, rho = 0.8, 3.0, 0.2, 1.5
        block = PidffModel(para(kp=kp, ti=ti, rev_dir=True))
        block.step(pv=0.0, sp=0.0, dt=dt)
        for n in range(1, 50):
            r = block.step(pv=rho * n * dt, sp=0.0, dt=dt)
            self.assertAlmostEqual(r.out, kp * (rho * n * dt + rho * dt * dt / ti * n * (n + 1) / 2), delta=TOL)

    def test_ovs_att_weights_the_setpoint_kick(self):
        """TermP = sense kp Delta(PV - (1 - ovs_att) SP): an SP step s gives a kick sense kp (-(1-ovs_att) s)."""
        kp, ti, dt, s = 2.0, 10.0, 1.0, 4.0
        for ovs in (0.0, 0.5, 1.0):
            block = PidffModel(para(kp=kp, ti=ti, ovs_att=ovs, rev_dir=True))
            block.step(pv=0.0, sp=0.0, dt=dt)
            r = block.step(pv=0.0, sp=s, dt=dt)
            self.assertAlmostEqual(r.out, kp * (-(1 - ovs) * s) + kp * dt / ti * (-s), delta=TOL)

    def test_dead_band_incremental(self):
        """Inside the band (|dev| < dband): TermP and TermI use gain_kp x dev (p. 174, I3)."""
        kp, ti, dt, d, g = 1.0, 2.0, 1.0, 0.4, 0.5
        block = PidffModel(para(kp=kp, ti=ti, dband=1.0, gain_kp=g, rev_dir=True))
        block.step(pv=0.0, sp=0.0, dt=dt)
        for n in range(1, 10):
            r = block.step(pv=d, sp=0.0, dt=dt)
            self.assertAlmostEqual(r.out, kp * g * d * (1 + n * dt / ti), delta=TOL)
        frozen = PidffModel(para(kp=kp, ti=ti, dband=1.0, gain_kp=0.0, rev_dir=True))
        for _ in range(5):
            self.assertEqual(frozen.step(pv=d, sp=0.0, dt=dt).out, 0.0)

    def test_integral_mode_uses_alpha(self):
        """kp = 0 (p. 181): OUTD = sense alpha dt/ti dev; alpha = (100-0)/(50-0) = 2. Parallel or mixed alike."""
        for mix_par in (False, True):
            block = PidffModel(para(kp=0.0, ti=5.0, td=3.0, kd=2.0, mix_par=mix_par, rev_dir=True,
                                    pv_inf=0.0, pv_sup=50.0, out_inf=0.0, out_sup=100.0, out_min=0.0, out_max=100.0))
            block.step(pv=10.0, sp=10.0, dt=1.0)
            for n in range(1, 6):
                r = block.step(pv=12.0, sp=10.0, dt=1.0)
                self.assertAlmostEqual(r.out, 2.0 * 1.0 / 5.0 * 2.0 * n, delta=TOL)

    def test_negative_kp_becomes_zero_bit4_integral_mode(self):
        block = PidffModel(para(kp=-3.0, ti=5.0, rev_dir=True, pv_inf=0.0, pv_sup=50.0,
                                out_inf=0.0, out_sup=100.0, out_min=0.0, out_max=100.0))
        block.step(pv=10.0, sp=10.0, dt=1.0)
        r = block.step(pv=12.0, sp=10.0, dt=1.0)
        self.assertTrue(r.status & BIT_CLAMPED)
        self.assertAlmostEqual(r.out, 2.0 * 0.2 * 2.0, delta=TOL)

    def test_agrees_with_project_reference_in_its_domain(self):
        """Cross-check (not an oracle): engine/pidff_reference.py, mixed, ti > 0, aw_type 0, no limits."""
        for rev_dir, pv_dev, ovs in ((False, 0, 0.0), (True, 1, 0.3)):
            mine = PidffModel(para(kp=1.2, ti=6.0, td=1.5, kd=5.0, rev_dir=rev_dir, pv_dev=bool(pv_dev), ovs_att=ovs))
            ref = PidffReference(1.2, 6.0, 1.5, 5.0, rev_dir, 0.1, pv_dev=pv_dev, ovs_att=ovs)
            for k in range(200):
                pv, sp = math.sin(0.05 * k) * 3, (2.0 if k > 20 else 0.0)
                self.assertAlmostEqual(mine.step(pv=pv, sp=sp, dt=0.1).out, ref.step(pv, sp), delta=1e-9)


class AntiWindup(unittest.TestCase):
    def test_aw0_restarts_from_limit_and_outd_unlimited(self):
        """aw_type 0 (p. 173, 179, I5): OUT(old) is the limited value; OUTD is the unlimited sum. A reduction of the
        deviation with no change of sign makes the output leave the limit (p. 173)."""
        kp, ti, dt = 1.0, 1.0, 1.0
        block = PidffModel(para(kp=kp, ti=ti, rev_dir=True, out_min=0.0, out_max=10.0))
        block.step(pv=0.0, sp=0.0, dt=dt)
        for _ in range(10):
            r = block.step(pv=5.0, sp=0.0, dt=dt)
        self.assertEqual(r.out, 10.0)
        self.assertAlmostEqual(r.outd, kp * dt / ti * 5.0, delta=TOL)   # OUTD = TermI, not capped to 0
        self.assertTrue(r.status & BIT_HIGH)
        r = block.step(pv=1.0, sp=0.0, dt=dt)                         # dev 5 -> 1, still positive
        expected = 10.0 + kp * (1.0 - 5.0) + kp * dt / ti * 1.0
        self.assertAlmostEqual(r.out, expected, delta=TOL)
        self.assertFalse(r.status & BIT_HIGH)

    def test_aw1_back_calculation_closed_form(self):
        """aw_type 1 (p. 180): with OUT held at M, y = OUTc - M obeys y(k) = (1 - dt/ti) y(k-1) + a, a = sense kp dt/ti e;
        y(k) = y* + (y0 - y*) (1 - dt/ti)^k with y* = a ti/dt. The output then stays at M while the deviation keeps
        its sign (p. 173)."""
        kp, ti, dt, e, M = 1.0, 4.0, 1.0, 2.0, 10.0
        block = PidffModel(para(kp=kp, ti=ti, aw_type=True, rev_dir=True, out_min=0.0, out_max=M), out_init=M)
        block.step(pv=e, sp=0.0, dt=dt)                               # first cycle: Delta = 0, OUTc = M + a
        a, c = kp * dt / ti * e, dt / ti
        y0 = a
        y_star = a * ti / dt
        for k in range(1, 40):
            r = block.step(pv=e, sp=0.0, dt=dt)
            self.assertEqual(r.out, M)
            self.assertAlmostEqual(block._outc - M, y_star + (y0 - y_star) * (1 - c) ** k, delta=TOL)
        # the same deviation drop as above: aw_type 1 keeps the output at its threshold
        r = block.step(pv=1.0, sp=0.0, dt=dt)
        self.assertEqual(r.out, M)

    def test_aw1_with_rcpy_pulls_towards_rcpy(self):
        """en_rcpy = 1, aw_type 1: TermAW = dt/ti (RCPY - OUTc(old)); with zero deviation OUTc converges
        geometrically to RCPY: OUTc(k) - RCPY = (OUTc(0) - RCPY)(1 - dt/ti)^k."""
        ti, dt, rcpy = 5.0, 1.0, 30.0
        block = PidffModel(para(kp=1.0, ti=ti, aw_type=True, en_rcpy=True, out_min=0.0, out_max=100.0))
        for k in range(1, 20):
            r = block.step(pv=0.0, sp=0.0, dt=dt, rcpy=rcpy)
            self.assertAlmostEqual(r.out, rcpy - rcpy * (1 - dt / ti) ** k, delta=TOL)

    def test_aw0_rcpy_output_not_limited(self):
        """en_rcpy = 1, aw_type 0: OUT = RCPY + OUTD, not limited (p. 173, I6)."""
        block = PidffModel(para(kp=1.0, ti=1.0, en_rcpy=True, rev_dir=True, out_min=0.0, out_max=10.0))
        block.step(pv=0.0, sp=0.0, dt=1.0, rcpy=9.0)
        r = block.step(pv=4.0, sp=0.0, dt=1.0, rcpy=9.0)
        self.assertAlmostEqual(r.out, 9.0 + 4.0 + 4.0, delta=TOL)
        self.assertFalse(r.status & BIT_HIGH)


class Modes(unittest.TestCase):
    def test_manual_limits_and_one_cycle_bits(self):
        """p. 176, 188: manual OUT limited to [out_inf, out_sup]; bit 6 for one cycle when the written value exceeds."""
        block = PidffModel(para(out_inf=0.0, out_sup=100.0, out_min=10.0, out_max=90.0))
        r = block.step(pv=0.0, sp=0.0, dt=1.0, man_auto=False, out=150.0)
        self.assertEqual((r.out, r.ma_o), (100.0, False))
        self.assertTrue(r.status & BIT_HIGH)
        r = block.step(pv=0.0, sp=0.0, dt=1.0, man_auto=False)
        self.assertEqual(r.out, 100.0)
        self.assertFalse(r.status & (BIT_HIGH | BIT_LOW))
        r = block.step(pv=0.0, sp=0.0, dt=1.0, man_auto=False, out=95.0)   # above out_max: allowed in manual
        self.assertEqual(r.out, 95.0)
        self.assertAlmostEqual(r.outd, -5.0, delta=TOL)

    def test_tracking_has_priority(self):
        block = PidffModel(para(out_inf=0.0, out_sup=100.0, out_min=0.0, out_max=100.0))
        for man_auto in (True, False):
            r = block.step(pv=0.0, sp=0.0, dt=1.0, man_auto=man_auto, tr_s=True, tr_i=42.0, out=7.0)
            self.assertEqual((r.out, r.ma_o), (42.0, False))
        r = block.step(pv=0.0, sp=0.0, dt=1.0, tr_s=True, tr_i=-5.0)
        self.assertEqual(r.out, 0.0)
        self.assertTrue(r.status & BIT_LOW)

    def test_bumpless_manual_to_auto_incremental(self):
        """ti > 0: first automatic cycle OUT = OUT_manual + TermI (dev constant, so Delta DEV_WGH = 0, I8)."""
        kp, ti, dt, e = 2.0, 4.0, 1.0, 1.0
        block = PidffModel(para(kp=kp, ti=ti, rev_dir=True))
        block.step(pv=e, sp=0.0, dt=dt, man_auto=False, out=30.0)
        block.step(pv=e, sp=0.0, dt=dt, man_auto=False)
        r = block.step(pv=e, sp=0.0, dt=dt)
        self.assertAlmostEqual(r.out, 30.0 + kp * dt / ti * e, delta=TOL)

    def test_absolute_bumpless_and_bump(self):
        """ti = 0: bump = 0 recalculates outbias (p. 176, I9): OUT restarts at the manual value; bump = 1 jumps to
        sense kp dev + outbias."""
        smooth = PidffModel(para(kp=2.0, rev_dir=True, outbias=1.0))
        smooth.step(pv=3.0, sp=0.0, dt=1.0, man_auto=False, out=20.0)
        self.assertAlmostEqual(smooth.step(pv=3.0, sp=0.0, dt=1.0).out, 20.0, delta=TOL)
        self.assertAlmostEqual(smooth.step(pv=4.0, sp=0.0, dt=1.0).out, 22.0, delta=TOL)
        jump = PidffModel(para(kp=2.0, rev_dir=True, outbias=1.0, bump=True))
        jump.step(pv=3.0, sp=0.0, dt=1.0, man_auto=False, out=20.0)
        self.assertAlmostEqual(jump.step(pv=3.0, sp=0.0, dt=1.0).out, 7.0, delta=TOL)


class StatusAndErrors(unittest.TestCase):
    def test_setpoint_clamped_bit4(self):
        block = PidffModel(para(pv_inf=0.0, pv_sup=100.0, kp=1.0, rev_dir=True))
        r = block.step(pv=50.0, sp=150.0, dt=1.0)
        self.assertEqual(r.dev, -50.0)
        self.assertTrue(r.status & BIT_CLAMPED)
        self.assertFalse(block.step(pv=50.0, sp=60.0, dt=1.0).status & BIT_CLAMPED)

    def test_parameter_substitutions_bit4(self):
        cases = [dict(dband=-1.0), dict(gain_kp=-1.0), dict(outrate=-1.0), dict(td=1.0, kd=0.5), dict(ovs_att=1.5),
                 dict(out_min=-2000.0), dict(out_max=5000.0), dict(outbias=3000.0), dict(otff_sup=3000.0)]
        for changes in cases:
            block = PidffModel(para(**changes))
            self.assertTrue(block.step(pv=0.0, sp=0.0, dt=1.0).status & BIT_CLAMPED, changes)
        b = PidffModel(para(out_inf=0.0, out_sup=100.0, out_min=-5.0, out_max=120.0, outbias=500.0))
        self.assertEqual((b.p["out_min"], b.p["out_max"], b.p["outbias"]), (0.0, 100.0, 100.0))
        self.assertEqual(PidffModel(para(td=1.0, kd=0.2)).p["kd"], 1.0)
        self.assertEqual(PidffModel(para(td=0.0, kd=0.2)).p["kd"], 0.2)      # td = 0: kd not substituted

    def test_kd_substituted_by_one_in_derivative(self):
        kp, td, dt, h = 1.0, 1.0, 0.1, 1.0
        block = PidffModel(para(kp=kp, td=td, kd=0.3, rev_dir=True, pv_dev=True))
        block.step(pv=0.0, sp=0.0, dt=dt)
        r = block.step(pv=h, sp=0.0, dt=dt)
        self.assertAlmostEqual(r.out, kp * h + kp * 1.0 * h * td / (1.0 * dt + td), delta=TOL)

    def test_inverted_out_min_max_interpretation_i10(self):
        """p. 35 note 2: inverted thresholds -> output at out_max (lowest)."""
        block = PidffModel(para(kp=1.0, rev_dir=True, out_min=50.0, out_max=20.0))
        r = block.step(pv=0.0, sp=0.0, dt=1.0)
        self.assertEqual(r.out, 20.0)

    def test_equal_pv_bounds_is_an_error_and_outputs_unchanged(self):
        block = PidffModel(para(pv_inf=10.0, pv_sup=10.0), out_init=33.0)
        r = block.step(pv=5.0, sp=0.0, dt=1.0)
        self.assertTrue(r.status & BIT_NULL_SCALE)
        self.assertEqual(r.error, E_ERR_NULL_INPUT_SCALE)
        self.assertEqual((r.out, r.outd, r.ma_o, r.dev, r.out_ff), (33.0, 0.0, False, 0.0, 0.0))

    def test_non_finite_input_leaves_outputs_unchanged(self):
        block = PidffModel(para(kp=1.0, ti=2.0, rev_dir=True))
        block.step(pv=0.0, sp=0.0, dt=1.0)
        good = block.step(pv=1.0, sp=0.0, dt=1.0)
        for bad in (dict(pv=math.nan), dict(sp=math.inf), dict(ff=-math.inf), dict(rcpy=math.nan), dict(tr_i=math.nan),
                    dict(out=math.inf)):
            args = dict(pv=1.0, sp=0.0, dt=1.0)
            args.update(bad)
            r = block.step(**args)
            self.assertTrue(r.status & BIT_INVALID_INPUT, bad)
            self.assertEqual((r.out, r.outd, r.ma_o, r.dev), (good.out, good.outd, good.ma_o, good.dev))
        after = block.step(pv=1.0, sp=0.0, dt=1.0)                     # memories untouched: plain TermI
        self.assertAlmostEqual(after.out, good.out + 0.5, delta=TOL)

    def test_overflow_leaves_outputs_unchanged(self):
        block = PidffModel(para(kp=1e300, rev_dir=True, pv_inf=-1e300, pv_sup=1e300, out_inf=-1e308, out_sup=1e308,
                                out_min=-1e308, out_max=1e308))
        r = block.step(pv=1e300, sp=-1e300, dt=1.0)
        self.assertTrue(r.status & BIT_OVERFLOW)
        self.assertEqual(r.out, 0.0)


class Refusals(unittest.TestCase):
    def test_parallel_incremental_refused(self):
        with self.assertRaisesRegex(ValueError, "parallel_incremental_contradictory"):
            PidffModel(para(mix_par=True, ti=5.0, kp=1.0))
        PidffModel(para(mix_par=True, ti=0.0, kp=1.0))                  # absolute parallel: consistent
        PidffModel(para(mix_par=True, ti=5.0, kp=0.0))                  # integral mode: consistent

    def test_bump_incremental_refused(self):
        with self.assertRaisesRegex(ValueError, "bump_incremental_undocumented"):
            PidffModel(para(bump=True, ti=5.0))

    def test_bad_parameters_refused(self):
        bad = [dict(kp=math.nan), dict(ti=-1.0), dict(td=-0.1), dict(pv_inf=10.0, pv_sup=0.0),
               dict(out_inf=5.0, out_sup=0.0), dict(rev_dir=2), dict(kp="1"), dict(outbias=math.inf), dict(kp=True)]
        for changes in bad:
            with self.assertRaises(ValueError, msg=changes):
                PidffModel(para(**changes))
        with self.assertRaises(ValueError):
            PidffModel({k: v for k, v in para().items() if k != "kd"})
        with self.assertRaises(ValueError):
            PidffModel(dict(para(), unknown=1.0))
        with self.assertRaises(ValueError):
            PidffModel(para(), out_init=math.nan)

    def test_bad_dt_refused(self):
        block = PidffModel(para())
        for dt in (0.0, -1.0, math.nan, math.inf, True):
            with self.assertRaises(ValueError):
                block.step(pv=0.0, sp=0.0, dt=dt)


if __name__ == "__main__":
    unittest.main()
