"""Comportement documenté du PI_B (33002535.16, p. 151-162). Oracles ANALYTIQUES, écrits sans appeler le modèle :
intégrale d'un écart constant, composante proportionnelle d'un saut d'écart, zone morte, sens d'action, bornage, mots d'état.
Les points marqués INTERPRÉTATION dans block_pi_b.py ne sont testés que comme choix documentés, pas comme faits du manuel."""
import math
import unittest

from block_pi_b import BIT4, BIT5, BIT6, BIT7, PiBModel


def make(**kw):
    base = dict(pv_inf=0, pv_sup=100, out_inf=0, out_sup=100, rev_dir=0, kp=2.0, ti=10.0, out_init=50)
    base.update(kw)
    return PiBModel(**base)


class IntegralAction(unittest.TestCase):
    def test_constant_error_ramps_linearly(self):
        # Écart DEV = PV - SP = -10, action directe (rev_dir=0, sense=-1) : OUT(n) = OUT0 + n * kp * dt/ti * 10 = 50 + n*2.
        m = make()
        for n in range(1, 6):
            r = m.execute(40, 50, dt=1.0)
            self.assertAlmostEqual(r.out, 50 + 2.0 * n, places=12)
            self.assertAlmostEqual(r.outd, 2.0, places=12)
            self.assertAlmostEqual(r.dev, -10.0)

    def test_reverse_action_flips_sign(self):
        m = make(rev_dir=1)
        self.assertAlmostEqual(m.execute(40, 50).out, 48.0, places=12)

    def test_proportional_step_on_first_error_change(self):
        # Après un cycle à écart nul (état initialisé), un saut d'écart de -10 donne TermP = -(-1)... : sense*kp*Delta(DEV) = (-1)(2)(-10) = +20, TermI = +2.
        m = make()
        m.execute(50, 50)
        r = m.execute(40, 50)
        self.assertAlmostEqual(r.outd, 22.0, places=12)
        self.assertAlmostEqual(r.out, 72.0, places=12)

    def test_discrete_matches_closed_form_for_pure_ramp_error(self):
        # Écart linéaire DEV_k = a*k, dt = 0,5 : somme exacte de la récurrence PI incrémentale.
        a, dt, kp, ti = 0.3, 0.5, 1.7, 8.0
        m = make(kp=kp, ti=ti, out_inf=-1e6, out_sup=1e6, pv_inf=-1e6, pv_sup=1e6, out_init=0)
        out = 0.0
        prev = 0.0
        for k in range(20):
            dev = a * k
            r = m.execute(dev, 0.0, dt=dt)
            delta = 0.0 if k == 0 else dev - prev          # Delta(DEV)=0 au premier cycle (I2)
            out += -kp * delta - kp * dt / ti * dev
            prev = dev
            self.assertAlmostEqual(r.out, out, places=9)


class AbsoluteAlgorithm(unittest.TestCase):
    def test_ti_zero_is_proportional_plus_bias(self):
        m = make(ti=0, kp=3.0, outbias=5.0, rev_dir=1)
        r = m.execute(60, 50)        # DEV=10, sense=+1 : 30 + 5
        self.assertAlmostEqual(r.out, 35.0, places=12)
        self.assertAlmostEqual(r.outd, 35.0 - 50.0, places=12)

    def test_absolute_output_is_clamped_but_outd_is_not(self):
        m = make(ti=0, kp=100.0, rev_dir=1)
        r = m.execute(60, 50)        # 1000 -> borné à 100
        self.assertEqual(r.out, 100.0)
        self.assertAlmostEqual(r.outd, 1000.0 - 50.0, places=9)
        self.assertTrue(r.status & BIT6)


class DeadBand(unittest.TestCase):
    def test_inside_band_calculation_uses_zero(self):
        m = make(dband=5.0)
        self.assertEqual(m.execute(47, 50).out, 50.0)      # |DEV|=3 < 5

    def test_outside_band_uses_full_error_not_shifted(self):
        # Saut à dband (p. 158) : DEV=-6 avec dband=5 compte pour -6, pas pour -1.
        m = make(dband=5.0, ti=0, kp=1.0, rev_dir=1, out_init=0)
        self.assertAlmostEqual(m.execute(44, 50).out, 0.0)   # -6 -> -6 borné à 0
        m2 = make(dband=5.0, ti=0, kp=1.0, rev_dir=1, out_init=0, outbias=20)
        self.assertAlmostEqual(m2.execute(56, 50).out, 26.0)  # +6 + 20


class SaturationAndStatus(unittest.TestCase):
    def test_integral_stops_at_limit_no_windup(self):
        m = make()
        for _ in range(100):
            r = m.execute(0, 100, dt=1.0)    # écart -100 : monte de 20 par cycle
        self.assertEqual(r.out, 100.0)
        self.assertTrue(r.status & BIT6)
        # Dès que l'écart s'inverse, la sortie redescend immédiatement (pas d'accumulation cachée).
        r = m.execute(100, 100)
        self.assertLessEqual(r.out, 100.0)
        r2 = m.execute(100, 0)               # DEV=+100 : redescend
        self.assertLess(r2.out, 100.0)

    def test_lower_limit_bit(self):
        m = make(out_init=1)
        r = m.execute(100, 0, dt=1.0)
        self.assertEqual(r.out, 0.0)
        self.assertTrue(r.status & BIT5)

    def test_setpoint_out_of_range_sets_bit4_and_is_clamped(self):
        m = make()
        r = m.execute(40, 150)
        self.assertTrue(r.status & BIT4)
        self.assertAlmostEqual(r.dev, 40 - 100)    # SP borné à pv_sup

    def test_negative_kp_and_dband_replaced_by_zero_with_bit4(self):
        m = make(kp=-1.0, ti=0, out_init=50)
        r = m.execute(60, 50)
        self.assertTrue(r.status & BIT4)
        self.assertEqual(r.out, 0.0)  # kp remplacé par 0, outbias 0

    def test_equal_pv_range_is_error_and_outputs_unchanged(self):
        m = make(pv_inf=10, pv_sup=10)
        r = m.execute(5, 5)
        self.assertEqual(r.error, "pv_range_identical")
        self.assertTrue(r.status & BIT7)
        self.assertEqual((r.out, r.outd, r.dev), (50.0, 0.0, 0.0))

    def test_non_finite_input_leaves_outputs_unchanged(self):
        m = make()
        m.execute(40, 50)
        before = (m.out, m.outd, m.dev)
        r = m.execute(float("nan"), 50)
        self.assertEqual(r.error, "float_input")
        self.assertEqual((r.out, r.outd, r.dev), before)


class PureIntegralAndRcpy(unittest.TestCase):
    def test_kp_zero_uses_range_ratio(self):
        # TermI = sense * (out_sup-out_inf)/(pv_sup-pv_inf) * dt/ti * DEV = (-1)(1)(1/10)(-10) = +1
        m = make(kp=0.0)
        self.assertAlmostEqual(m.execute(40, 50).out, 51.0, places=12)

    def test_en_rcpy_builds_on_rcpy_and_is_not_limited(self):
        m = make(en_rcpy=True)
        r = m.execute(40, 50, rcpy=150.0)
        self.assertAlmostEqual(r.out, 152.0, places=12)   # 150 + 2, non borné (note p. 158)


class ModesAndBumpless(unittest.TestCase):
    def test_tracking_follows_tr_i_limited(self):
        m = make()
        self.assertEqual(m.execute(40, 50, tracking=True, tr_i=70).out, 70.0)
        self.assertEqual(m.execute(40, 50, tracking=True, tr_i=170).out, 100.0)

    def test_manual_to_auto_has_no_bump_when_ti_positive(self):
        m = make()
        m.execute(40, 50, auto=False, out_manual=30)
        r = m.execute(40, 50)            # Delta(DEV)=0 (I2) : seule l'intégrale agit
        self.assertAlmostEqual(r.out, 32.0, places=12)

    def test_manual_to_auto_bump_when_ti_zero(self):
        m = make(ti=0, kp=1.0, rev_dir=1)
        m.execute(60, 50, auto=False, out_manual=30)
        self.assertAlmostEqual(m.execute(60, 50).out, 10.0)   # repart de kp*DEV : à-coup (p. 159)


if __name__ == "__main__":
    unittest.main()
