"""LOT 0b, P0-4 : le moteur refuse lui-même de régler un modèle dont la validation n'est pas déclarée acceptée.

Avant correction, le refus n'existait que dans l'application C# (RequireAcceptedForTuning) : le moteur calculait un réglage pour
n'importe quels paramètres. Le moteur est sans état : il ne peut pas relire la base, mais il peut EXIGER la preuve de validation dans la
requête (modelValidation.accepted = true, sans dérive déclarée) et refuser sinon, avec un code explicite.
"""
import json
import unittest

from mkr_engine import respond

PAYLOAD = dict(family="FOPDT", gain=1.0, tauSeconds=10.0, delaySeconds=2.0, secondTauSeconds=None, sampleSeconds=1.0, standardErrors=None,
               objective="balanced", derivativeFilterRatio=None, processVariationPercent=None, optimise=False)


def call(payload):
    raw = json.dumps(dict(version=1, requestId="t-1", operation="recommend_tuning", payload=payload)).encode()
    return respond(raw)


class RecommendRequiresValidatedModel(unittest.TestCase):
    def test_missing_proof_is_refused(self):
        self.assertEqual(call(dict(PAYLOAD))["error"]["code"], "recommend_model_not_validated")

    def test_not_accepted_is_refused(self):
        for validation in (dict(accepted=False), dict(accepted=None), dict(accepted="true"), dict(), "accepted", None):
            with self.subTest(validation=validation):
                self.assertEqual(call(dict(PAYLOAD, modelValidation=validation))["error"]["code"], "recommend_model_not_validated")

    def test_drifting_model_is_refused(self):
        self.assertEqual(call(dict(PAYLOAD, modelValidation=dict(accepted=True, drift=True)))["error"]["code"], "recommend_model_not_validated")

    def test_accepted_model_gets_its_recommendation(self):
        reply = call(dict(PAYLOAD, modelValidation=dict(accepted=True, drift=False)))
        self.assertNotIn("error", reply)
        self.assertEqual(reply["result"]["decision"], "recommended")


if __name__ == "__main__":
    unittest.main()
