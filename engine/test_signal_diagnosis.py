"""Diagnostic automatique des signaux : règles fixées avant évaluation, vérifiées sur des cas construits (vérité connue) — pas sur les données réelles."""
import math, random, unittest

import signal_diagnosis as sd


def noisy_ramp(n=400, seed=1, noise=0.05):
    rng = random.Random(seed)
    return [20 + 10 * math.sin(i / 40) + rng.gauss(0, noise) for i in range(n)]


class SaturationTests(unittest.TestCase):
    def test_actuator_stuck_at_its_bound_is_reported_with_its_intervals(self):
        values = [min(100.0, 40 + 0.5 * i) for i in range(200)]               # monte puis reste à 100
        out = sd.saturation(values)
        self.assertEqual(out["bounds"][0]["side"], "max")
        self.assertEqual(out["bounds"][0]["intervals"], [(120, 199)])
        self.assertAlmostEqual(out["fraction"], 80 / 200)

    def test_a_continuous_noisy_signal_is_not_saturated_and_a_single_extreme_is_not_a_plateau(self):
        out = sd.saturation(noisy_ramp())
        self.assertEqual(out["bounds"] if "bounds" in out else [], [])
        values = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 9.0]            # deux échantillons au maximum : sous MIN_RUN
        self.assertEqual(sd.saturation(values)["bounds"], [])

    def test_min_run_is_three_not_tuned(self):
        self.assertEqual(sd.MIN_RUN, 3)
        self.assertEqual(sd.ROBUST_Z, 3.5)


class SensorDropoutTests(unittest.TestCase):
    def test_zero_runs_reached_by_a_jump_are_flagged_and_nothing_is_removed(self):
        values = noisy_ramp()
        original = list(values)
        for i in list(range(100, 108)) + [250]:
            values[i] = 0.0
        flagged = sd.possible_sensor_dropout(values)
        self.assertEqual(flagged, list(range(100, 108)) + [250])
        self.assertEqual([v for i, v in enumerate(values) if i not in flagged], [v for i, v in enumerate(original) if i not in flagged])   # entrée non modifiée

    def test_a_real_zero_at_the_start_is_not_a_dropout_when_it_is_not_reached_by_a_jump(self):
        values = [0.0, 0.1, 0.25, 0.45, 0.7, 1.0, 1.4, 1.9, 2.5, 3.2] + [3.2 + 0.7 * i for i in range(1, 60)]      # démarrage progressif depuis zéro
        self.assertEqual(sd.possible_sensor_dropout(values), [])

    def test_a_constant_or_too_short_signal_gives_no_verdict(self):
        self.assertEqual(sd.possible_sensor_dropout([5.0] * 50), [])
        self.assertEqual(sd.possible_sensor_dropout([1.0, 2.0]), [])


class CadenceTests(unittest.TestCase):
    def test_duplicated_timestamps_reveal_a_clock_coarser_than_the_sampling(self):
        stamps = [60 * (i // 60) for i in range(600)]                         # 1 Hz échantillonné, horloge à la minute
        out = sd.cadence(stamps)
        self.assertTrue(out["clock_resolution_coarser_than_sampling"])
        self.assertEqual(out["duplicate_timestamps"], 600 - 10)
        self.assertEqual(out["median_step_s"], 60)

    def test_regular_stamps_are_clean(self):
        out = sd.cadence([float(i) for i in range(100)])
        self.assertFalse(out["clock_resolution_coarser_than_sampling"]); self.assertEqual(out["median_step_s"], 1.0)


class DiagnoseTests(unittest.TestCase):
    def test_role_decides_the_checks_and_the_input_is_validated(self):
        t = [float(i) for i in range(30)]
        self.assertNotIn("possibleSensorDropout", sd.diagnose(t, [1.0] * 30, "MV"))
        self.assertIn("possibleSensorDropout", sd.diagnose(t, [1.0] * 30, "PV"))
        with self.assertRaises(ValueError): sd.diagnose(t, [1.0] * 29, "PV")
        with self.assertRaises(ValueError): sd.diagnose(t, [1.0] * 30, "xx")


if __name__ == "__main__":
    unittest.main()
