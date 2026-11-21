import unittest
from tuning_comparison import compare


def run(name, **metrics):
    return dict(name=name, parameters={"kp": 1}, scenario={"study": "test", "unit": "bar"},
                valid=True, result=metrics)


class TuningComparisonTests(unittest.TestCase):
    def test_equal_missing_and_conflicting_objectives(self):
        a, b = run("A", iae=1, ise=3), run("B", iae=1, ise=3)
        def result(criteria):
            return compare(dict(criteria=criteria, runs=[a,b]))
        self.assertEqual(result(["iae"])["decision"], "equal")
        self.assertEqual(result([])["decision"], "missing_criteria")
        self.assertEqual(result(["itae"])["decision"], "insufficient_evidence")
        b["result"] = dict(iae=2, ise=1)
        self.assertEqual(result(["iae", "ise"])["decision"], "no_unique_preference")
        self.assertEqual(result(["iae"])["preferred"], "A")
        b["scenario"]["unit"] = "Pa"
        self.assertIsNone(result(["iae"])["preferred"])
