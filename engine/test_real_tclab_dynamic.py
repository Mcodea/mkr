"""Real open-loop data (TCLab, two heaters, two temperature sensors; extract of 121 points every 5 s) against MKR's refusals.

Source and caveats: tests/reference/README-tclab.md. The observed outcome is recorded as a regression, NOT as proof that the refusal is
optimal. Honest account of what was fixed when: the validation limits 0.5 and 1.0 degC were chosen before the first run, 2.0 degC was
added afterwards to check that a looser limit does not change the decision; the 40 % bound of the independent check was set AFTER
observing about 20 % (it is a regression bound, not a pre-registered acceptance criterion).
"""
import base64
import csv
import unittest
from pathlib import Path

import numpy as np

import arx
import step_finder

DATA = Path(__file__).resolve().parents[1] / "tests" / "reference" / "tclab_dyn_data2_extract.csv"


def source(first=None, last=None):
    raw = DATA.read_bytes()
    result = dict(raw=base64.b64encode(raw).decode(), delimiter=",", timeColumn="timestamp", mvColumn="H1", pvColumn="T1")
    if first is not None:
        result.update(rowStart=first, rowEnd=last)
    return result


@unittest.skipUnless(DATA.exists(), "Données réelles TCLab retirées du dépôt le 2026-11-27 (licence de redistribution non établie, tests/reference/README-tclab.md) : "
                     "assertions conservées intactes pour un jeu réel autorisé ; couverture équivalente sans donnée réelle : test_two_heater_unmodelled_input.py")
class RealTclab(unittest.TestCase):
    def test_the_fixture_is_the_published_extract_shape(self):
        rows = list(csv.DictReader(DATA.read_text(encoding="utf-8").splitlines()))
        self.assertEqual(len(rows), 121)
        self.assertEqual([r["H1"] for r in rows[:3]], ["0", "0", "80"])             # H1 0 -> 80 % at t = 10 s
        self.assertEqual(rows[24]["H2"], "100")                                      # H2 0 -> 100 % at t = 120 s
        self.assertAlmostEqual(float(rows[0]["T1"]), 19.29)

    def test_no_step_of_the_recording_has_a_usable_baseline_and_the_finder_says_so(self):
        found = step_finder.find(dict(source=source(), mvTolerance=0.5, pvTolerance=0.2))
        self.assertEqual(found["candidates"], [])
        self.assertEqual({r["reason"] for r in found["rejected"]}, {"baseline_too_short_or_unstable"})
        self.assertEqual([r["stepRow"] for r in found["rejected"]], [2, 40, 56, 80])        # H1 changes at 10, 200, 280, 400 s

    def test_arx_refuses_every_train_validation_split_with_whiteness_or_error_reasons(self):
        splits = {"0-320 / 320-520": ((0, 64), (64, 104)), "120-320 / 320-520": ((24, 64), (64, 104)),
                  "swapped": ((64, 104), (24, 64)), "0-320 / 320-600": ((0, 64), (64, 120))}
        for name, (train, valid) in splits.items():
            for limit in (0.5, 1.0, 2.0):
                with self.subTest(split=name, limit=limit):
                    result = arx.identify(dict(training=source(*train), validation=source(*valid), mvTolerance=0.01, maximumRmse=limit, loop="open"))
                    self.assertEqual(result["decision"], "refused")
                    self.assertIsNone(result["selected"])
                    self.assertTrue({"validation_residuals_not_white", "validation_simulation_rmse"} & set(result["reasons"]))

    def test_independent_check_a_linear_single_input_model_does_not_explain_the_held_out_segment(self):
        """Independent of MKR: one-step least squares y[k] = a y[k-1] + b u[k-d] + c on 0-320 s, free-run simulation on 320-520 s.
        The best of d = 1..3 explains less than 40 % of the variance of the held-out temperature (about 20 % when written): the refusal is not an artefact of MKR."""
        rows = list(csv.DictReader(DATA.read_text(encoding="utf-8").splitlines()))
        u = np.array([float(r["H1"]) for r in rows])
        y = np.array([float(r["T1"]) for r in rows])
        best = None
        for d in (1, 2, 3):
            design = np.array([[y[k - 1], u[k - d], 1.0] for k in range(max(1, d), 64)])
            a, b, c = np.linalg.lstsq(design, y[max(1, d):64], rcond=None)[0]
            simulated = [y[64]]
            for k in range(65, 104):
                simulated.append(a * simulated[-1] + b * u[k - d] + c)
            rmse = float(np.sqrt(np.mean((np.array(simulated) - y[64:104]) ** 2)))
            best = rmse if best is None else min(best, rmse)
        variance_explained = 1 - best ** 2 / float(np.var(y[64:104]))
        self.assertLess(variance_explained, 0.4)


if __name__ == "__main__":
    unittest.main()
