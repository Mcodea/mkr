import unittest
from block_catalogue import convert, lookup


class CatalogueTests(unittest.TestCase):
    def test_unknown_target_is_visible_but_blocked(self):
        entries = lookup({"cpu": "M580"})["entries"]
        self.assertEqual({e["name"] for e in entries}, {"PI_B", "PIDFF", "PIFINT", "PID_INT", "PID_Compact"})
        self.assertTrue(all(not e["selectable"] and "cpu" in e["unverifiedTargetFields"] for e in entries))
        # Siemens PID_Compact is listed with no equation, parameter or fact: nothing is claimed
        # about them until the official documentation is read (AGENTS.md §7).
        siemens = lookup({"manufacturer": "Siemens"})["entries"]
        self.assertEqual([e["name"] for e in siemens], ["PID_Compact"] * 3)
        for name in ("PID_Compact",):
            entries = lookup({"name": name})["entries"]
            for entry in entries:
                self.assertEqual((entry["equation"], entry["parameters"], entry["status"]), (None, [], "to_verify"))
                self.assertFalse(entry["selectable"])
        self.assertFalse(convert(dict(name="PID_Compact", target={"libraryVersion": "V3.x"}, parameters={"kp": 1}))["converted"])

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
        # The structure was first transcribed from captures; the equation is now read from the official document
        # (see PidffOfficialDocumentTests). Selection stays blocked: no CPU, firmware or software version.
        self.assertIsNotNone(entry["equation"])
        self.assertFalse(entry["selectable"])


class VendorRulesTests(unittest.TestCase):
    def sources(self):
        return {s["id"]: s for s in lookup({})["sources"]}

    def test_relayed_vendor_rules_are_flagged_as_not_read_at_the_source(self):
        sources = self.sources()
        relayed = [s for s in sources.values() if s["id"].startswith(("SE-FAQ-", "SIEMENS-"))]
        self.assertEqual({s["id"] for s in relayed}, {"SE-FAQ-FA410692", "SE-FAQ-FAQ000279181", "SE-FAQ-FAQ000256113",
                                                       "SIEMENS-PIDCOMPACT-DOC", "SIEMENS-FAQ-109481448",
                                                       "SIEMENS-PIDCOMPACT-V3", "SIEMENS-PIDCOMPACT-V22"})
        for source in relayed:
            self.assertIn("moteur de recherche", source["scope"])         # evidence level stays visible
            self.assertIn("À relire à la source", source["scope"])
        self.assertIn("images", sources["SIEMENS-PIDCOMPACT-DOC"]["scope"])

    def test_sign_conventions_that_a_conversion_must_handle_are_recorded(self):
        pidff, = lookup({"name": "PIDFF"})["entries"]
        text = " ".join(f["claim"] for f in pidff["facts"])
        self.assertIn("kp négatif n’est donc pas accepté", text)
        self.assertIn("ovs_att", text)
        compact = next(e for e in lookup({"name": "PID_Compact"})["entries"] if e["libraryVersion"] is None)
        claims = " ".join(f["claim"] for f in compact["facts"])
        self.assertIn("gain proportionnel négatif n’est pas pris en charge", claims)
        self.assertIn("en secondes", claims)
        self.assertIsNotNone(pidff["equation"])                         # read at the source (document 33002536)
        self.assertIsNone(compact["equation"])                          # PID_Compact: documentation not read yet
        for entry in (pidff, compact):
            self.assertFalse(entry["selectable"])


class SiemensVersionsTests(unittest.TestCase):
    def test_versions_are_distinct_entries_and_a_version_filter_keeps_the_unversioned_facts(self):
        entries = lookup({"name": "PID_Compact"})["entries"]
        self.assertEqual([e["libraryVersion"] for e in entries], [None, "V2.x", "V3.x"])
        v3 = lookup({"name": "PID_Compact", "libraryVersion": "V3.x"})["entries"]
        self.assertEqual([e["libraryVersion"] for e in v3], [None, "V3.x"])      # unversioned facts still apply
        v2 = lookup({"name": "PID_Compact", "libraryVersion": "V2.x"})["entries"]
        self.assertEqual([e["libraryVersion"] for e in v2], [None, "V2.x"])
        # Hardware and firmware compatibility are not documented: they stay explicitly unverified for every version.
        for entry in entries:
            self.assertTrue({"cpu", "firmware", "softwareVersion"} <= set(entry["unverifiedTargetFields"]))
            self.assertFalse(entry["selectable"])

    def test_designated_vendor_documents_are_recorded_as_not_read(self):
        sources = {s["id"]: s for s in lookup({})["sources"]}
        # 33002536 (PIDFF) and 33002520 (PID_INT) have been read since: their edition and scope say so.
        self.assertEqual(sources["SE-33002520K01000"]["edition"], "09/2020 (33002520.20)")
        self.assertEqual(sources["SE-33002536K01000"]["edition"], "09/2020 (33002536.16)")


class PidffOfficialDocumentTests(unittest.TestCase):
    def test_equation_and_conventions_are_read_at_the_source_with_their_limits(self):
        result = lookup({"name": "PIDFF"})
        sources = {x["id"]: x for x in result["sources"]}
        entry, = result["entries"]
        document = sources["SE-33002536K01000"]
        self.assertEqual(document["edition"], "09/2020 (33002536.16)")
        self.assertIn("images", document["scope"])                                   # transcribed from images: said so
        self.assertIn("CPU restent à identifier", document["scope"])
        self.assertIn("kp × [1 + 1/(ti·p)", entry["equation"])
        self.assertIn("α = (out_sup − out_inf) / (pv_sup − pv_inf)", entry["equation"])
        text = " ".join(f["claim"] for f in entry["facts"])
        for needle in ("sense = +1 pour rev_dir = 1", "Incohérence de la documentation", "DEV_WGH = PV − (1 − ovs_att) × SP",
                       "td/kd", "aucune conversion parallèle n'est offerte", "outrate"):
            self.assertIn(needle, text)
        # Read at the source, yet still not qualified and not selectable: no CPU, firmware or software version.
        self.assertEqual((entry["status"], entry["selectable"]), ("to_verify", False))
        self.assertEqual(entry["library"], "Control")
