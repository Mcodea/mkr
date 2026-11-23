# Contrat WPF–Python v1

Un document JSON UTF-8 sur stdin, fermé après écriture, un document JSON UTF-8 sur stdout. Une exécution par requête. stderr est réservé au diagnostic interne. Le client limite l'exécution à dix secondes et termine le processus enfant à l'annulation.

Requête exacte : `{"version":1,"requestId":"abc123","operation":"ping","payload":{}}`. L'identifiant est ASCII alphanumérique, avec tiret/underscore autorisés, limité à 64 caractères. L'opération ping vérifie la disponibilité et ne calcule aucun résultat. Le moteur 0.1 acceptait au plus 8192 octets en entrée.

Succès : `{"version":1,"requestId":"abc123","engineVersion":"0.2.0","result":{"status":"ready"}}`.

Erreur : `{"version":1,"requestId":"abc123","engineVersion":"0.2.0","error":{"code":"unsupported_operation"}}`. requestId vaut null si l'identifiant ne peut être validé. Codes : invalid_json, invalid_request, unsupported_version, unsupported_operation. Les erreurs de contrat sont des réponses JSON. Un code de sortie non nul est un échec de processus. Toute future opération scientifique versionnera aussi son algorithme.

## Extension CSV du moteur 0.2

Le plafond du message passe à 1 500 000 octets. Le client v1 conserve la même requête ping. Les opérations `inspect_csv` et `analyze_csv` prennent `raw` (octets originaux en Base64) et `delimiter` (virgule ou point-virgule). L’analyse demande aussi `timeColumn`, `valueColumn`, `role` et `unit`. La réponse contient `headers`, `preview` (200 enregistrements au plus) et `rowCount`. L’analyse ajoute `quality` et son `algorithmVersion`. Les erreurs CSV utilisent le même format d’enveloppe avec les codes `csv_limit`, `csv_header`, `csv_width`, `csv_empty`, `csv_encoding_or_syntax` et `csv_mapping`.

## Extension identification du moteur 0.3

Le plafond passe à 2 900 000 octets. `identify_fopdt` reçoit `training` et `validation`, chacun avec `raw` (Base64), `delimiter`, `timeColumn`, `mvColumn` et `pvColumn`, plus `mvTolerance` et `pvTolerance` (nombres). Le client accorde 30 secondes à cette opération. Le résultat contient paramètres et bornes, diagnostics séparés, avertissements et version d’algorithme. Les codes `fit_*` distinguent les erreurs de méthode des erreurs CSV.

Le moteur 0.4 ajoute `identify_sopdt` avec le même schéma de requête. La réponse SOPDT ajoute `secondTauSeconds`. Dans FOPDT, `standardErrors` contient les erreurs-types conditionnelles lorsqu’elles sont calculables, ou vaut null.

Le moteur 0.5 ajoute `identify_integrator` avec le même schéma de requête. La réponse de cette famille omet les constantes de temps et leurs bornes. `gain` est le gain intégrateur exprimé par seconde. `fit_integrator_tail` indique une dérive terminale insuffisante sur l’essai d’estimation.

Le moteur 0.6 ajoute `calculate_pi` avec les nombres `gain`, `tauSeconds`, `delaySeconds`, `responseSeconds`. La réponse versionnée compare `imc` et `simc`, chacun avec `kp`, `tiSeconds` et `parallelKi`. Les erreurs `pi_input`, `pi_domain` et `pi_range` concernent respectivement le schéma, le domaine de la méthode et la plage numérique.

Le moteur 0.7 ajoute `simulate_pi`. La requête contient les paramètres FOPDT `gain`, `tauSeconds`, `delaySeconds`, les deux structures `imc` et `simc` avec `kp` et `tiSeconds`, et le scénario `sampleSeconds`, `durationSeconds`, `setpoint`, `minimumMv`, `maximumMv`, `disturbance`, `disturbanceAtSeconds`. La réponse contient les indicateurs et séries de chaque méthode. Les erreurs `sim_input`, `sim_domain`, `sim_grid` et `sim_range` distinguent le schéma, le domaine, le pas et la plage numérique.

Le moteur 0.8 ajoute `simulate_open_loop`. La requête associe les paramètres FOPDT `gain`, `tauSeconds`, `delaySeconds` aux champs `sampleSeconds`, `durationSeconds`, `stepMv`, `stepAtSeconds`, `minimumMv` et `maximumMv`. La réponse contient `algorithmVersion`, `warnings`, `steadyPv`, `saturationSeconds` et `points`. Chaque point donne `seconds`, `pv`, `requestedMv` et `appliedMv`. Les deux commandes sont nulles au dernier point. Les entrées invalides renvoient `sim_input`, `sim_domain` ou `sim_grid`.

Le moteur 0.9 accepte `maximumMvRate` facultatif (absent, nul au sens JSON, ou strictement positif) pour les deux opérations de simulation. Les résultats ajoutent `rateLimitedSeconds`. Chaque série PI contient aussi `setpointResponse` avec `windowEndSeconds`, `overshootPv` et `settlingSeconds`. Les deux dernières valeurs peuvent être nulles.

Le moteur 0.10 ajoute `assess_model`, la décision de validation indépendante exigée avant tout réglage (audit NC-02).
- **Requête :** `family` (`FOPDT`, `SOPDT`, `Integrateur` ou `BroidaFOPDT`), `trials` (le même objet que `compare_models`), `maximumRmse` en unité de PV et `maximumResidualLagOne` (0 < valeur ≤ 1).
- **Réponse :** `report`, le résultat d'identification de la famille, et `assessment`, qui contient `algorithmVersion`, `accepted`, `reasons` et `criteria`.
- **Règle d'acceptation :** elle est commune à `compare_models` et figure dans `engine/validation_gate.py`. Elle porte sur la RMSE de validation et la corrélation des résidus de validation. Les avertissements `parameter_bound`, `short_horizon`, `fast_pole_resolution`, `ill_conditioned` et `integrator_validation_tail` sont bloquants.
- **Erreurs :** des critères invalides renvoient `fit_comparison_limits`.
- **Délai :** le client accorde 30 secondes à `assess_model` et à `compare_models`.

Le moteur 0.10 ajoute aussi `suggest_criteria`, appelée avec les mêmes champs que `identify_fopdt`.
- **Réponse :** `algorithmVersion`, `baselineNoise`, `maximumRmse`, `maximumResidualLagOne`, `basis` (`noise` ou `pv_tolerance`), `noiseMultiple`, `trainingNoise` et `validationNoise`.
- **Évolution de `compare_models` (version 1.1.0) :** le champ `decision` peut valoir `significantly_better_higher_order`, avec `holdoutTest` (`mseRatio`, `critical`, `level`). Ce champ vaut null quand le test n'est pas applicable.
- **Diagnostics :** chaque diagnostic d'ajustement ajoute `baselineNoise`.
