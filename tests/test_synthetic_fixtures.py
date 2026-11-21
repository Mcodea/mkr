from pathlib import Path
from tempfile import TemporaryDirectory
import json
import unittest

from mkr_data_import.io.csv_import import import_csv
from samples.synthetic.generate import generate


class SyntheticFixturesTests(unittest.TestCase):
    def test_regeneration_and_readability(self):
        fixtures = Path(__file__).resolve().parents[1] / "samples" / "synthetic"
        reference = json.loads((fixtures / "truth.json").read_text(encoding="utf-8"))
        with TemporaryDirectory() as directory:
            target = Path(directory)
            generate(target)
            self.assertEqual((target / "truth.json").read_bytes(), (fixtures / "truth.json").read_bytes())
            for name, case in reference.items():
                with self.subTest(name=name):
                    filename = f"{name}.csv"
                    raw = (fixtures / filename).read_bytes()
                    self.assertEqual(raw, (target / filename).read_bytes())
                    result = import_csv(
                        raw, delimiter=",", encoding="utf-8", decimal_separator=".",
                        timestamp_column="timestamp", date_format="%Y-%m-%dT%H:%M:%S%z",
                        columns=case["units"],
                    )
                    self.assertEqual(result.errors, ())
                    self.assertGreater(len(result.records), 2)
