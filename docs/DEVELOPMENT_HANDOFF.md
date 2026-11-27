# ORIAS / MKR — Reprise de développement (phase scientifique)

*Dernière mise à jour : 2026-11-27, LOT 0b clos (en attente de la section 42 du cahier et de l'ouverture du LOT 1).*

## Point de départ
- **Révision UI/UX réceptionnée :** `c7915cd` (dernier code : `482373a`). Tag local `ui-ux-recue-2026-11-26` (push du tag échoué à plusieurs reprises : à retenter). CI « Vérification MKR » verte : [run 329](https://github.com/Mcodea/mkr/actions/runs/45901338505), [run 330](https://github.com/Mcodea/mkr/actions/runs/45904584081).
- **Arbitrage du LOT 0 reçu** (2026-11-26) : LOT 0b autorisé ; accès réseau scientifique autorisé (environnement « ORIAS-MKR recherche scientifique », 20 hôtes) ; SIPPY exclu provisoirement ; licences des données à vérifier avant tout embarquement ; Broïda reclassé en méthode d'estimation du FOPDT ; scénario pression/pompe/M580 à construire (données synthétiques) ; rebaselining de la feuille de route autorisé. Cahier directeur déposé : `docs/requirements/cahier-directeur-orias.md`.

## Branches
- `claude/peaceful-cray-phk6q4` : branche UI/UX figée.
- `claude/science-engine-dev` : **branche active**. Aucune pull request. **Aucune fusion dans `main`.**

## LOT 0b — état (fiches dans `docs/science/lot0b/`)
| Correction | État |
|---|---|
| P0-1 erreur-type du gain FOPDT | **faite** (commit 58cf304), test `test_fopdt_error_coverage.py` |
| P0-2 ARX / intégrateur | **faite**, test `test_arx_integrator_guard.py` |
| P0-3a robustesse sur le SOPDT identifié | **faite**, test `test_recommend_half_rule_robustness.py` |
| P0-3b Ms de la boucle échantillonnée | **faite** (`sampled_loop.py`, `recommend.py` 1.2.0), tests `test_sampled_loop.py`, `test_recommend_sampled_loop.py` |
| P0-3c période du régulateur | moteur fait (`controllerPeriodSeconds` facultatif) ; **à relier côté C#/interface/SQLite (LOT 1)** |
| P0-4 refus moteur du modèle non validé | **faite** (`mkr_engine.py`, `modelAccepted` en C#), test `test_recommend_requires_validated_model.py` |
| P0-6 `loop="open"` forcé / MANUAL ignoré | **reportée au LOT 2** (justification dans la fiche) |
| P0-7 compatibilité catalogue | **faite côté moteur** (`compatibility`, avertissement affiché) ; saisie de la cible dans l'interface au LOT 7 |
| Reclassement de Broïda (méthode, pas famille) + migration | **au LOT 3** (95 usages de `ModelFamily`, assertions du smoke, stockage : lecture compatible des anciennes études, aucune réécriture de l'historique, tests de migration) |

## Constats de vérification bibliographique (à traiter)
- **E-1 (élevée)** : `pi_tuning.imcPublished` n'est pas un PI publié par Rivera, Morari & Skogestad 1986 (c'est le PID de Padé sans sa dérivée) ; l'attribution du code et de `docs/**/pi-fopdt.md` est à corriger (renommage = migration de stockage possible) — à traiter au LOT 5.
- E-2 bornes publiées d'IMC non appliquées ; E-3 « Lambda (Dahlin) » non vérifié ; E-4 plage Ms attribuée à Åström & Hägglund alors qu'elle est lue chez Skogestad 2003. Détails : `docs/science/references/referentiel-verifie.md`.
- Non lus (ouvrages non accessibles légalement) : Ljung, Söderström & Stoica, Åström & Hägglund, Åström & Wittenmark, Franklin-Powell ; Broïda 1969 (contenu : accès fermé).

## Travaux de recherche interrompus (limite d'usage atteinte, reprise après 03h50 UTC)
- `docs/science/licences/` : inventaire des dépendances et licences des jeux de données (brouillons, non relus ; sections bibliothèques candidates et README manquantes).
- `docs/science/scenario-pression-pompe-m580/` et `tests/reference/scenario_pression_pompe/` : scénario pression/pompe/M580 (brouillon, non relu) ; manuels Schneider (PIDFF, « PIFINT » à vérifier) non encore lus.

## LOT 1 — état (dossier : `docs/science/lot1/DOSSIER.md`)
| Élément | État |
|---|---|
| Contrat IPC versionné et documenté | **fait (1re tranche)** : `OPERATIONS`, `CONTRACT_REVISION = 2`, opération `describe_contract`, `docs/communication/ipc-operations.md` (16 opérations jusque-là non documentées), test `engine/test_ipc_contract.py` ; client C# `VerifyContractAsync` (refus explicite d'un moteur trop ancien ou incomplet, message affiché au démarrage), test dans le smoke avec un faux moteur ancien |
| Schémas JSON formels, unités et détail d'erreur dans les réponses | à faire |
| Orchestration (8 statuts), invalidation en cascade, traçabilité des versions, sauvegarde avant migration, période du régulateur, règles hors des vues | à faire |
| Section 42 du cahier | texte non reçu : répartition dans les lots à faire dès réception |

## Preuves du LOT 0b
- CI « Vérification MKR » [run 331](https://github.com/Mcodea/mkr/actions/runs/45975387738), commit `f6c87f0` : **vert à la tentative 2** (compilation, 338 tests Python, smoke .NET, SQLite, Modbus/S7, paquet autonome, tests graphiques). La tentative 1 avait échoué sur `CheckInterruptedSession` (`AcquisitionChecks.cs:261`, `EndOfStreamException`) : **test intermittent**, code d'acquisition non modifié, cause non démontrée ; à stabiliser au LOT 2 (acquisition) sans affaiblir son assertion.
- Fiches : `docs/science/lot0b/`. Aucun test existant supprimé ni modifié.

## Derniers résultats de tests
- Suite Python locale : 338 tests, 313 sous-tests verts (commit `f6c87f0`) ; CI Windows : voir ci-dessus.
- Section 42 du cahier directeur (complément v1.1) : **annoncée par le client mais texte non reçu** ; à intégrer au cahier, à la matrice de traçabilité, à la feuille de route et à ce document dès réception, puis à répartir dans les lots.
- Décisions en attente du client : sort du fichier `tests/reference/tclab_dyn_data2_extract.csv` (données APMonitor sans licence claire, versionné) ; sens de « PIFINT » (aucune source officielle sous ce nom).

## Règles à respecter à chaque session
Lire `AGENTS.md` et `docs/science/lot0/`. Ne pas refaire l'audit. Ne jamais fusionner dans `main`. Ne jamais affaiblir un test. Lecture seule sur les automates. Aucune déclaration de qualification ou de supériorité sans preuve. Opus pour les travaux scientifiques complexes, Sonnet pour le courant.
