"""PID_Compact V2.x / V3.x conversion (Siemens A5E35300227-AJ p. 88, 91, 92, 246-247, 270). Documentary, never qualified."""
import cmath
import unittest

from block_adapters import pid_compact, pid_compact_versions
from block_catalogue import convert, lookup

BASE = dict(version="V2.4", kp=2.0, tiSeconds=15.0, tdSeconds=3.0, filterSeconds=0.6, outputPercentPerMvUnit=1.0)


class Conversion(unittest.TestCase):
    def test_transfer_function_equals_mkr_ideal_pid_on_the_measurement(self):
        values = pid_compact(BASE)["parameters"]
        for p in (0.03j, 0.5j, 3j, 0.2 + 1.3j):
            # Manual (p. 88) with b = PWeighting, c = DWeighting acting on the feedback: y/(-x) from the measurement side.
            manual = values["Gain"] * (1 + 1 / (values["Ti"] * p) + values["Td"] * p / (values["TdFiltRatio"] * values["Td"] * p + 1))
            mkr = 2.0 * (1 + 1 / (15.0 * p) + 3.0 * p / (0.6 * p + 1))
            self.assertAlmostEqual(abs(manual - mkr), 0.0, places=12)

    def test_setpoint_path_matches_mkr_proportional_on_error_and_derivative_on_measurement(self):
        v = pid_compact(BASE)["parameters"]
        self.assertEqual((v["PWeighting"], v["DWeighting"]), (1.0, 0.0))     # b = 1 (error), c = 0 (derivative on the measurement)
        for p in (0.1j, 1j):
            from_setpoint = v["Gain"] * (v["PWeighting"] + 1 / (v["Ti"] * p) + v["DWeighting"] * v["Td"] * p / (v["TdFiltRatio"] * v["Td"] * p + 1))
            self.assertAlmostEqual(abs(from_setpoint - 2.0 * (1 + 1 / (15.0 * p))), 0.0, places=12)

    def test_negative_gain_uses_the_inversion_flag_and_percent_scaling(self):
        v = pid_compact(dict(BASE, kp=-0.5, outputPercentPerMvUnit=2.0))["parameters"]
        self.assertEqual((v["Gain"], v["InvertControl"]), (1.0, True))
        self.assertFalse(pid_compact(BASE)["parameters"]["InvertControl"])

    def test_versions_differ_by_the_dead_zone_only(self):
        v2, v3 = pid_compact(BASE), pid_compact(dict(BASE, version="V3.0"))
        self.assertNotIn("DeadZone", v2["parameters"])
        self.assertEqual(v3["parameters"]["DeadZone"], 0.0)
        self.assertIn("dead_zone_set_to_zero", v3["warnings"])
        self.assertEqual({k: v for k, v in v3["parameters"].items() if k != "DeadZone"}, v2["parameters"])
        for result in (v2, v3):
            self.assertEqual((result["qualification"], result["readyToLoad"]), ("non_qualified", False))


class Compatibility(unittest.TestCase):
    def test_table_from_the_manual(self):
        self.assertEqual(pid_compact_versions("S7-1500", "3.1"), ["V2.4", "V3.0"])
        self.assertEqual(pid_compact_versions("S7-1500", "V3.0"), ["V2.4"])
        self.assertEqual(pid_compact_versions("S7-1500", "1.8"), ["V2.0", "V2.1", "V2.2"])
        self.assertEqual(pid_compact_versions("S7-1200", "4.0"), ["V2.2"])
        self.assertEqual(pid_compact_versions("S7-1200", "4.6"), ["V2.2", "V2.3"])
        self.assertEqual(pid_compact_versions("S7-1200", "3.0"), [])           # V1.x only: out of scope
        self.assertEqual(pid_compact_versions("S7-1200 G2", "1.0"), ["V3.0"])

    def test_incompatible_hardware_is_refused(self):
        for extra in (dict(version="V3.0", cpuFamily="S7-1500", firmware="3.0"),
                      dict(version="V2.4", cpuFamily="S7-1200 G2", firmware="1.0"),
                      dict(version="V2.3", cpuFamily="S7-1200", firmware="4.1")):
            with self.assertRaises(ValueError) as caught:
                pid_compact(dict(BASE, **extra))
            self.assertEqual(str(caught.exception), "conversion_hardware_incompatible")
        pid_compact(dict(BASE, version="V3.0", cpuFamily="S7-1200 G2", firmware="1.0"))      # accepted

    def test_family_without_firmware_is_malformed(self):
        with self.assertRaises(ValueError):
            pid_compact(dict(BASE, cpuFamily="S7-1500"))


class Refusals(unittest.TestCase):
    def test_what_the_documentation_does_not_settle_is_refused(self):
        cases = {
            "conversion_filter_required": [dict(BASE, filterSeconds=None), dict(BASE, filterSeconds=0)],
            "conversion_version": [dict(BASE, version="V1.2"), dict(BASE, version="V4.0"), dict(BASE, version="V2.x")],
            "conversion_parameters": [dict(BASE, tiSeconds=0), dict(BASE, kp=0), dict(BASE, outputPercentPerMvUnit=0)],
        }
        for reason, variants in cases.items():
            for parameters in variants:
                if parameters.get("filterSeconds", 1) is None:
                    del parameters["filterSeconds"]
                with self.subTest(reason=reason, parameters=parameters):
                    with self.assertRaises(ValueError) as caught:
                        pid_compact(parameters)
                    self.assertEqual(str(caught.exception), reason)

    def test_pi_needs_no_filter(self):
        v = pid_compact({**{k: x for k, x in BASE.items() if k != "filterSeconds"}, "tdSeconds": 0.0})
        self.assertEqual((v["parameters"]["Td"], v["parameters"]["TdFiltRatio"]), (0.0, 0.0))

    def test_through_the_catalogue_with_the_version_entries(self):
        result = convert(dict(name="PID_Compact", target={"libraryVersion": "V3.x"}, parameters=dict(BASE, version="V3.0")))
        self.assertTrue(result["converted"])
        entries = lookup({"name": "PID_Compact", "libraryVersion": "V3.x"})["entries"]
        v3 = next(e for e in entries if e["libraryVersion"] == "V3.x")
        self.assertTrue({"Gain", "Ti", "Td", "TdFiltRatio", "PWeighting", "DWeighting", "DeadZone", "InvertControl"} <= {p["name"] for p in v3["parameters"]})
        self.assertIn("1200 G2", " ".join(f["claim"] for f in v3["facts"]))
        self.assertFalse(v3["selectable"])


if __name__ == "__main__":
    unittest.main()
