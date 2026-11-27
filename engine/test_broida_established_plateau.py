"""Broïda : variation finale « suffisamment établie » (cahier directeur, section 42.2), passages à 28 % et 40 % calculés sans saisie.

Oracle indépendant : pour un FOPDT exact y = K A (1 - exp(-(t - L)/tau)), les instants de passage de la fraction f sont
t_f = L - tau ln(1 - f) (forme close, écrite ici sans appeler broida.py) et le reste à parcourir à l'instant t est |K A| exp(-(t - L)/tau), majoré sur la fenêtre de plateau par sa valeur au début de la fenêtre (6 échantillons, 2,5 s).
Critères fixés avant mesure : instants de passage à 0,02 s près (pas 0,5 s, interpolation linéaire) ; tau à 1 % (le biais intrinsèque de
l'estimateur à deux points sur un FOPDT exact est 0,28 % de tau, calculé : 5,5 ln(0,72/0,60) = 1,0028) ; L à 0,1 s (biais intrinsèque 0,0003 tau)."""
import base64
import json
import math
import unittest
from datetime import datetime, timedelta, timezone

from broida import identify
from mkr_engine import respond


def trial(day, gain, tau, delay, amplitude, duration, cadence=0.5, baseline=7.0):
    """Essai FOPDT exact : 6 échantillons avant l'échelon, puis `duration` secondes de réponse."""
    start = datetime(2026, 3, day, tzinfo=timezone.utc)
    lines = ["timestamp,mv,pv"]
    for i in range(6 + int(round(duration / cadence)) + 1):
        t = (i - 6) * cadence
        pv = baseline + gain * amplitude * (1 - math.exp(-max(t - delay, 0) / tau))
        lines.append(",".join([(start + timedelta(seconds=i * cadence)).isoformat(), str(amplitude if t >= 0 else 0), repr(pv)]))
    return dict(raw=base64.b64encode("\n".join(lines).encode()).decode(), delimiter=",", timeColumn="timestamp", mvColumn="mv", pvColumn="pv")


def fit(duration, gain=2.0, tau=10.0, delay=4.0, amplitude=1.5, tolerance=0.01):
    return identify(dict(training=trial(1, gain, tau, delay, amplitude, duration),
                         validation=trial(2, gain, tau, delay, -1.0, duration), mvTolerance=0.01, pvTolerance=tolerance))


class EstablishedPlateau(unittest.TestCase):
    gain, tau, delay, amplitude = 2.0, 10.0, 4.0, 1.5      # reponse = 3,0 ; bande de bruit 2 x 0,01 ; Delta / bande = 150

    def test_crossings_match_the_closed_form_and_are_recorded_for_the_chart(self):
        result = fit(70.0)
        e = result["broidaEvidence"]
        self.assertAlmostEqual(e["crossing28Seconds"], self.delay - self.tau * math.log(1 - 0.28), delta=0.02)
        self.assertAlmostEqual(e["crossing40Seconds"], self.delay - self.tau * math.log(1 - 0.40), delta=0.02)
        self.assertAlmostEqual(result["tauSeconds"], self.tau, delta=0.01 * self.tau)
        self.assertAlmostEqual(result["delaySeconds"], self.delay, delta=0.1)
        # biais du gain = reste moyen sur la fenêtre de plateau / amplitude ; borné par le reste au début de la fenêtre (67,5 s)
        self.assertAlmostEqual(result["gain"], self.gain, delta=3.0 * math.exp(-(67.5 - self.delay) / self.tau) / self.amplitude)
        # niveaux à tracer sur la courbe : variation finale 3,0 -> 28 % = 0,84 et 40 % = 1,2 au-dessus de la ligne de base 7
        self.assertAlmostEqual(e["baselinePv"], 7.0, places=9)
        bias = 3.0 * math.exp(-(67.5 - self.delay) / self.tau)       # majorant du biais du niveau final (reste au début de la fenêtre)
        self.assertAlmostEqual(e["crossing28Pv"], 7.0 + 0.28 * 3.0, delta=0.28 * bias)
        self.assertAlmostEqual(e["crossing40Pv"], 7.0 + 0.40 * 3.0, delta=0.40 * bias)
        self.assertLess(e["plateauWindowStartSeconds"], e["plateauWindowEndSeconds"])
        self.assertAlmostEqual(e["plateauWindowEndSeconds"], 70.0, places=9)

    def test_published_residual_and_required_duration_match_the_closed_form(self):
        e = fit(70.0)["broidaEvidence"]
        self.assertAlmostEqual(e["settledResidualBoundPv"], 3.0 * math.exp(-(67.5 - self.delay) / self.tau), delta=2e-4)
        # L + tau ln(|reponse| / (2 tol)) + fenêtre = 4 + 10 ln(150) + 2,5
        self.assertAlmostEqual(e["requiredDurationSeconds"], 4.0 + 10.0 * math.log(150) + 2.5, delta=0.5)
        self.assertGreaterEqual(e["observedDurationSeconds"], e["requiredDurationSeconds"])

    def test_plateau_not_yet_established_is_refused_where_the_fixed_4_tau_rule_accepted(self):
        # T = 45 s : T - L = 41 s > 4 tau, l'ancienne règle acceptait. Reste prévu : 3 exp(-4,1) = 0,0497 > bande 0,02 : refus.
        self.assertGreater(3.0 * math.exp(-(45.0 - self.delay) / self.tau), 0.02)
        with self.assertRaisesRegex(ValueError, "broida_not_established"):
            fit(45.0)

    def test_accepted_once_the_closed_form_residual_fits_the_noise_band(self):
        self.assertLess(3.0 * math.exp(-(58.0 - 2.5 - self.delay) / self.tau), 0.02)       # reste au début de la fenêtre de 58 s : 0,0173
        self.assertGreater(3.0 * math.exp(-(56.0 - 2.5 - self.delay) / self.tau), 0.02)    # à 56 s : 0,0212, encore refusé
        with self.assertRaisesRegex(ValueError, "broida_not_established"):
            fit(56.0)
        self.assertAlmostEqual(fit(58.0)["tauSeconds"], self.tau, delta=0.01 * self.tau)

    def test_threshold_follows_the_signal_to_noise_ratio_not_a_fixed_number_of_time_constants(self):
        # Même essai, bande de bruit 10 fois plus large (0,2) : 45 s suffisent (reste au début de fenêtre 3 exp(-3,85) = 0,064 <= 0,2).
        # Le niveau final peut alors être biaisé jusqu'à 2,1 % de la réponse : la précision attendue sur tau est de l'ordre de 5 %, pas 1 %
        # (critère fixé d'après ce biais toléré, deux fois sa fraction).
        self.assertLessEqual(3.0 * math.exp(-(45.0 - 2.5 - self.delay) / self.tau), 0.2)
        self.assertAlmostEqual(fit(45.0, tolerance=0.1)["tauSeconds"], self.tau, delta=0.05 * self.tau)

    def test_legacy_floor_still_applies_for_a_large_noise_band(self):
        with self.assertRaisesRegex(ValueError, "broida_horizon"):   # T - L = 28 s < 4 tau : plancher conservé de la v1.1
            fit(32.0, tolerance=0.1)

    def test_ipc_reports_the_new_refusal_code(self):
        request = dict(operation="identify_broida", version=1, requestId="b",
                       payload=dict(training=trial(1, 2.0, 10.0, 4.0, 1.5, 45.0), validation=trial(2, 2.0, 10.0, 4.0, -1.0, 45.0),
                                    mvTolerance=0.01, pvTolerance=0.01))
        response = respond(json.dumps(request).encode())
        self.assertEqual(response["error"]["code"], "broida_not_established")


if __name__ == "__main__":
    unittest.main()
