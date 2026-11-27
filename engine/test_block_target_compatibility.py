"""LOT 0b, P0-7 : une compatibilité constructeur non établie ne doit jamais apparaître comme acquise.

Avant correction (audit LOT 0, annexe D) : un champ de cible vide était traité comme compatible ; une conversion PID_Compact était rendue pour une cible
vide (ou pour une CPU d'un autre constructeur) sans aucun avertissement, et une cible V3.x acceptait des paramètres V2.4.
Critères fixés a priori : (1) cible vide ou incomplète => conversion rendue SEULEMENT avec `compatibility.status == "not_verified"`, le code
d'avertissement `hardware_compatibility_not_verified` et `readyToLoad` faux ; (2) cible déclarant un autre constructeur => refus ;
(3) version de bibliothèque de la cible différente de celle des paramètres => refus ; (4) la conversion numérique elle-même est inchangée.
"""
import unittest

from block_catalogue import convert

PID_COMPACT = dict(version="V2.4", kp=2.0, tiSeconds=30.0, tdSeconds=0.0, outputPercentPerMvUnit=1.0)
PIDFF = None


def request(name, target, parameters):
    return dict(name=name, target=target, parameters=parameters)


class TargetCompatibility(unittest.TestCase):
    def test_empty_target_is_converted_but_never_declared_compatible(self):
        result = convert(request("PID_Compact", {}, PID_COMPACT))
        self.assertTrue(result["converted"])
        self.assertEqual(result["compatibility"]["status"], "not_verified")
        self.assertIn("hardware_compatibility_not_verified", result["conversion"]["warnings"])
        self.assertFalse(result["conversion"]["readyToLoad"])

    def test_partial_target_lists_what_is_still_unverified(self):
        result = convert(request("PID_Compact", {"manufacturer": "Siemens"}, PID_COMPACT))
        if result["converted"]:
            self.assertEqual(result["compatibility"]["status"], "not_verified")
            self.assertIn("cpu", result["compatibility"]["unverifiedTargetFields"])
            self.assertIn("firmware", result["compatibility"]["unverifiedTargetFields"])

    def test_other_manufacturer_is_refused(self):
        result = convert(request("PID_Compact", {"manufacturer": "Schneider Electric"}, PID_COMPACT))
        self.assertFalse(result["converted"])

    def test_library_version_of_the_target_must_match_the_parameters(self):
        for target_version, parameter_version in (("V3.0", "V2.4"), ("V2.4", "V3.0")):
            with self.subTest(target=target_version, parameters=parameter_version):
                result = convert(request("PID_Compact", {"libraryVersion": target_version}, dict(PID_COMPACT, version=parameter_version)))
                self.assertFalse(result["converted"])

    def test_numerical_conversion_is_unchanged(self):
        parameters = convert(request("PID_Compact", {}, PID_COMPACT))["parameters"]
        self.assertAlmostEqual(parameters["Gain"], 2.0)
        self.assertAlmostEqual(parameters["Ti"], 30.0)


if __name__ == "__main__":
    unittest.main()
