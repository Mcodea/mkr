import base64
import json
import unittest
from datetime import datetime, timedelta, timezone

from mkr_engine import respond


def request(**changes):
    start = datetime(2026, 1, 1, tzinfo=timezone.utc)
    lines = ["timestamp;mv;pv"]
    for i in range(12):
        when = (start + timedelta(seconds=i * 0.25)).isoformat()
        lines.append(f"{when};{0 if i < 4 else 2};{1 + i / 10}")
    source = "\n".join(lines)
    payload = dict(raw=base64.b64encode(source.encode()).decode(),
                   delimiter=";", timeColumn="timestamp", mvColumn="mv", pvColumn="pv")
    payload.update(changes)
    return dict(version=1, requestId="trial-test", operation="preview_trial", payload=payload)


def call(**changes):
    return respond(json.dumps(request(**changes)).encode())


class TrialPreviewTests(unittest.TestCase):
    def test_raw_signals_and_absolute_utc_origin(self):
        result = call()["result"]
        self.assertEqual(result["sourceRows"], 12)
        self.assertEqual(result["displayedRows"], 12)
        self.assertFalse(result["displayDecimated"])
        self.assertEqual(result["points"][4], dict(rowIndex=4, seconds=1.0, mv=2.0, pv=1.4))
        self.assertTrue(result["firstUtc"].startswith("2026-01-01T00:00:00"))
        self.assertAlmostEqual(result["points"][-1]["seconds"], 2.75)

    def test_decimation_keeps_original_rows_not_interpolated(self):
        source = "\n".join(
            ["timestamp,mv,pv"] +
            [f"2026-01-01T00:00:{i // 100:02d}.{i % 100:02d}Z,{i},{-i}"
             for i in range(1000)]
        )
        result = call(raw=base64.b64encode(source.encode()).decode(), delimiter=",")["result"]
        self.assertTrue(result["displayDecimated"])
        self.assertEqual(result["sourceRows"], 1000)
        self.assertLessEqual(result["displayedRows"], 600)
        self.assertGreater(result["displayedRows"], 1)
        self.assertEqual(result["points"][0]["rowIndex"], 0)
        self.assertEqual(result["points"][-1]["rowIndex"], 999)
        for point in result["points"]:
            self.assertEqual(point["mv"], point["rowIndex"])
            self.assertEqual(point["pv"], -point["rowIndex"])

    def test_single_sample_spike_survives_reduction(self):
        # Old uniform-index preview could miss isolated anomalies.
        from trial_preview import display_indices
        mv = [0.0] * 10000
        pv = [3.0] * 10000
        mv[4321] = 120.0
        pv[7654] = -90.0
        selected = display_indices(mv, pv)
        self.assertIn(4321, selected)
        self.assertIn(7654, selected)
        self.assertEqual(selected[0], 0)
        self.assertEqual(selected[-1], 9999)
        self.assertLessEqual(len(selected), 600)
        self.assertEqual(selected, sorted(set(selected)))

    def test_step_boundary_and_signed_extremes_survive(self):
        from trial_preview import display_indices
        mv = [0.0] * 5000 + [10.0] * 5000
        pv = [0.0] * 10000
        pv[147] = -12.0
        pv[148] = 16.0
        selected = display_indices(mv, pv)
        self.assertIn(4999, selected)
        self.assertIn(5000, selected)
        self.assertIn(147, selected)
        self.assertIn(148, selected)
        self.assertLessEqual(len(selected), 600)

    def test_preserves_source_order_and_returns_full_small_series(self):
        from trial_preview import display_indices
        mv = [float(i % 17) for i in range(512)]
        pv = [-value for value in mv]
        self.assertEqual(display_indices(mv, pv), list(range(512)))
        selected = display_indices(mv * 20, pv * 20)
        self.assertEqual(selected, sorted(set(selected)))
        self.assertTrue(all(0 <= idx < 10240 for idx in selected))

    def test_mapping_and_malformed_source_rejected(self):
        self.assertEqual(call(pvColumn="mv")["error"]["code"], "preview_mapping")
        self.assertEqual(call(timeColumn="absent")["error"]["code"], "preview_mapping")
        source = "timestamp;mv;pv\n2026-01-01T00:00:01Z;1;2\n2026-01-01T00:00:00Z;2;3"
        bad = base64.b64encode(source.encode()).decode()
        self.assertEqual(call(raw=bad)["error"]["code"], "preview_time")
        valid_source = "timestamp;mv;pv\n2026-01-01T00:00:00Z;1;2\n2026-01-01T00:00:01Z;2;inf"
        malformed = base64.b64encode(valid_source.encode()).decode()
        self.assertEqual(call(raw=malformed)["error"]["code"], "preview_data")


if __name__ == "__main__":
    unittest.main()
