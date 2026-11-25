import base64
import math
import unittest
from datetime import datetime, timedelta, timezone

import numpy as np

from closed_loop import fit_fopdt, identify


def loop(kp=0.5, ti=25.0, gain=2.0, tau=30.0, delay=6.0, n=1500, sigma=0.03, disturbance=0.02, seed=1, sp_events=None,
         lower=-1000.0, upper=1000.0, manual_until=0, dt=1.0, extra_column=True):
    """Closed loop: ideal PI on a FOPDT, coloured unmeasured load disturbance, white measurement noise."""
    rng = np.random.default_rng(seed)
    events = sp_events if sp_events is not None else [(100, 1.0), (600, -1.0), (1000, 0.5)]
    sp = np.zeros(n)
    for at, level in events:
        sp[at:] = level
    a, d = math.exp(-dt / tau), int(round(delay / dt))
    y = np.zeros(n)
    u = np.zeros(n)
    load = np.zeros(n)
    integral = 0.0
    for i in range(1, n):
        load[i] = 0.98 * load[i - 1] + rng.normal(0, disturbance)
    measured = np.zeros(n)
    for i in range(n):
        measured[i] = y[i] + rng.normal(0, sigma)
        error = sp[i] - measured[i]
        integral += error * dt / ti
        u[i] = float(np.clip(kp * (error + integral), lower, upper))
        if i + 1 < n:
            y[i + 1] = a * y[i] + (1 - a) * gain * (u[i - d] if i - d >= 0 else 0.0) + (1 - a) * load[i]
    start = datetime(2026, 3, 1, tzinfo=timezone.utc)
    lines = ["timestamp,sp,mv,pv" + (",MANUAL" if extra_column else "")]
    for i in range(n):
        flag = f",{1 if i < manual_until else 0}" if extra_column else ""
        lines.append(f"{(start + timedelta(seconds=i * dt)).isoformat()},{sp[i]},{u[i]},{measured[i]}{flag}")
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",", timeColumn="timestamp",
                spColumn="sp", mvColumn="mv", pvColumn="pv"), u, measured


def request(source, training=(50, 700), validation=(750, 1450), **overrides):
    payload = dict(source=source, training=dict(rowStart=training[0], rowEnd=training[1]),
                   validation=dict(rowStart=validation[0], rowEnd=validation[1]),
                   mvLower=-1000.0, mvUpper=1000.0, spTolerance=1e-6, pvTolerance=0.1)
    payload.update(overrides)
    return payload


class Identification(unittest.TestCase):
    def test_a_clean_closed_loop_is_identified_with_its_dispersion_and_the_experimental_label(self):
        source, _, _ = loop(sigma=0.01, disturbance=0.005)
        result = identify(request(source))
        self.assertTrue(result["assessment"]["accepted"], result["assessment"]["reasons"])
        self.assertIn("correlated_residuals", result["warnings"])       # coloured load disturbance: announced, not a refusal
        self.assertAlmostEqual(result["gain"] / 2.0, 1, delta=0.1)
        self.assertAlmostEqual(result["tauSeconds"] / 30.0, 1, delta=0.2)
        self.assertLess(abs(result["delaySeconds"] - 6.0), 3.0)
        self.assertIn("closed_loop_experimental", result["warnings"])
        self.assertTrue(all(v > 0 for v in result["standardErrors"].values()))

    def test_it_does_not_depend_on_the_controller_tuning(self):
        for kp, ti in ((0.3, 40.0), (0.8, 15.0)):
            with self.subTest(kp=kp, ti=ti):
                source, _, _ = loop(kp=kp, ti=ti, sigma=0.01, disturbance=0.005)
                result = identify(request(source))
                self.assertAlmostEqual(result["gain"] / 2.0, 1, delta=0.15)
                self.assertAlmostEqual(result["tauSeconds"] / 30.0, 1, delta=0.25)

    def test_negative_gain_process(self):
        source, _, _ = loop(gain=-1.5, kp=-0.6, sigma=0.01, disturbance=0.005)
        result = identify(request(source))
        self.assertLess(result["gain"], 0)
        self.assertAlmostEqual(result["gain"] / -1.5, 1, delta=0.15)

    def test_it_beats_the_open_loop_fit_on_the_same_closed_loop_data(self):
        """Mean absolute error of tau over several noisy realisations: two-stage vs the fit of raw MV (the open-loop way)."""
        two_stage, naive = [], []
        for seed in range(6):
            source, u, pv = loop(sigma=0.05, disturbance=0.05, seed=seed)
            try:
                result = identify(request(source, pvTolerance=0.3))
            except ValueError:
                continue
            times = np.arange(len(u), dtype=float)
            window = slice(50, 701)
            raw_fit, _ = fit_fopdt(times[window] - times[50], u[window] - u[50:58].mean(), pv[window] - pv[50:58].mean())
            two_stage.append(abs(result["tauSeconds"] - 30.0))
            naive.append(abs(raw_fit[1] - 30.0))
        self.assertGreaterEqual(len(two_stage), 4)
        self.assertLess(np.mean(two_stage), np.mean(naive))

    def test_a_strong_unmeasured_disturbance_is_not_accepted_as_a_good_model(self):
        source, _, _ = loop(sigma=0.05, disturbance=0.6)
        try:
            result = identify(request(source, pvTolerance=0.3))
        except ValueError:
            return                                              # refused earlier: also acceptable
        self.assertFalse(result["assessment"]["accepted"])

    def test_the_published_dispersion_is_the_right_order_of_magnitude(self):
        """Monte Carlo calibration: over several realisations the reported standard error must not be far below the real spread."""
        estimates, reported = [], []
        for seed in range(10):
            source, _, _ = loop(sigma=0.05, disturbance=0.02, seed=seed)
            result = identify(request(source, pvTolerance=0.3))
            estimates.append(result["tauSeconds"])
            reported.append(result["standardErrors"]["tauSeconds"])
        ratio = float(np.mean(reported) / np.std(estimates, ddof=1))
        self.assertGreater(ratio, 0.5)         # measured about 0.7: still an underestimate, never to be read as a guarantee
        self.assertLess(ratio, 2.0)


class Refusals(unittest.TestCase):
    def refuse(self, code, source, **overrides):
        with self.assertRaisesRegex(ValueError, code):
            identify(request(source, **overrides))

    def test_saturation_is_refused(self):
        source, _, _ = loop(lower=-0.8, upper=0.8, sigma=0.01, disturbance=0.005)
        self.refuse("cl_saturation", source, mvLower=-0.8, mvUpper=0.8)

    def test_a_flat_setpoint_is_refused(self):
        source, _, _ = loop(sp_events=[])
        self.refuse("cl_excitation", source)

    def test_a_setpoint_change_buried_in_the_noise_is_refused(self):
        source, _, _ = loop(sp_events=[(100, 0.05)], sigma=0.05)
        self.refuse("cl_excitation", source)

    def test_manual_rows_in_a_window_are_refused(self):
        source, _, _ = loop(manual_until=200)
        self.refuse("cl_manual_in_window", source)

    def test_without_the_mode_column_nothing_is_assumed(self):
        source, _, _ = loop(extra_column=False, sigma=0.01, disturbance=0.005)
        self.assertIn("assessment", identify(request(source)))

    def test_overlapping_windows_are_refused(self):
        source, _, _ = loop()
        self.refuse("cl_independence", source, validation=(600, 1400))

    def test_no_quiet_baseline_is_refused(self):
        source, _, _ = loop(sp_events=[(3, 1.0), (600, -1.0)])
        self.refuse("cl_baseline", source, training=(0, 500), validation=(550, 1100))

    def test_a_non_uniform_period_is_refused(self):
        source, _, _ = loop()
        text = base64.b64decode(source["raw"]).decode().split("\n")
        del text[300]                                            # one missing sample inside both... training window
        self.refuse("cl_sampling", dict(source, raw=base64.b64encode("\n".join(text).encode()).decode()))

    def test_bad_mapping(self):
        source, _, _ = loop()
        with self.assertRaisesRegex(ValueError, "cl_mapping"):
            identify(dict(request(source), extra=1))


class Boundary(unittest.TestCase):
    def test_the_engine_serves_it_and_returns_refusal_codes(self):
        import json
        from mkr_engine import respond
        source, _, _ = loop(sigma=0.01, disturbance=0.005)
        ok = respond(json.dumps(dict(version=1, requestId="cl", operation="identify_closed_loop", payload=request(source))).encode())
        self.assertEqual(ok["result"]["method"], "two_stage_fir")
        flat, _, _ = loop(sp_events=[])
        refused = respond(json.dumps(dict(version=1, requestId="cl2", operation="identify_closed_loop", payload=request(flat))).encode())
        self.assertEqual(refused["error"]["code"], "cl_excitation")


if __name__ == "__main__":
    unittest.main()
