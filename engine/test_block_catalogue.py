import unittest
from block_catalogue import convert, lookup


class CatalogueTests(unittest.TestCase):
    def test_unknown_target_is_visible_but_blocked(self):
        entries = lookup({"cpu": "M580"})["entries"]
        self.assertEqual({e["name"] for e in entries}, {"PI_B", "PIDFF", "PIFINT"})
        self.assertTrue(all(not e["selectable"] and "cpu" in e["unverifiedTargetFields"] for e in entries))
        self.assertEqual(lookup({"manufacturer": "Siemens"})["entries"], [])

    def test_conversion_refused_and_document_edition_not_used_as_software_version(self):
        pid = lookup({"name": "PIDFF", "softwareVersion": "16"})["entries"][0]
        self.assertIsNone(pid["softwareVersion"])
        result = convert(dict(name="PIDFF", target={"cpu": "M340"}, parameters={"kp": 3}))
        self.assertFalse(result["converted"])
        self.assertIsNone(result["parameters"])
