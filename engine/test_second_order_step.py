import math
import unittest

from fopdt_simulation import second_order_step


class SecondOrderStepTests(unittest.TestCase):
    def test_fractional_delay_and_repeated_poles_against_analytic_step(self):
        for fast in (0.8, 3, 3 - 1e-12):
            for h in (0.2, 0.05):
                step = second_order_step(-2, 3, fast, 0.37, h, 8)
                pv, history = 0.0, []
                for i in range(round(8/h)):
                    history.append(1)
                    pv = step(pv, history)
                    t = max(0, (i+1)*h - 0.37)
                    if abs(3-fast) < 1e-10:
                        expected = 1 - (1+t/3)*math.exp(-t/3)
                    else:
                        expected = 1 - (3*math.exp(-t/3)-fast*math.exp(-t/fast))/(3-fast)
                    self.assertAlmostEqual(pv, -2*expected, places=11)
