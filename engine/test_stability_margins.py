import math
import unittest

from pi_simulation import calculate as simulate_pi
from pi_tuning import calculate as tune_pi
from stability_margins import analyze


class AnalyticOracles(unittest.TestCase):
    def test_proportional_loop_on_first_order_with_delay_matches_closed_forms(self):
        # L = k exp(-s th) / (tau s + 1), k = Kp K: |L| = 1 gives wc = sqrt(k^2 - 1) / tau exactly,
        # phase margin = pi - wc th - atan(wc tau). Independent of the numerical search in the module.
        k, tau, theta = 4.0, 5.0, 1.2
        result = analyze(gain=2.0, taus=[tau], delay=theta, kp=2.0, ti=math.inf, td=0)
        wc = math.sqrt(k * k - 1) / tau
        self.assertAlmostEqual(result["gainCrossoverRadPerSecond"], wc, places=6)
        expected = math.degrees(math.pi - wc * theta - math.atan(wc * tau))
        self.assertAlmostEqual(result["phaseMarginDegrees"], expected, places=4)
        self.assertAlmostEqual(result["delayMarginSeconds"], math.radians(expected) / wc, places=6)
        # Phase crossover: w th + atan(w tau) = pi, solved here by plain bisection.
        low, high = 1e-6, 50.0
        for _ in range(200):
            mid = (low + high) / 2
            low, high = (mid, high) if mid * theta + math.atan(mid * tau) < math.pi else (low, mid)
        w180 = (low + high) / 2
        self.assertAlmostEqual(result["phaseCrossoverRadPerSecond"], w180, places=5)
        self.assertAlmostEqual(result["gainMargin"], math.sqrt(1 + (w180 * tau) ** 2) / k, places=5)

    def test_integrator_with_delay_closed_forms(self):
        # PI with Ti = tau cancels the lag: L = k exp(-s th) / s with k = Kp K / tau.
        tau, theta, kp, gain = 8.0, 1.5, 0.3, 2.0
        k = kp * gain / tau
        result = analyze(gain=gain, taus=[tau], delay=theta, kp=kp, ti=tau, td=0)
        self.assertAlmostEqual(result["gainCrossoverRadPerSecond"], k, places=7)
        self.assertAlmostEqual(result["phaseMarginDegrees"], 90 - math.degrees(k * theta), places=5)
        self.assertAlmostEqual(result["gainMargin"], math.pi / (2 * k * theta), places=5)
        self.assertAlmostEqual(result["phaseCrossoverRadPerSecond"], math.pi / (2 * theta), places=6)

    def test_lambda_tuning_without_delay_has_ninety_degrees_and_no_overshoot_in_sensitivity(self):
        tau, lam = 6.0, 2.0
        tuning = tune_pi(dict(gain=1.5, tauSeconds=tau, delaySeconds=0, responseSeconds=lam))["imc"]
        result = analyze(gain=1.5, taus=[tau], delay=0, kp=tuning["kp"], ti=tuning["tiSeconds"], td=0)
        self.assertAlmostEqual(result["gainCrossoverRadPerSecond"], 1 / lam, places=8)
        self.assertAlmostEqual(result["phaseMarginDegrees"], 90.0, places=6)
        self.assertIsNone(result["gainMargin"])                       # no phase crossover: infinite margin
        self.assertAlmostEqual(result["peakSensitivity"], 1.0, places=3)
        self.assertEqual(result["warnings"], [])

    def test_second_order_plant_with_pid_has_the_expected_integrator_form(self):
        # Series PID cancelling both lags of a SOPDT: L = k exp(-s th) / s again.
        t1, t2, theta, kc, ti, gain = 9.0, 3.0, 1.0, 0.8, 9.0, 1.2
        # Series Kc (1 + 1/(Ti s))(1 + Td s) with Td = t2 → ideal form below.
        td_series = t2
        kp = kc * (ti + td_series) / ti
        ti_ideal, td_ideal = ti + td_series, ti * td_series / (ti + td_series)
        result = analyze(gain=gain, taus=[t1, t2], delay=theta, kp=kp, ti=ti_ideal, td=td_ideal)
        k = kc * gain / ti
        self.assertAlmostEqual(result["gainCrossoverRadPerSecond"], k, places=6)
        self.assertAlmostEqual(result["phaseMarginDegrees"], 90 - math.degrees(k * theta), places=4)


class TimeDomainOracle(unittest.TestCase):
    """The gain margin predicts the gain at which the sampled closed loop becomes unstable."""

    def final_error(self, tuning, plant_gain, tau=5.0, theta=1.0, duration=100.0):
        step = 0.02
        payload = dict(gain=plant_gain, tauSeconds=tau, delaySeconds=theta, sampleSeconds=step, durationSeconds=duration,
                       setpoint=1.0, minimumMv=-1e9, maximumMv=1e9, disturbance=0.0, disturbanceAtSeconds=0.0,
                       imc=dict(kp=tuning["kp"], tiSeconds=tuning["tiSeconds"]), simc=dict(kp=tuning["kp"], tiSeconds=tuning["tiSeconds"]))
        points = simulate_pi(payload)["simc"]["points"]
        return max(abs(1.0 - p["pv"]) for p in points[-200:] if p["pv"] is not None)

    def test_scaling_the_plant_gain_by_the_gain_margin_brackets_instability(self):
        tau, theta, gain = 5.0, 1.0, 1.0
        tuning = tune_pi(dict(gain=gain, tauSeconds=tau, delaySeconds=theta, responseSeconds=theta))["simc"]
        margins = analyze(gain=gain, taus=[tau], delay=theta, kp=tuning["kp"], ti=tuning["tiSeconds"], td=0)
        gm = margins["gainMargin"]
        self.assertGreater(gm, 1.5)
        self.assertLess(self.final_error(tuning, gain * gm * 0.9), 0.05)     # damped: converges to the setpoint
        self.assertGreater(self.final_error(tuning, gain * gm * 1.1), 1.0)   # beyond the margin: diverges


class ProductionGuards(unittest.TestCase):
    def test_warnings_and_refusals(self):
        poor = analyze(gain=1.0, taus=[5.0], delay=1.0, kp=12.0, ti=math.inf, td=0)    # P gain above the ultimate gain (about 9.5)
        self.assertIn("nominal_unstable", poor["warnings"])
        self.assertLess(poor["gainMargin"], 1)
        aggressive = tune_pi(dict(gain=1.0, tauSeconds=5.0, delaySeconds=2.0, responseSeconds=0.2))["simc"]
        result = analyze(gain=1.0, taus=[5.0], delay=2.0, kp=aggressive["kp"], ti=aggressive["tiSeconds"], td=0)
        self.assertTrue({"low_phase_margin", "low_gain_margin", "high_sensitivity"} & set(result["warnings"]), result)
        weak = analyze(gain=1.0, taus=[5.0], delay=1.0, kp=0.05, ti=math.inf, td=0)
        self.assertIsNone(weak["phaseMarginDegrees"])
        self.assertIn("no_gain_crossover", weak["warnings"])
        for bad in (dict(gain=1.0, taus=[5.0], delay=1.0, kp=-1.0, ti=5.0, td=0),
                    dict(gain=1.0, taus=[0.0], delay=1.0, kp=1.0, ti=5.0, td=0),
                    dict(gain=1.0, taus=[5.0], delay=-1.0, kp=1.0, ti=5.0, td=0),
                    dict(gain=math.nan, taus=[5.0], delay=1.0, kp=1.0, ti=5.0, td=0)):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                analyze(**bad)


if __name__ == "__main__":
    unittest.main()
