# Contrat WPF–Python : référence des opérations (révision 3)

Complète `ipc-v1.md` (enveloppe, limites, historique par version du moteur). **Cette référence est vérifiée par `engine/test_ipc_contract.py`** : toute opération servie par `engine/mkr_engine.py` doit figurer dans `OPERATIONS` et être décrite dans `docs/communication/`. L'opération `describe_contract` renvoie `protocolVersion` (1), `contractRevision` (3) et la liste exacte des opérations ; l'application peut s'en servir pour refuser un moteur incompatible (contrôle C# prévu au LOT 1).

Conventions : charge utile JSON stricte (clés exactes ; une clé inconnue ou manquante donne un code d'erreur du module) ; nombres finis ; unités de la grandeur (`bar`, `%`, `s`…) portées par la requête ; résultats structurés avec `algorithmVersion`. Délai accordé par le client : 30 s pour `identify_*`, `recommend_tuning`, `assess_model`, `compare_models`, `suggest_criteria`, `find_steps`, `check_model_drift` ; 10 s pour les autres.

## Opérations ajoutées depuis `ipc-v1.md` (clés d'entrée relevées dans le code)

| Opération | Module | Clés d'entrée | Rôle |
|---|---|---|---|
| `ping` | `mkr_engine.py` | aucune (charge utile vide) | `{"status":"ready"}` |
| `describe_contract` | `mkr_engine.py` | aucune | version du protocole, révision du contrat, liste des opérations, **versions de calcul** `libraries` (`python`, `numpy`, `scipy` : lues dans les métadonnées d'installation sans importer les bibliothèques ; `null` si absente, jamais devinée) — traçabilité du LOT 1 ; leur enregistrement avec chaque étude reste à faire côté C# |
| `identify_broida` | `broida.py` | comme `identify_fopdt` (`training`, `validation`, `mvTolerance`, `pvTolerance`) | méthode d'estimation graphique d'un FOPDT (abscisses à 28 % et 40 %) ; **sera reclassée en méthode du FOPDT au LOT 3** |
| `identify_arx` | `arx.py` | `training`, `validation`, `mvTolerance`, `maximumRmse`, `loop` (`"open"` exigé) | ARX/OE avec excitation quelconque ; `selected.firstOrder` n'est publié que si le pôle est distinct de 1 et si τ ≤ durée enregistrée (P0-2) |
| `identify_closed_loop` | `closed_loop.py` | `source`, `training`, `validation`, `mvLower`, `mvUpper`, `spTolerance`, `pvTolerance` | identification en boucle fermée, **expérimentale** (ADR 0006) |
| `check_model_drift` | `model_drift.py` | `family`, `model`, `trial`, `mvTolerance`, `pvTolerance`, `maximumRmse`, `maximumResidualLagOne` | compare un nouvel essai à un modèle validé |
| `recommend_tuning` | `recommend.py` via `mkr_engine.py` | `family`, `gain`, `tauSeconds`, `delaySeconds`, `secondTauSeconds`, `sampleSeconds`, `standardErrors`, `objective`, `derivativeFilterRatio`, `processVariationPercent`, `optimise` ; facultatif `controllerPeriodSeconds` ; **obligatoire `modelValidation`** (`accepted: true`, sans `drift`) | réglage guidé : refus `recommend_model_not_validated` sans preuve ; Ms jugé sur la boucle échantillonnée (P0-3, P0-4) |
| `assess_trial` | `trial_assistant.py` | `seconds`, `mv`, `pv`, `mvTolerance`, `pvTolerance` ; facultatifs `sp`, `manual`, `loopMode` (`open_loop` seul traité), `processKind` (`self_regulating` seul traité), `mvLow`, `mvHigh`, `maxDurationSeconds`, `hasValidationTrial` | assistant d'essai en temps réel (section 42.2-42.4), tranche 1 : lit un tampon d'acquisition et renvoie l'état de l'essai (régime initial, échelon détecté, réponse en cours, données supplémentaires nécessaires, exploitable…), les passages 28 %/40 % et la durée restante **conditionnelle au modèle**. Estimation toujours provisoire ; `safetyAuthorization` toujours `false` ; aucune écriture. Codes d'erreur `assess_mapping`, `assess_data`, `assess_tolerance` |
| `calculate_integrator_pi` | `pi_tuning_integrator.py` | `gain`, `delaySeconds`, `responseSeconds` | PI SIMC d'un procédé intégrateur |
| `simulate_pi_transition` | `pi_mode_transition.py` | `gain`, `tauSeconds`, `delaySeconds`, `sampleSeconds`, `durationSeconds`, `setpoint`, `minimumMv`, `maximumMv`, `manualMv`, `automaticAtSeconds`, `kp`, `tiSeconds` ; facultatif `maximumMvRate` | passage manuel → automatique |
| `simulate_pi_robustness` | `pi_robustness.py` | `gain`, `tauSeconds`, `delaySeconds`, `imc`, `simc`, `sampleSeconds`, `durationSeconds`, `setpoint`, `minimumMv`, `maximumMv`, `disturbance`, `disturbanceAtSeconds`, … (liste complète : `expected` dans le module) | sensibilité d'un PI aux variations des paramètres |
| `simulate_pi_combined` | `pi_combined_robustness.py` | mêmes clés que `simulate_pi_robustness` | grille combinée de variations |
| `simulate_pid_experimental` | `pid_simulation.py` | `gain`, `tauSeconds`, `delaySeconds`, `controller`, `sampleSeconds`, `durationSeconds`, `setpoint`, `minimumMv`, `maximumMv`, `disturbance`, `disturbanceAtSeconds` ; facultatifs `maximumMvRate`, `measurementNoise`, `secondTauSeconds` | simulation PID, **expérimentale** |
| `select_strategy` | `strategy_selection.py` | `studyId`, `modelRunId`, `scenario`, `controllers`, `criteria`, `maximumAbsoluteFinalError` | comparaison de régulateurs candidats (aucun appelant C# à ce jour) |
| `compare_tunings` | `tuning_comparison.py` | `criteria`, `runs` | dominance sans pondération (aucun appelant C# à ce jour) |
| `lookup_blocks` | `block_catalogue.py` | filtres de cible (`manufacturer`, `cpu`, `firmware`, `software`, `softwareVersion`, `library`, `libraryVersion`) et `name`, toutes des chaînes | fiches du catalogue ; un champ vide de la fiche est signalé **non vérifié**, jamais compatible |
| `convert_block_settings` | `block_catalogue.py` | `name`, `target`, `parameters` | conversion vers un bloc constructeur ; renvoie `compatibility` (`verified_target` ou `not_verified`) et l'avertissement `hardware_compatibility_not_verified` ; refus `conversion_version_mismatch` si la version de bibliothèque de la cible diffère (P0-7) |
| `recommend_blocks` | `block_catalogue.py` | `target`, `strategy` (`P`/`PI`/`PID`), `functions`, `studyId`, `modelRunId` | blocs éligibles (aucun appelant C# à ce jour) |

## Codes d'erreur propres à cette révision
`recommend_model_not_validated` (P0-4), `conversion_version_mismatch` (P0-7), `broida_not_established` (Broïda 1.2.0 : plateau pas encore établi selon le bruit déclaré), `assess_mapping` / `assess_data` / `assess_tolerance` (assistant d'essai), `fit_not_manual` / `arx_not_manual` (une fenêtre d'identification contient une ligne hors mode manuel, colonne `MANUAL` présente ; P0-6), `invalid_request` pour `describe_contract` ou `ping` avec charge utile non vide.

## Ce qui reste à faire au LOT 1
Schémas JSON formels par opération (aujourd'hui : clés relevées dans le code), unités et hypothèses dans les réponses, erreurs avec détail, contrôle de révision par le client C#.
