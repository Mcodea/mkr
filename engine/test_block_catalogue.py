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
            self.assertTrue(all(f["source"].startswith(("CAPTURE-", "SIEMENS-")) for f in entry["facts"]))
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


class VendorRulesTests(unittest.TestCase):
    def sources(self):
        return {s["id"]: s for s in lookup({})["sources"]}

    def test_relayed_vendor_rules_are_flagged_as_not_read_at_the_source(self):
        sources = self.sources()
        relayed = [s for s in sources.values() if s["id"].startswith(("SE-FAQ-", "SIEMENS-"))]
        self.assertEqual({s["id"] for s in relayed}, {"SE-FAQ-FA410692", "SE-FAQ-FAQ000279181", "SE-FAQ-FAQ000256113",
                                                       "SIEMENS-PIDCOMPACT-DOC", "SIEMENS-FAQ-109481448"})
        for source in relayed:
            self.assertIn("moteur de recherche", source["scope"])         # evidence level stays visible
            self.assertIn("À relire à la source", source["scope"])
        self.assertIn("images", sources["SIEMENS-PIDCOMPACT-DOC"]["scope"])

    def test_sign_conventions_that_a_conversion_must_handle_are_recorded(self):
        pidff, = lookup({"name": "PIDFF"})["entries"]
        text = " ".join(f["claim"] for f in pidff["facts"])
        self.assertIn("kp négatif n’est donc pas accepté", text)
        self.assertIn("ovs_att", text)
        compact, = lookup({"name": "PID_Compact"})["entries"]
        claims = " ".join(f["claim"] for f in compact["facts"])
        self.assertIn("gain proportionnel négatif n’est pas pris en charge", claims)
        self.assertIn("en secondes", claims)
        for entry in (pidff, compact):
            self.assertIsNone(entry["equation"])
            self.assertFalse(entry["selectable"])
