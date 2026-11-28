"""Le dispositif de qualification indépendante : gel du moteur, détection de modification, scellement des résultats avant révélation (protocole §7)."""
import importlib.util, json, tempfile, unittest
from pathlib import Path

SPEC = importlib.util.spec_from_file_location("blind_tool", Path(__file__).resolve().parents[1] / "scripts" / "blind" / "blind_tool.py")
blind = importlib.util.module_from_spec(SPEC); SPEC.loader.exec_module(blind)


class BlindToolTests(unittest.TestCase):
    def repo(self, root):
        (Path(root) / "engine").mkdir(); (Path(root) / "engine" / "algo.py").write_text("X = 1\n")
        (Path(root) / "engine" / "test_algo.py").write_text("# ignoré : un test n'est pas le moteur\n")
        (Path(root) / "contracts").mkdir(); (Path(root) / "contracts" / "c.json").write_text("{}")

    def test_freeze_then_verify_is_clean_and_any_change_is_detected(self):
        with tempfile.TemporaryDirectory() as t:
            self.repo(t); manifest = Path(t) / "manifest.json"
            body = blind.freeze(t, manifest)
            self.assertIn("engine/algo.py", body["files"]); self.assertNotIn("engine/test_algo.py", body["files"])
            self.assertEqual(blind.verify(t, manifest), [])
            (Path(t) / "engine" / "algo.py").write_text("X = 2\n")                      # un seuil change : le gel est rompu
            self.assertEqual(blind.verify(t, manifest), ["engine/algo.py"])
            (Path(t) / "engine" / "algo.py").write_text("X = 1\n")                      # retour à l'état figé : de nouveau propre
            self.assertEqual(blind.verify(t, manifest), [])
            (Path(t) / "engine" / "new.py").write_text("Y = 1\n")                       # un fichier ajouté est aussi un écart
            self.assertEqual(blind.verify(t, manifest), ["engine/new.py"])

    def test_results_must_be_sealed_unchanged_before_the_comparison_is_allowed(self):
        with tempfile.TemporaryDirectory() as t:
            results = Path(t) / "results"; results.mkdir(); (results / "case1.json").write_text('{"decision":"refused"}')
            seal = Path(t) / "seal.json"
            with self.assertRaises(FileNotFoundError):
                blind.check(results, Path(t) / "absent.json")                           # pas de scellement : pas de comparaison
            blind.seal(results, seal)
            self.assertEqual(blind.check(results, seal), [])
            (results / "case1.json").write_text('{"decision":"accepted"}')              # résultat modifié APRÈS scellement : refusé
            self.assertEqual(blind.check(results, seal), ["case1.json"])
            self.assertEqual(blind.main(["x", "reveal-allowed", str(results), str(seal)]), 2)

    def test_manifest_keys_use_forward_slashes_so_a_freeze_verifies_on_any_platform(self):
        with tempfile.TemporaryDirectory() as t:
            self.repo(t); body = blind.freeze(t, Path(t) / "m.json")
            self.assertTrue(all("\\\\" not in key for key in body["files"]))
            results = Path(t) / "r" / "sub"; results.mkdir(parents=True); (results / "a.json").write_text("1")
            self.assertTrue(all("\\\\" not in key for key in blind.seal(Path(t) / "r", Path(t) / "s.json")["files"]))

    def test_an_empty_results_directory_cannot_be_sealed(self):
        with tempfile.TemporaryDirectory() as t:
            empty = Path(t) / "r"; empty.mkdir()
            with self.assertRaises(SystemExit):
                blind.seal(empty, Path(t) / "s.json")

    def test_the_seal_records_a_utc_time_and_a_global_digest(self):
        with tempfile.TemporaryDirectory() as t:
            results = Path(t) / "r"; results.mkdir(); (results / "a.json").write_text("1")
            body = blind.seal(results, Path(t) / "s.json")
            self.assertTrue(body["sealed_at_utc"].endswith("+00:00")); self.assertEqual(len(body["seal_sha256"]), 64)


if __name__ == "__main__":
    unittest.main()
