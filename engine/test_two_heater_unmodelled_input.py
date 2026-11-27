"""Couverture équivalente, SANS donnée réelle : un procédé thermique à deux chauffages couplés dont un seul est fourni au modèle.

DONNÉES SYNTHÉTIQUES : ce test remplace, dans le dépôt, la régression sur l'extrait APMonitor de TCLab (retiré le 2026-11-27 faute de droits de
redistribution établis : tests/reference/README-tclab.md). Il NE PRÉSENTE PAS de mesures expérimentales ; il ne couvre donc pas ce que la donnée
réelle couvrait (non-linéarités et dérives réelles du banc, bruit réel). Un test sur un jeu réel sous licence compatible est suivi séparément.

Structure reproduite : essai en boucle ouverte, deux chauffages H1/H2 (%), température T1 (°C), un point toutes les 5 s sur 600 s (121 points),
mêmes instants de commande que l'ancien extrait. Générateur INDÉPENDANT du moteur : deux retards du premier ordre exacts (récurrence discrète)
sommés à la température ambiante, bruit gaussien à graine fixe.

Critères posés par la théorie AVANT la première exécution (seule la graine et les gains ont été choisis ; aucune borne n'a été ajustée après
observation) :
  1. un échelon n'est exploitable que si la ligne de base est établie : avec des échelons espacés de moins de quelques constantes de temps,
     le chercheur d'essais ne propose aucun candidat et rejette chaque échelon pour ligne de base trop courte ou instable ;
  2. un modèle ARX ne recevant que H1 alors que H2 pilote aussi T1 ne peut pas être accepté sur un essai de validation indépendant
     (résidus non blancs ou erreur de simulation élevée), quelle que soit la limite d'erreur raisonnable ;
  3. contrôle indépendant du moteur : un modèle linéaire à entrée unique ajusté par moindres carrés sur le premier segment explique moins de
     40 % de la variance du segment retenu (la part de T1 due à H2 est de l'ordre de la moitié de celle de H1 sur ce segment).
"""
import base64
import csv
import io
import unittest
from datetime import datetime, timedelta, timezone

import numpy as np

import arx
import fopdt
import step_finder

SEED = 20261127
TS = 5.0
AMBIENT = 19.3
K1, TAU1 = 0.40, 120.0           # °C / %, s : chauffage 1 -> T1
K2, TAU2 = 0.22, 150.0           # °C / %, s : chauffage 2 -> T1 (couplage)
NOISE = 0.03                     # °C


def commands():
    """Mêmes instants de commande que l'ancien extrait : H1 0->80 (10 s), 80->20 (200 s), 20->70 (280 s), 70->50 (400 s) ; H2 0->100 (120 s), 100->10 (320 s), 10->80 (520 s)."""
    h1, h2 = np.zeros(121), np.zeros(121)
    for k in range(121):
        t = k * TS
        h1[k] = 0 if t < 10 else 80 if t < 200 else 20 if t < 280 else 70 if t < 400 else 50
        h2[k] = 0 if t < 120 else 100 if t < 320 else 10 if t < 520 else 80
    return h1, h2


def generate():
    rng = np.random.default_rng(SEED)
    h1, h2 = commands()
    a1, a2 = np.exp(-TS / TAU1), np.exp(-TS / TAU2)
    x1 = x2 = 0.0
    y = np.zeros(121)
    for k in range(121):
        y[k] = AMBIENT + x1 + x2 + rng.normal(0, NOISE)
        x1 = a1 * x1 + K1 * (1 - a1) * h1[k]
        x2 = a2 * x2 + K2 * (1 - a2) * h2[k]
    return h1, h2, y


def csv_bytes():
    h1, h2, y = generate()
    origin = datetime(2000, 1, 1, tzinfo=timezone.utc)
    out = io.StringIO()
    writer = csv.writer(out)
    writer.writerow(["timestamp", "H1", "H2", "T1"])
    for k in range(121):
        writer.writerow([(origin + timedelta(seconds=k * TS)).isoformat(), f"{h1[k]:g}", f"{h2[k]:g}", f"{y[k]:.4f}"])
    return out.getvalue().encode()


def source(first=None, last=None):
    result = dict(raw=base64.b64encode(csv_bytes()).decode(), delimiter=",", timeColumn="timestamp", mvColumn="H1", pvColumn="T1")
    if first is not None:
        result.update(rowStart=first, rowEnd=last)
    return result


class TwoHeaterUnmodelledInput(unittest.TestCase):
    def test_the_generator_is_reproducible_and_has_the_published_shape(self):
        self.assertEqual(csv_bytes(), csv_bytes())
        rows = list(csv.DictReader(csv_bytes().decode().splitlines()))
        self.assertEqual(len(rows), 121)
        self.assertEqual([r["H1"] for r in rows[:3]], ["0", "0", "80"])
        self.assertEqual(rows[24]["H2"], "100")

    def test_no_step_has_an_established_baseline_and_the_finder_says_so(self):
        found = step_finder.find(dict(source=source(), mvTolerance=0.5, pvTolerance=0.2))
        self.assertEqual(found["candidates"], [])
        self.assertEqual({r["reason"] for r in found["rejected"]}, {"baseline_too_short_or_unstable"})

    def test_arx_with_only_h1_refuses_every_train_validation_split(self):
        splits = {"0-320 / 320-520": ((0, 64), (64, 104)), "120-320 / 320-520": ((24, 64), (64, 104)),
                  "swapped": ((64, 104), (24, 64)), "0-320 / 320-600": ((0, 64), (64, 120))}
        for name, (train, valid) in splits.items():
            for limit in (0.5, 1.0, 2.0):
                with self.subTest(split=name, limit=limit):
                    result = arx.identify(dict(training=source(*train), validation=source(*valid), mvTolerance=0.01, maximumRmse=limit, loop="open"))
                    self.assertEqual(result["decision"], "refused")
                    self.assertIsNone(result["selected"])
                    self.assertTrue({"validation_residuals_not_white", "validation_simulation_rmse"} & set(result["reasons"]))

    def test_independent_check_a_single_input_linear_model_does_not_explain_the_held_out_segment(self):
        h1, _, y = generate()
        best = None
        for d in (1, 2, 3):
            design = np.array([[y[k - 1], h1[k - d], 1.0] for k in range(max(1, d), 64)])
            a, b, c = np.linalg.lstsq(design, y[max(1, d):64], rcond=None)[0]
            simulated = [y[64]]
            for k in range(65, 104):
                simulated.append(a * simulated[-1] + b * h1[k - d] + c)
            rmse = float(np.sqrt(np.mean((np.array(simulated) - y[64:104]) ** 2)))
            best = rmse if best is None else min(best, rmse)
        self.assertLess(1 - best ** 2 / float(np.var(y[64:104])), 0.4)

    def test_a_recording_without_an_observed_input_step_is_refused(self):
        """Équivalent synthétique du contrôle sur la montée en température à chauffage constant (jeu externe non redistribué) : la sortie varie, l'entrée non."""
        origin = datetime(2000, 1, 1, tzinfo=timezone.utc)
        out = io.StringIO()
        writer = csv.writer(out)
        writer.writerow(["timestamp", "Q1", "T1"])
        rng = np.random.default_rng(SEED + 1)
        for k in range(800):
            writer.writerow([(origin + timedelta(seconds=k)).isoformat(), "50", f"{20 + 30 * (1 - np.exp(-k / 150)) + rng.normal(0, 0.02):.4f}"])
        source_ = dict(raw=base64.b64encode(out.getvalue().encode()).decode(), delimiter=",", timeColumn="timestamp", mvColumn="Q1", pvColumn="T1")
        with self.assertRaises(ValueError) as caught:
            fopdt.read_step(source_, input_tolerance=0.01, output_tolerance=0.01)
        self.assertEqual(caught.exception.args, ("fit_step",))


if __name__ == "__main__":
    unittest.main()
