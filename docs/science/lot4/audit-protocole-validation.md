# Audit préalable au protocole directeur de validation (étape 1) — 2026-11-28

Base : branche `claude/science-engine-dev`, 60 fichiers de tests Python (508 tests exécutés en CI, run 374 vert), tests .NET (Orchestration 37 contrôles, Smoke Windows, Communication, PhysicalLoop), CI Windows « Vérification MKR ». **Ce qui suit n'est affirmé que si je l'ai lu ou exécuté dans cette session ; sinon c'est marqué « non audité ».**

## 1. Ce qui existe et a été vérifié (à ne pas refaire)
| Domaine | Existant | Preuve | Limite |
|---|---|---|---|
| Identification FOPDT, SOPDT, intégrateur, ARX, Broïda (méthode, v1.2.0) | `fopdt.py`, `sopdt.py`, `integrator.py`, `arx.py`, `broida.py` | tests unitaires et campagnes synthétiques à paramètres vrais connus (`test_reference_campaign.py`, `test_process_diversity.py`) | données **synthétiques** ; Broïda traité comme méthode d'estimation FOPDT |
| Porte de validation (v1.1.0) | `validation_gate.py` : RMSE + autocorrélation lag-1 sur essai de validation indépendant | `test_validation_gate.py` ; étude LOT 4 tranche 1 | étude **non conclusive** pour tout nouveau critère (voir rapport) ; critère actuel conservé |
| Réglage guidé | `recommend.py` (SIMC, Ms sur coins d'incertitude, refus motivé), `sampled_loop.py` (boucle échantillonnée exacte) | tests dont références analytiques (Skogestad 2003) ; Ms recalculé indépendamment (écart 1e-7) | coins = estimation, **pas** une garantie de pire cas (déjà déclaré) |
| Stabilité | `stability_margins.py` ; Nyquist et simulation temporelle indépendantes dans l'étude | 120/120 accords sur plusieurs types de procédés | durée finie ≠ preuve générale |
| Identification en boucle fermée | `closed_loop.py` (deux étapes, Van den Hof & Schrama 1993), refus des cas douteux | tests sur simulations | **étiqueté expérimental, validé sur simulations seulement** (ADR 0006) |
| Mode manuel/auto, plateau Broïda, assistant d'essais | `fopdt.require_manual`, `trial_assistant.py` | tests | non validés sur essais réels en direct |
| Blocs constructeurs | PI_B, PIDFF, PID_INT, PID_Compact : modèles exécutables documentaires, conversions | tests ; documentation officielle lue pour PI_B/PIDFF/PID_INT | **aucun bloc testé chez le constructeur ni qualifié sur automate** ; interprétations listées dans les docstrings |
| Traçabilité | LOT 1 : dépendances, obsolescence dérivée, preuve de validation scellée, migration SQLite 12, conseiller | 37 contrôles Orchestration + Smoke Windows (run 387 vert) | **non branché de bout en bout** : l'enregistrement des dépendances à chaque calcul et les changements de configuration ne sont pas encore câblés ; l'interface ne montre l'obsolescence que lorsque des dépendances existent |
| Données réelles déjà utilisées | TCLab (CC BY 4.0, extrait de 121 points, `test_real_tclab_*`) | régressions ; critères fixés avant ou après observation, **déclarés** dans les docstrings | extrait unique, un banc ; ne compte pas comme référence indépendante |
| Étude du critère de validation | `tests/reference/validation_study/`, protocole + amendements v1.1–v1.5, rapport tranche 1 | 27 tests ; mutation vérifiée sur `adopt` ; seuils gelés avec empreinte | synthétique seulement ; 18 inaptes à la définition primaire (59 requis) |

## 2. Écarts par rapport au protocole (faiblesses identifiées, non encore traitées)
1. **§6–7 : pas de validation à l'aveugle possible aujourd'hui.** Aucun évaluateur indépendant, aucune donnée conservée hors du dépôt de développement, et les sources publiques ont des références accessibles. Je prépare l'outillage ; je ne revendique rien.
2. **§3 : diversité de procédés insuffisante.** Niveau seulement (S01, S03, S04) ; pas de pression, débit, température (hors TCLab déjà utilisé), intégrateurs, forts temps morts, multivariable téléchargés et contrôlés. S02 et S05 sont inaccessibles ; l'agent n'a trouvé aucune boucle de procédé sous PID à réglages fixes avec boucle ouverte du même procédé.
3. **§8 : taux de détection du diagnostic non mesurés** (détection / non-détection / fausses alertes) sur données réelles.
4. **§9 : l'assistant d'essais n'est pas validé sur essai réel.**
5. **§10 : « modèles d'ordre supérieur, non linéaires, multivariables »** : non implémentés au sens de l'inventaire `engine/` (aucun module trouvé) ; doivent être signalés comme **non disponibles** dans l'interface et la matrice, jamais simulés.
6. **§11 : sélection par aptitude à la régulation** : `model_comparison.py` compare sur la prédiction de validation ; la comparaison par robustesse de la régulation et la sensibilité du réglage aux modèles compatibles sont des travaux **à consolider** (le réglage plus robuste récupère 12/18 réglages inaptes dans l'étude synthétique ; l'alternative SOPDT 0/18).
7. **§12 : stratégies** : `strategy_selection.py` compare des candidats simulés ; **cascade, feedforward (hors conversion PIDFF), ratio, split-range, override, Smith, gain scheduling, MPC : aucun module de réglage trouvé** — non développés, à déclarer comme tels.
8. **§13 : méthodes de réglage** : SIMC/IMC et optimisation présents ; Lambda, Ziegler-Nichols, Cohen-Coon : à vérifier au cas par cas (`tuning_comparison.py`) — **non audité ici**.
9. **§18 : oracles** : une partie des tests est de la non-régression (valeurs figées) ; le test de reproductibilité de l'étude le déclare. Un inventaire test par test (analytique / indépendant / expérimental / non-régression) reste à faire.
10. **§21 : données à l'aveugle hors infrastructure** : à mettre en place avec le client.

## 3. Plan par étape (§25)
| Étape | État |
|---|---|
| 1 Audit | **fait (ce document)**, à compléter par l'inventaire des oracles (§2 pt 9) |
| 2 Acquisition | S01, S03, S04 téléchargés et contrôlés ; S02, S05 bloqués ; complémentaires à chercher |
| 3 Benchmarks | règle de répartition A/B/C à figer par fichier/essai ; D impossible sans évaluateur indépendant |
| 4 Évaluation | à lancer sur S01/S03/S04 (identification, prédiction, diagnostic) en respectant A/B/C |
| 5 Consolidation | seulement pour des faiblesses **démontrées** par l'étape 4 |
| 6–7 Intégration, gel | LOT 1 en cours ; gel après l'étape 5 |
| 8 Aveugle | outillage seulement |
| 9 Qualification industrielle | moyens matériels : essai TCLab dédié, automates M580/S7-1500 : **décision du client** |
| 10 Bilan | rapport + matrice (`matrice-de-qualification.md`) |
