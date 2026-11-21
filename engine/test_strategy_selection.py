import unittest
from strategy_selection import select
from test_pid_simulation import case


class StrategySelectionTests(unittest.TestCase):
    def study(self):
        scenario = case()
        del scenario["controller"]
        return dict(studyId="study1", modelRunId="model1", scenario=scenario, criteria=["iae"],
                    maximumAbsoluteFinalError=2, controllers={
                        "P": dict(kind="P", kp=0.5),
                        "PI": dict(kp=0.5, tiSeconds=4, tdSeconds=0, filterSeconds=0),
                    })

    def test_common_model_and_explicit_criteria(self):
        self.assertEqual(select(self.study())["selected"], "PI")
        self.assertIsNone(select({**self.study(), "criteria": []})["selected"])

    def test_insufficient_evidence_returned_with_reasons(self):
        study = self.study()
        study["controllers"]["PID"] = dict(kp=0.5, tiSeconds=4, tdSeconds=0.3, filterSeconds=0.2)
        report = select(study)
        self.assertIsNone(report["selected"])
        self.assertIn("derivative_noise_response_unassessed", report["evidence"][-1]["reasons"])
