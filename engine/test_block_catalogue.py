import unittest
from block_catalogue import convert, lookup


class CatalogueTests(unittest.TestCase):
    def test_unknown_target_is_visible_but_blocked(self):
        entries = lookup({"cpu": "M580"})["entries"]
        self.assertEqual({e["name"] for e in entries}, {"PI_B", "PIDFF", "PIFINT", "PIDINT", "PID_Compact"})
        self.assertTrue(all(not e["selectable"] and "cpu" in e["unverifiedTargetFields"] for e in entries))
        # Siemens PID_Compact and Schneider PIDINT are listed with no equation, parameter or fact: nothing is claimed
        # about them until the official documentation is read (AGENTS.md §7).
        siemens = lookup({"manufacturer": "Siemens"})["entries"]
        self.assertEqual([e["name"] for e in siemens], ["PID_Compact"])
        for name in ("PID_Compact", "PIDINT"):
            entry, = lookup({"name": name})["entries"]
            self.assertEqual((entry["equation"], entry["parameters"], entry["status"]), (None, [], "to_verify"))
            # Only pin names transcribed from user captures: no claim about behaviour, units or equations.
            self.assertTrue(all(f["source"].startswith("CAPTURE-") for f in entry["facts"]))
            self.assertFalse(entry["selectable"])
            self.assertFalse(convert(dict(name=name, target={}, parameters={"kp": 1}))["converted"])

    def test_conversion_refused_and_document_edition_not_used_as_software_version(self):
        pid = lookup({"name": "PIDFF", "softwareVersion": "16"})["entries"][0]
        self.assertIsNone(pid["softwareVersion"])
        result = convert(dict(name="PIDFF", target={"cpu": "M340"}, parameters={"kp": 3}))
        self.assertFalse(result["converted"])
        self.assertIsNone(result["parameters"])

    def test_recommendation_keeps_provenance_and_specific_missing_functions(self):
        from block_catalogue import recommend
        result = recommend(dict(target={"cpu": "M340"}, strategy="PI", functions=["tracking"],
                                studyId="S1", modelRunId="M1"))
        self.assertEqual(result["decision"], "insufficient_evidence")
        self.assertEqual(result["eligible"], [])
        self.assertEqual(result["modelRunId"], "M1")
        self.assertTrue(all("tracking" in item["missingFunctions"] for item in result["evaluated"]))

    def test_pidff_typed_parameters_remain_documentary(self):
        result = lookup({"name": "PIDFF"})
        entry, = result["entries"]
        params = {p["name"]: p for p in entry["parameters"]}
        self.assertEqual({k: p["iecType"] for k, p in params.items()}, {
            "kp": "REAL", "ti": "TIME", "td": "TIME",
            "mix_par": "BOOL", "rev_dir": "BOOL", "en_rcpy": "BOOL",
        })
        references = {s["id"] for s in result["sources"]}
        self.assertTrue(all(p["source"] in references for p in params.values()))
        self.assertFalse(entry["selectable"])


class ObservedStructureTests(unittest.TestCase):
    def test_captured_structure_is_traceable_and_claims_no_equation(self):
        result = lookup({"name": "PIDFF"})
        sources = {s["id"]: s for s in result["sources"]}
        entry, = result["entries"]
        captured = [f for f in entry["facts"] if f["source"].startswith("CAPTURE-")]
        self.assertEqual(len(captured), 6)
        for fact in captured:
            source = sources[fact["source"]]
            self.assertEqual(source["edition"], "non identifiée")        # provenance gap stays visible
            self.assertIn("Aucune équation" if fact["source"] == "CAPTURE-PIDFF-1" else "non établis", source["scope"])
        # The structure names the functions a conversion must neutralise before any equivalence test.
        text = " ".join(f["claim"] for f in captured)
        for name in ("ovs_att", "dband", "gain_kp", "pv_dev", "outbias", "outrate", "rev_dir", "TR_I", "MAN_AUTO"):
            self.assertIn(name, text)
        self.assertIsNone(entry["equation"])
        self.assertFalse(entry["selectable"])
