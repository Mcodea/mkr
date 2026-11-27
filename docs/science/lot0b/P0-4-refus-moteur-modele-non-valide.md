# P0-4 — Le moteur ne refusait pas de régler un modèle non validé

## Anomalie
Le refus « aucun réglage sans modèle accepté à la validation indépendante » n'existait que dans l'application C# (`RequireAcceptedForTuning`). Le moteur Python calculait un réglage pour n'importe quels paramètres.

## Preuve
`engine/test_recommend_requires_validated_model.py` : appel de l'opération IPC `recommend_tuning` sans preuve, avec `accepted` faux/nul/non booléen, ou avec dérive déclarée. **Avant correction : 9 échecs sur 10** (le moteur renvoyait un réglage ou une erreur de contrat sans rapport).

## Impact
Tout autre appelant du moteur (script, futur module, erreur d'intégration) pouvait obtenir une recommandation de réglage pour un modèle non validé, contournant la règle centrale du cahier des charges (§15 : « si aucun candidat ne satisfait les critères, bloquer les recommandations »).

## Référence
Exigence du cahier directeur §15 et §25 (blocage de la progression) — décision d'architecture, pas une méthode scientifique à référencer.

## Correction
Le moteur est sans état : il ne peut pas relire la base. Il **exige la preuve dans la requête** : `modelValidation.accepted` strictement `true` et aucune dérive déclarée, sinon erreur `recommend_model_not_validated`. Le C# fournit la preuve depuis l'étude (`IsAcceptedForTuning`) via un paramètre explicite et obligatoire `modelAccepted` de `RecommendTuningAsync` (le chemin ARX envoie `study.IsAccepted`). Le smoke test vérifie que le moteur refuse sans preuve (`CsvInputException`).

## Résultats avant / après
| Requête | Avant | Après |
|---|---|---|
| sans `modelValidation` | réglage calculé | refus `recommend_model_not_validated` |
| `accepted=false/null/"true"` | réglage ou erreur de contrat | refus |
| `accepted=true, drift=true` | réglage calculé | refus |
| `accepted=true` | réglage | réglage (inchangé) |

## Limites
Un appelant malhonnête peut mentir sur `accepted` : la protection est un **contrat vérifié des deux côtés**, pas une preuve cryptographique. La vérification de dérive côté application (valeur `drift` toujours `false` aujourd'hui) est à brancher au LOT 1 (invalidation et dérive).
