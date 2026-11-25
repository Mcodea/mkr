"""Regression tests for the exploratory PI manual/automatic transfer."""
import math
import unittest

from pi_mode_transition import calculate


def scenario(**changes):
    base = dict(gain=2, tauSeconds=4, delaySeconds=0, sampleSeconds=0.1,
                durationSeconds=2, setpoint=1, minimumMv=-2, maximumMv=2,
                manualMv=0.5, automaticAtSeconds=1,
                kp=0.5, tiSeconds=4)
    return calculate(base | changes)


class PiModeTransitionTests(unittest.TestCase):
    def test_bumpless_transfer_tracks_applied_manual_mv(self):
        result = scenario()
        points = result["points"]
        previous = points[9]
        first_auto = points[10]
        self.assertEqual(previous["mode"], "manual")
        self.assertIsNone(previous["cv"])
        self.assertEqual(first_auto["mode"], "automatic")
        self.assertAlmostEqual(first_auto["mv"], previous["mv"])
        self.assertAlmostEqual(first_auto["cv"], previous["mv"])
        self.assertAlmostEqual(result["transferBumpMv"], 0)
        self.assertEqual(len(points), 21)
        self.assertIsNone(points[-1]["mv"])

    def test_automatic_control_resumes_after_transfer(self):
        first, second = scenario()["points"][10:12]
        self.assertAlmostEqual(first["pv"], 1 - math.exp(-1 / 4))
        self.assertAlmostEqual(second["pv"], 1 - math.exp(-1.1 / 4))
        expected = first["mv"] + 0.5 * (first["pv"] - second["pv"])
        expected += 0.5 * 0.1 / 4 * (1 - first["pv"])
        self.assertAlmostEqual(second["cv"], expected)

    def test_transfer_tracks_saturated_command_not_requested(self):
        result = scenario(manualMv=9)
        first_auto = result["points"][10]
        self.assertEqual(result["points"][9]["mv"], 2)
        self.assertEqual(first_auto["cv"], 2)
        self.assertEqual(first_auto["mv"], 2)
        self.assertGreater(result["saturationSeconds"], 0)

    def test_transfer_tracks_rate_limited_command(self):
        result = scenario(manualMv=2, maximumMvRate=0.5)
        points = result["points"]
        self.assertGreater(result["rateLimitedSeconds"], 0)
        self.assertAlmostEqual(points[9]["mv"], 0.5)
        self.assertAlmostEqual(points[10]["mv"], points[9]["mv"])
        self.assertAlmostEqual(result["transferBumpMv"], 0)
        for before, after in zip(points, points[1:-1]):
            self.assertLessEqual(abs(after["mv"] - before["mv"]), 0.05 + 1e-12)

    def test_reverse_process_sign_and_fractional_delay(self):
        result = scenario(gain=-2, kp=-0.5, delaySeconds=0.35, manualMv=-0.5)
        self.assertAlmostEqual(result["transferBumpMv"], 0)
        self.assertLess(result["points"][10]["mv"], 0)
        self.assertGreater(result["points"][-1]["pv"], 0)

    def test_rejects_bad_transition_or_controller(self):
        cases = (
            (dict(automaticAtSeconds=0), "sim_domain"),
            (dict(automaticAtSeconds=2), "sim_domain"),
            (dict(automaticAtSeconds=0.15), "sim_grid"),
            (dict(kp=-0.5), "sim_domain"),
            (dict(maximumMvRate=0), "sim_domain"),
            (dict(manualMv=True), "sim_input"),
            (dict(tiSeconds=0), "sim_domain"),
        )
        for changes, code in cases:
            with self.subTest(changes=changes):
                with self.assertRaisesRegex(ValueError, code):
                    scenario(**changes)


if __name__ == "__main__":
    unittest.main()
