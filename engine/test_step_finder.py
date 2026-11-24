import base64
import json
import math
import unittest
from datetime import datetime, timedelta, timezone

import numpy as np

from fopdt import identify
from mkr_engine import respond
from step_finder import find


def recording(events, duration=3000, period=5.0, sigma=0.0, seed=1, tau=40.0, delay=10.0, gain=2.0,
              drift=None, blank=None):
    """Long MV/PV recording of a first-order process; events = [(time, new_mv)]; MV steps are exact."""
    rng = np.random.default_rng(seed)
    start = datetime(2026, 6, 1, tzinfo=timezone.utc)
    times = np.arange(0, duration, period)
    mv = np.zeros(len(times))
    for at, level in events:
        mv[times >= at] = level
    # Exact response to a piecewise-constant input: superposition of delayed first-order steps.
    pv = np.full(len(times), 20.0)
    previous = 0.0
    for at, level in events:
        x = np.maximum(times - at - delay, 0)
        pv += gain * (level - previous) * (1 - np.exp(-x / tau)) * (times >= at + delay)
        previous = level
    pv += sigma * rng.standard_normal(len(times))
    if drift:
        pv += drift * times
    lines = ["timestamp;mv;pv"]
    for i, t in enumerate(times):
        value = "" if blank is not None and i in blank else repr(float(pv[i]))
        lines.append(f"{(start + timedelta(seconds=float(t))).isoformat()};{float(mv[i])!r};{value}")
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=";",
                timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


def request(source, mv_tol=0.01, pv_tol=0.01):
    return dict(source=source, mvTolerance=mv_tol, pvTolerance=pv_tol)


class StepFinderTests(unittest.TestCase):
    def test_finds_each_clean_step_and_pairs_opposite_directions(self):
        source = recording([(300, 5.0), (1500, 0.0), (2400, 4.0)], duration=2500)
        result = find(request(source))
        steps = {c["stepRow"]: c for c in result["candidates"]}
        self.assertEqual(sorted(steps), [60, 300, 480])                # rows = time / 5 s
        first = steps[60]
        self.assertEqual((first["mvBefore"], first["mvAfter"], first["amplitude"]), (0.0, 5.0, 5.0))
        self.assertEqual(first["rowEnd"], 299)                          # window stops before the next MV change
        self.assertTrue(first["settled"])
        self.assertAlmostEqual(first["pvResponse"], 2.0 * 5.0, delta=0.01)
        self.assertEqual(steps[300]["amplitude"], -5.0)
        # The last step is observed for 100 s = 2.5 time constants only: listed, but flagged as not settled.
        self.assertFalse(steps[480]["settled"])
        self.assertIn("response_not_settled", steps[480]["warnings"])
        best = result["pairs"][0]
        self.assertEqual((best["firstId"], best["secondId"]), (1, 2))
        self.assertTrue(best["oppositeDirection"])
        self.assertTrue(best["firstSettled"] and best["secondSettled"])
        # The second baseline (60 samples back) lies inside the first response window: the plateau is split.
        self.assertEqual(best["notes"], ["shared_plateau_split"])

    def test_every_listed_window_is_accepted_by_the_estimators(self):
        source = recording([(300, 5.0), (1500, 0.0)], duration=2700)
        result = find(request(source))
        pair = result["pairs"][0]
        trials = dict(training=dict(source, **pair["firstWindow"]), validation=dict(source, **pair["secondWindow"]),
                      mvTolerance=0.01, pvTolerance=0.01)
        fit = identify(trials)                                          # same file, two disjoint windows
        self.assertAlmostEqual(fit["gain"], 2.0, places=4)
        self.assertAlmostEqual(fit["tauSeconds"], 40.0, delta=0.05)
        self.assertLess(fit["validation"]["rmse"], 1e-3)

    def test_ramp_is_rejected_not_used(self):
        source = recording([(300, 1.0), (305, 2.0), (310, 3.0), (315, 4.0)], duration=1500)
        result = find(request(source))
        self.assertEqual(result["candidates"], [])
        self.assertTrue(all(r["reason"] == "ramp_or_multi_sample_change" for r in result["rejected"]))

    def test_second_mv_change_during_response_is_no_trial(self):
        source = recording([(300, 5.0), (340, 3.0)], duration=1500)    # 8 samples between the two changes
        result = find(request(source))
        reasons = {r["stepRow"]: r["reason"] for r in result["rejected"]}
        self.assertEqual(reasons[60], "response_window_too_short")
        self.assertEqual(reasons[68], "baseline_too_short_or_unstable")   # PV still moving: no valid baseline
        self.assertEqual(result["candidates"], [])

    def test_drifting_baseline_is_not_a_trial(self):
        source = recording([(300, 5.0)], duration=1500, drift=0.01)    # 0.01 per second ≫ tolerance
        result = find(request(source))
        self.assertEqual(result["candidates"], [])
        self.assertEqual(result["rejected"][0]["reason"], "baseline_too_short_or_unstable")

    def test_missing_pv_truncates_windows_and_is_never_bridged(self):
        source = recording([(300, 5.0), (1500, 0.0)], duration=2700, blank={400})
        result = find(request(source))
        windows = {c["stepRow"]: (c["rowStart"], c["rowEnd"]) for c in result["candidates"]}
        self.assertEqual(windows[300][1], 399)                          # stops before the empty PV cell
        self.assertEqual(windows[60], (0, 299))
        # A hole in the baseline cuts it: the window starts after the hole, or the step is rejected.
        late = find(request(recording([(300, 5.0), (1500, 0.0)], duration=2700, blank={55})))
        self.assertEqual({c["stepRow"]: c["rowStart"] for c in late["candidates"]}[60], 56)
        close = find(request(recording([(300, 5.0), (1500, 0.0)], duration=2700, blank={58})))
        self.assertEqual({r["stepRow"]: r["reason"] for r in close["rejected"]}, {60: "baseline_too_short_or_unstable"})

    def test_noisy_recording_reports_noise_and_still_finds_steps(self):
        source = recording([(300, 5.0), (1500, 0.0)], duration=2700, sigma=0.01)
        result = find(request(source, pv_tol=0.1))
        self.assertEqual(len(result["candidates"]), 2)
        for candidate in result["candidates"]:
            self.assertAlmostEqual(candidate["baselineNoise"], 0.01, delta=0.004)

    def test_back_to_back_steps_share_a_plateau_and_are_split_for_independence(self):
        # Up at 300 s, down at 700 s: 400 s apart, the second baseline lies inside the first response window.
        source = recording([(300, 5.0), (700, 0.0)], duration=1400)
        result = find(request(source))
        pair = result["pairs"][0]
        self.assertIn("shared_plateau_split", pair["notes"])
        first, second = pair["firstWindow"], pair["secondWindow"]
        self.assertLess(first["rowEnd"], second["rowStart"])            # row-disjoint, as the estimators require
        self.assertEqual(second["rowStart"], 140 - 8)                   # 8 baseline samples kept for the second trial
        fit = identify(dict(training=dict(source, **first), validation=dict(source, **second),
                            mvTolerance=0.01, pvTolerance=0.01))
        self.assertAlmostEqual(fit["tauSeconds"], 40.0, delta=0.05)
        # 400 s = 10 time constants after the first step: the shared baseline still moves by 1e-3 (physics, not error).
        self.assertLess(fit["validation"]["rmse"], 5e-3)
        self.assertLess(fit["validation"]["rmse"], 0.01)                # well inside the declared PV tolerance

    def test_pairs_exclude_overlap_and_incompatible_amplitudes(self):
        source = recording([(300, 5.0), (1200, 0.0), (2100, 12.0)], duration=3000)
        result = find(request(source))
        ids = {(p["firstId"], p["secondId"]) for p in result["pairs"]}
        self.assertIn((1, 2), ids)
        self.assertNotIn((2, 3), ids)      # amplitudes -5 then +12: ratio 2.4 outside [0.5, 2]
        self.assertNotIn((1, 3), ids)      # 5 then 12 (ratio 2.4)

    def test_contract_and_ipc(self):
        source = recording([(300, 5.0), (1500, 0.0)], duration=2700)
        reply = respond(json.dumps(dict(version=1, requestId="finder", operation="find_steps",
                                        payload=request(source))).encode())
        self.assertEqual(len(reply["result"]["candidates"]), 2)
        for bad in (dict(request(source), pvTolerance=0), dict(request(source), mvTolerance=True)):
            with self.subTest(bad=bad), self.assertRaisesRegex(ValueError, "fit_tolerance"):
                find(bad)
        with self.assertRaisesRegex(ValueError, "fit_mapping"):
            find(dict(request(source), source=dict(source, pvColumn="mv")))


if __name__ == "__main__":
    unittest.main()
