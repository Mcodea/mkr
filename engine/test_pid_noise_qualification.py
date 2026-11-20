"""Safety regression tests: deterministic sensor noise and derivative filter qualification.

All test signals are synthetic; these do not constitute hardware validation.
"""
import copy
import json
import math
import unittest

from mkr_engine import respond
from pid_simulation import calculate
from test_pid_simulation import case


class PidNoiseQualificationTests(unittest.TestCase):
    def test_zero_noise_exactly_matches_legacy_nominal_signals(self):
        data = case()
        count = round(data["durationSeconds"] / data["sampleSeconds"])
        reference = calculate(data)
        noisy = calculate(data | {"measurementNoise": [0.0] * count})
        self.assertEqual(data, case())
        for metric in ("iae", "ise", "finalError", "saturationSeconds",
                       "rateLimitedSeconds", "frozenSteps", "setpointResponse"):
            self.assertEqual(reference[metric], noisy[metric])
        for left, right in zip(reference["points"], noisy["points"]):
            for field in ("seconds", "pv", "cv", "mv", "derivative", "disturbance"):
                self.assertEqual(left[field], right[field])
            if right["measuredPv"] is not None:
                self.assertEqual(right["pv"], right["measuredPv"])
        self.assertNotIn("deterministic_measurement_noise", reference["warnings"])
        self.assertIn("deterministic_measurement_noise", noisy["warnings"])

    def test_sensor_impulse_filter_has_independent_discrete_oracle(self):
        data = case(setpoint=0, sampleSeconds=0.1, durationSeconds=2,
                    controller=dict(kp=0.5, tiSeconds=4,
                                    tdSeconds=0.4, filterSeconds=0.2))
        n = 20
        noise = [1.0] + [0.0] * (n - 1)
        original = copy.deepcopy(data)
        output = calculate(data | {"measurementNoise": noise})
        self.assertEqual(data, original)
        previous_derivative = 0.0
        previous_measured = 0.0
        kp, td, tf, h = 0.5, 0.4, 0.2, 0.1
        for k, point in enumerate(output["points"][:-1]):
            self.assertAlmostEqual(point["measuredPv"] - point["pv"], noise[k], delta=1e-13)
            measured = point["measuredPv"]
            expected_d = tf / (tf + h) * previous_derivative - (
                kp * td / (tf + h)) * (measured - previous_measured)
            self.assertAlmostEqual(point["derivative"], expected_d, delta=1e-12)
            previous_derivative, previous_measured = expected_d, measured
        self.assertAlmostEqual(output["points"][0]["derivative"], -2 / 3, delta=1e-12)
        self.assertEqual(output["points"][0]["pv"], 0)
        self.assertEqual(output["points"][0]["measuredPv"], 1)
        self.assertGreater(output["iae"], 0)
        self.assertNotEqual(output["points"][0]["cv"], 0)

    def test_longer_filter_reduces_immediate_derivative_impulse(self):
        base = case(setpoint=0, sampleSeconds=0.1, durationSeconds=2)
        noise = [1.] + [0.] * 19
        short = calculate(base | {
            "measurementNoise": noise,
            "controller": dict(kp=.5, tiSeconds=4, tdSeconds=.4, filterSeconds=.05)})
        long = calculate(base | {
            "measurementNoise": noise,
            "controller": dict(kp=.5, tiSeconds=4, tdSeconds=.4, filterSeconds=.5)})
        self.assertLess(abs(long["points"][0]["derivative"]),
                        abs(short["points"][0]["derivative"]))
        # Physical response must not be confused with the sensor reading.
        self.assertEqual(long["points"][0]["pv"], 0)
        self.assertEqual(short["points"][0]["pv"], 0)

    def test_rejects_invalid_noise_length_type_and_nonfinite(self):
        base = case(sampleSeconds=.1, durationSeconds=2)
        for values in ([], [0.] * 19, [0.] * 21, "zeros", None,
                       [0.] * 19 + [float("nan")],
                       [0.] * 19 + [float("inf")],
                       [0.] * 19 + [True]):
            with self.subTest(noise=repr(values)), self.assertRaisesRegex(
                    ValueError, "pid_noise"):
                calculate(base | {"measurementNoise": values})

    def test_ipc_reports_sensor_provenance_and_rejects_bad_noise(self):
        inputs = case(sampleSeconds=.1, durationSeconds=2)
        request = dict(version=1, requestId="noise-qualification",
                       operation="simulate_pid_experimental",
                       payload=inputs | {"measurementNoise": [0.01] * 20})
        response = respond(json.dumps(request).encode())
        self.assertNotIn("error", response)
        self.assertEqual(response["result"]["algorithmVersion"], "0.2.0")
        self.assertIn("deterministic_measurement_noise", response["result"]["warnings"])
        self.assertAlmostEqual(response["result"]["points"][0]["measuredPv"], 0.01)
        request["payload"]["measurementNoise"] = [0.1]
        bad = respond(json.dumps(request).encode())
        self.assertEqual(bad["error"]["code"], "pid_noise")


if __name__ == "__main__":
    unittest.main()
