"""Regression tests: row-index windows are inclusive and provenance is immutable."""
import base64
from datetime import datetime, timedelta, timezone
import unittest

from fopdt import identify
from test_fopdt import source


def append_second_step(trial):
    raw = base64.b64decode(trial["raw"]).decode()
    header, *rows = raw.splitlines()
    start = datetime.fromisoformat(rows[0].split(",")[0])
    for index in range(70, 90):
        stamp = (start + timedelta(seconds=index)).isoformat()
        rows.append(f"{stamp},100,123")
    return trial | {"raw": base64.b64encode(
        ("\n".join([header] + rows)).encode()).decode()}


def fit(training, validation, start=0, end=69):
    return identify(dict(
        training=training | {"rowStart": start, "rowEnd": end},
        validation=validation | {"rowStart": start, "rowEnd": end},
        mvTolerance=0.01, pvTolerance=0.01))


class WindowedIdentificationTests(unittest.TestCase):
    def test_window_ignores_later_second_step_without_changing_source(self):
        training = append_second_step(source(1))
        validation = append_second_step(source(2, amplitude=-1.5))
        original = dict(training)
        with self.assertRaisesRegex(ValueError, "fit_step"):
            identify(dict(training=training, validation=validation,
                          mvTolerance=0.01, pvTolerance=0.01))

        result = fit(training, validation)
        self.assertAlmostEqual(result["gain"], -3, places=4)
        self.assertAlmostEqual(result["tauSeconds"], 4, places=4)
        self.assertAlmostEqual(result["delaySeconds"], 2.5, places=4)
        self.assertLess(result["validation"]["rmse"], 1e-6)
        self.assertEqual(training, original)

    def test_training_selection_does_not_depend_on_validation_values(self):
        training = append_second_step(source(1))
        a = fit(training, append_second_step(source(2, amplitude=-1.5)))
        b = fit(training, append_second_step(source(2, gain=-0.4, amplitude=-1.5)))
        for field in ("gain", "tauSeconds", "delaySeconds"):
            self.assertEqual(a[field], b[field])
        self.assertGreater(b["validation"]["rmse"], a["validation"]["rmse"] + 0.1)

    def test_rejects_bad_window_boundaries_and_insufficient_baseline(self):
        for first, last in ((-1, 69), (12, 12), (70, 70), (0, 200),
                            (True, 69), (0, 69.5), (12, 69)):
            with self.subTest(first=first, last=last):
                with self.assertRaisesRegex(ValueError, "fit_window|fit_step"):
                    fit(source(1), source(2, amplitude=-1.5), first, last)

    def test_selected_source_rows_are_inclusive(self):
        result = fit(source(1), source(2, amplitude=-1.5), 1, 65)
        self.assertEqual(result["training"]["samples"], 60)  # 65-6+1
        self.assertEqual(result["validation"]["samples"], 60)

    def test_one_sided_window_declaration_rejected(self):
        original = source(1) | {"rowStart": 0}
        with self.assertRaisesRegex(ValueError, "fit_mapping"):
            identify(dict(training=original, validation=source(2, amplitude=-1.5),
                          mvTolerance=0.01, pvTolerance=0.01))


if __name__ == "__main__":
    unittest.main()
