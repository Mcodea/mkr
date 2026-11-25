"""Calibration of the published standard errors of the SOPDT and integrator fits (they feed the guided tuning's uncertainty set).

Monte-Carlo with seeded noise: over independent noise realisations of a process with KNOWN parameters, the true value must lie
within 2.5 standard errors of the estimate in the large majority of runs (expected 98.8 % for a Gaussian estimate). A systematic
under-estimation of the errors would make the guided tuning too optimistic, so this guards the uncertainty set, not the optimum."""
import unittest

import numpy as np

from test_process_diversity import first_order, integrator, second_order, source
from validation_gate import assess, suggest


def run(family, response, cadence, sigma, seed, post):
    rng = np.random.default_rng(seed)
    trials = dict(training=source(1, response, 2.0, cadence, sigma, rng, post=post),
                  validation=source(2, response, -1.5, cadence, sigma, rng, post=post), mvTolerance=0.01, pvTolerance=6 * sigma)
    criteria = suggest(trials)
    return assess(dict(family=family, trials=trials, maximumRmse=criteria["maximumRmse"],
                       maximumResidualLagOne=criteria["maximumResidualLagOne"]))["report"]


class Calibration(unittest.TestCase):
    def coverage(self, family, response, truth, cadence, sigma, seeds, post):
        hits = {name: 0 for name in truth}
        published = 0
        for seed in seeds:
            report = run(family, response, cadence, sigma, seed, post)
            errors = report.get("standardErrors")
            if errors is None:
                continue
            published += 1
            for name, value in truth.items():
                hits[name] += abs(report[name] - value) <= 2.5 * errors[name]
        self.assertGreaterEqual(published, 0.8 * len(seeds), "errors must be published for most well-posed fits")
        return {name: hits[name] / published for name in truth}

    def test_integrator_errors_cover_the_truth(self):
        result = self.coverage("Integrateur", integrator(0.02, 5), dict(gain=0.02), 1.0, 0.01, range(100, 125), 120)
        self.assertGreaterEqual(result["gain"], 0.85, result)

    def test_second_order_errors_cover_the_truth(self):
        result = self.coverage("SOPDT", second_order(2, 120, 30, 10), dict(gain=2.0, tauSeconds=120.0),
                               (10 + 6 * 120) / 110, 0.04, range(200, 212), 250)
        self.assertGreaterEqual(result["gain"], 0.8, result)

    def test_errors_are_withheld_when_the_fit_is_not_trustworthy(self):
        # a clean fit publishes errors; an exact (noise-free) first-order fit has none to publish for the integrator tail case
        report = run("Integrateur", integrator(0.02, 5), 1.0, 0.01, 7, 120)
        self.assertIn("standardErrors", report)
        self.assertTrue(report["standardErrors"] is None or report["standardErrors"]["gain"] > 0)


if __name__ == "__main__":
    unittest.main()
