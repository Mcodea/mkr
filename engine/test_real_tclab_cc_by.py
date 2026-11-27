"""Régression sur des MESURES RÉELLES TCLab sous licence CC BY 4.0 (Goubej, Hedengren, Sukovatý, Zenodo 10.5281/zenodo.24286966).

Source, licence, modifications et limites : tests/reference/tclab_cc_by/README.md et ATTRIBUTION.txt. Données réelles (kit TCLab, boucle ouverte, 1 s) ;
elles ne sont PAS synthétiques.

Honnêteté sur le statut des assertions :
  * faits contrôlés (empreinte, forme, instants de commande) : établis AVANT toute exécution à partir du fichier et de sa documentation ;
  * comportement du moteur : RÉSULTAT OBSERVÉ enregistré comme régression, NON critère d'acceptation posé a priori ; il ne prouve pas que ce
    comportement est optimal. Observé le 2026-11-27 : l'unique échelon de H1 (ligne 19) n'a que 19 s de ligne de base et n'est pas proposé
    automatiquement ; une fenêtre choisie explicitement est lue.
Limites de couverture : un seul essai, deux échelons montants (un par chauffage, H2 couple T1 après 300 s), températures quantifiées par pas
d'environ 0,49 °C, pas d'échelon descendant ni d'essai de validation indépendant : AUCUNE identification validée ne peut en être déduite.
"""
import base64
import csv
import hashlib
import unittest
from pathlib import Path

import fopdt
import step_finder

DATA = Path(__file__).resolve().parents[1] / "tests" / "reference" / "tclab_cc_by" / "tclab_takehome_siso_openloop_extract.csv"
EXPECTED_SHA256 = "eb24ddbd5b2f0a3c613c90cfb7941c4a6682d8ab15855698976cab5ecd101ad6"


def source(first=None, last=None):
    result = dict(raw=base64.b64encode(DATA.read_bytes()).decode(), delimiter=",", timeColumn="timestamp", mvColumn="H1", pvColumn="T1")
    if first is not None:
        result.update(rowStart=first, rowEnd=last)
    return result


class RealTclabCcBy(unittest.TestCase):
    def test_the_extract_is_the_documented_file(self):
        self.assertEqual(hashlib.sha256(DATA.read_bytes()).hexdigest(), EXPECTED_SHA256)
        rows = list(csv.DictReader(DATA.read_text(encoding="utf-8").splitlines()))
        self.assertEqual(len(rows), 1000)
        self.assertEqual([r["H1"] for r in rows[:19]], ["0"] * 19)          # H1 = 0 % until 19 s
        self.assertEqual({r["H1"] for r in rows[19:]}, {"70"})              # H1 = 70 % from 20 s
        self.assertEqual({r["H2"] for r in rows[:299]}, {"0"})              # H2 = 0 % until 299 s
        self.assertEqual({r["H2"] for r in rows[299:]}, {"30"})             # H2 = 30 % from 300 s
        self.assertTrue(float(rows[999]["T1"]) > float(rows[0]["T1"]) + 50)  # the plant really heats up

    def test_observed_the_single_h1_step_is_not_proposed_for_lack_of_baseline(self):
        found = step_finder.find(dict(source=source(), mvTolerance=0.5, pvTolerance=0.6))
        self.assertEqual(found["candidates"], [])
        self.assertEqual([(r["stepRow"], r["reason"]) for r in found["rejected"]], [(19, "baseline_too_short_or_unstable")])

    def test_observed_an_explicit_window_before_the_coupling_is_readable(self):
        step = fopdt.read_step(source(0, 299), input_tolerance=0.5, output_tolerance=0.6)
        self.assertIsNotNone(step)


if __name__ == "__main__":
    unittest.main()
