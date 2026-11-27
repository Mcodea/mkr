"""Audit P0-6, protection côté moteur : avec une colonne MANUAL (écrite par l'acquisition MKR), l'identification en boucle ouverte refuse une
fenêtre qui contient une ligne hors mode manuel. Sans la colonne, comportement inchangé (la limite P0-6 subsiste pour ces données, documentée).

Oracle indépendant : le résultat d'une étude dont toutes les lignes sont manuelles est IDENTIQUE à celui du même fichier sans colonne (le contrôle ne
modifie aucun calcul) ; le refus est exigé par construction du fichier (une ligne MANUAL = 0), pas par un calcul du moteur."""
import base64
import json
import unittest

import numpy as np

import arx
import broida
import fopdt
import integrator
import sopdt
from mkr_engine import respond
from test_arx import make as make_arx, prbs, first_order_zoh
from test_fopdt import source


def with_mode(src, flags, name="MANUAL"):
    """Ajoute une colonne de mode au CSV d'un essai ; `flags` : une valeur (texte) par ligne de données."""
    lines = base64.b64decode(src["raw"]).decode().splitlines()
    assert len(lines) - 1 == len(flags)
    out = [lines[0] + "," + name] + [f"{line},{flag}" for line, flag in zip(lines[1:], flags)]
    return src | {"raw": base64.b64encode("\n".join(out).encode()).decode()}


def request(training, validation):
    return dict(training=training, validation=validation, mvTolerance=0.01, pvTolerance=0.01)


N = 70
ALL = ["1"] * N


class StepIdentificationFamilies(unittest.TestCase):
    def test_all_manual_gives_exactly_the_same_result_as_no_column(self):
        plain = fopdt.identify(request(source(1, cadence=0.5), source(2, amplitude=-1.5, cadence=0.75)))
        marked = fopdt.identify(request(with_mode(source(1, cadence=0.5), ALL), with_mode(source(2, amplitude=-1.5, cadence=0.75), ALL)))
        for field in ("gain", "tauSeconds", "delaySeconds", "standardErrors", "warnings"):
            self.assertEqual(plain[field], marked[field])

    def test_one_row_outside_manual_is_refused_by_every_step_family(self):
        bad = ALL[:]
        bad[40] = "0"
        for module in (fopdt, sopdt, integrator, broida):
            with self.subTest(module=module.__name__), self.assertRaisesRegex(ValueError, "fit_not_manual"):
                module.identify(request(with_mode(source(1), bad), source(2, amplitude=-1.5)))

    def test_the_validation_trial_is_checked_too(self):
        bad = ALL[:]
        bad[10] = "0"
        with self.assertRaisesRegex(ValueError, "fit_not_manual"):
            fopdt.identify(request(source(1), with_mode(source(2, amplitude=-1.5), bad)))

    def test_missing_or_unreadable_mode_is_not_manual(self):
        for bad_value in ("", "x", "nan", "2", "0.5"):
            flags = ALL[:]
            flags[3] = bad_value
            with self.subTest(value=bad_value), self.assertRaisesRegex(ValueError, "fit_not_manual"):
                fopdt.identify(request(with_mode(source(1), flags), source(2, amplitude=-1.5)))

    def test_only_rows_inside_the_declared_window_count(self):
        flags = ALL[:]
        flags[0] = "0"                                       # hors fenêtre [1, 69] : n'est pas lu
        window = with_mode(source(1), flags) | dict(rowStart=1, rowEnd=69)
        result = fopdt.identify(request(window, source(2, amplitude=-1.5)))
        self.assertAlmostEqual(result["gain"], -3, places=5)
        inside = with_mode(source(1), flags) | dict(rowStart=0, rowEnd=69)
        with self.assertRaisesRegex(ValueError, "fit_not_manual"):
            fopdt.identify(request(inside, source(2, amplitude=-1.5)))

    def test_a_column_that_is_not_named_exactly_manual_is_ignored(self):
        flags = ["0"] * N
        result = fopdt.identify(request(with_mode(source(1), flags, name="manual"), source(2, amplitude=-1.5)))
        self.assertAlmostEqual(result["gain"], -3, places=5)

    def test_ipc_error_code(self):
        bad = ALL[:]
        bad[40] = "0"
        message = dict(version=1, requestId="m", operation="identify_fopdt", payload=request(with_mode(source(1), bad), source(2, amplitude=-1.5)))
        self.assertEqual(respond(json.dumps(message).encode())["error"]["code"], "fit_not_manual")


class ArxIdentification(unittest.TestCase):
    def data(self):
        ts = 2.0
        rng = np.random.default_rng(3)
        u, v = prbs(300, rng), prbs(260, rng)
        y = first_order_zoh(u, 20.0, 1.5, 3, ts, 0.0, 50.0, 0.02, rng)
        w = first_order_zoh(v, 20.0, 1.5, 3, ts, 0.0, 50.0, 0.02, rng)
        return make_arx(1, u, y, ts), make_arx(2, v, w, ts)

    def payload(self, train, valid):
        return dict(training=train, validation=valid, mvTolerance=1e-6, maximumRmse=0.2, loop="open")

    def test_all_manual_is_accepted_and_one_non_manual_row_is_refused(self):
        train, valid = self.data()
        n_train = len(base64.b64decode(train["raw"]).decode().splitlines()) - 1
        n_valid = len(base64.b64decode(valid["raw"]).decode().splitlines()) - 1
        accepted = arx.identify(self.payload(with_mode(train, ["1"] * n_train), with_mode(valid, ["1"] * n_valid)))
        self.assertIn("decision", accepted)
        flags = ["1"] * n_train
        flags[100] = "0"
        with self.assertRaisesRegex(ValueError, "arx_not_manual"):
            arx.identify(self.payload(with_mode(train, flags), valid))


if __name__ == "__main__":
    unittest.main()
