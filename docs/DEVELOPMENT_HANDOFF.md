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
| Sauvegarde automatique avant migration SQLite | **faite** (`BackupBeforeMigration`, API de sauvegarde en ligne ; test dans le smoke depuis un schéma v1) ; tests de migration depuis v8–v11 à compléter |
| Schémas JSON formels, unités et détail d'erreur dans les réponses | à faire |
| Orchestration (8 statuts), invalidation en cascade, traçabilité des versions, fiche d'étude (limites, durée max, arrêt), journal d'événements d'essai, période du régulateur, règles hors des vues | à faire |
| Section 42 du cahier | **reçue et intégrée** (2026-11-27) : cahier, matrice de traçabilité (`docs/science/lot0/01-matrice-tracabilite.md`, complément v1.1), feuille de route (`docs/science/feuille-de-route-scientifique.md`) ; prérequis structurels au LOT 1 (fiche d'étude, journal d'événements, statut provisoire/définitif), assistant et boucle ouverte/fermée en interface au LOT 2, Broïda automatique au LOT 3 |

## Blocs Schneider (PIDFF, PI_B, PID_INT) — lecture des manuels officiels (2026-11-27)
Fiches : `docs/science/schneider/` (README, `pidff.md`, `pi_b.md`, `pid_int.md`, `ecarts-depot.md`). Manuels lus (hors dépôt, droit d'auteur ; MD5 identiques aux `checkSum` des pages se.com) : **33002535.16** (Control Expert, bibliothèque de régulation, EN, 09/2020), **33002520.16/.20** (bibliothèque standard, FR, 09/2020), 33002536.16 (régulation, FR). Versions : Control Expert 15.0 et suivantes.
- **Vérifié par le responsable de lot dans le texte des manuels :** disponibilité du PI_B et du PIDFF « + » sur M340, M580, Quantum, Momentum, Premium (33002535, tableau « Availability of the blocks », p. 28) — **disponibilité de la bibliothèque, pas compatibilité avec une CPU ou un micrologiciel précis (non documentés : « Conversion non qualifiée »)** ; **kp ou dband négatif : le bloc utilise la valeur 0** (avertissement), la sortie n'est pas arrêtée — le dépôt affirmait le contraire (`schneider.json:261`, `blocs-observes.md:37`, `pidff-conversion.md:24`, `block_adapters.py:42`) : **à corriger au LOT 7**.
- Constats de l'agent non encore recontrôlés : anti-emballement `aw_type` (le manuel et la FAQ 000279186 se contredisent), table des paramètres du PIDFF dont l'étiquette `rev_dir` est inverse de ses équations (p. 167/178), borne stricte −10000 < KP < +10000, tâche périodique exigée pour PID_INT, schéma « mixte » de la FAQ 000256113 générique (ne pas l'utiliser pour une conversion).
- **PI_B :** spécification suffisante pour un adaptateur documentaire PI (structure mixte, `ti > 0`, `en_rcpy = 0`, `dband = 0`), **mais** il manque l'unité de `kp`, l'ordre du limiteur, l'initialisation de Δ(DEV), la période réelle et un oracle indépendant ; adaptateur **non qualifié** et non implémenté.
- **« PIFINT »** : erreur de nomenclature du client ; **traité le 2026-11-27** (fiche retirée du catalogue, `qualification.md`, 2 tests et `schneider-m580.md` corrigés ; PIFINT reste refusé comme bloc inconnu).
- **Aucune compatibilité M340/M580 n'est établie pour une CPU/firmware donnés ; aucun banc.**
- Note : les SHA-256 des deux PDF avaient été notés inversés par erreur ; contrôle par MD5 publié.

## Avancées fonctionnelles après l'arbitrage du 2026-11-27 (branche `claude/science-engine-dev`)
CI : run 334 (962a6e5) vert ; run 335 (4a56868) vert ; run 339 et 343 en échec sur le test graphique du catalogue (cause : retrait de PIFINT, puis état vide devenu inatteignable — **couverture de ce contrôle réduite et déclarée** dans `tests/windows/catalogue.ps1`) ; run 344 (131280a) : 1re tentative en échec (l'application n'a pas affiché sa fenêtre, aucun `mkr.log`, **cause non démontrée**), **vert à la 2e tentative** (aléa du runner présumé, non prouvé).
- **Modèles exécutables des blocs Schneider** (documentaires, non qualifiés, `readyToLoad=False`) : `engine/block_pi_b.py` (19 tests), `block_pidff.py` (36), `block_pid_int.py` (23), chacun avec ses INTERPRÉTATIONS listées dans la docstring (le manuel est muet ou contradictoire : refusées, pas devinées). `test_block_conversion_closed_loop.py` vérifie adaptateur → modèle de bloc → boucle fermée contre un PI idéal RK4 ; la conversion PID_INT publie l'erreur statique de la troncature entière (`integerDeadZoneCounts`).
- **Broïda 1.2.0** : variation finale « établie » selon le bruit déclaré (`broida_not_established`), preuves pour le tracé des passages 28 %/40 % (niveaux, fenêtre de plateau, durée requise). Plancher 4 tau conservé.
- **Assistant d'essai (LOT 2, tranches 1 et 2)** : `engine/trial_assistant.py`, opération `assess_trial`, contrat **révision 3**. Autorégulant (Broïda provisoire, durée restante conditionnelle) et intégrateur sans plateau (rampe, pente significative à 99 %, aucune durée restante). `safetyAuthorization` toujours `false`. Dossier : `docs/science/lot2/assistant-essai-tranche1.md`.
- **Interface (partiel, compilé ici avec `-p:EnableWindowsTargeting=true`, jamais vu à l'écran)** : le graphique d'identification d'une étude Broïda trace, sur l'essai d'estimation, les niveaux à 28 % et 40 % (lignes) et les instants de passage (marqueurs « 28 % » / « 40 % »). `BroidaEvidence` gagne trois champs optionnels (études anciennes relues sans niveaux ; vérifié par le smoke test). **Aucune revue visuelle sur Windows réel** : à faire.
- **NON fait** : instructions et état de l'assistant d'essai dans l'interface WPF (section 42.6 non satisfaite) ; boucle fermée, grand temps mort, oscillant, réponse inverse dans l'assistant ; essai sur automate simulé ou réel ; LOT 1 restant (fiche d'étude, journal d'essai, statuts, invalidation en cascade) ; reclassement de Broïda en méthode du FOPDT (LOT 3).

## Preuves du LOT 0b
- CI « Vérification MKR » [run 331](https://github.com/Mcodea/mkr/actions/runs/45975387738), commit `f6c87f0` : **vert à la tentative 2** (compilation, 338 tests Python, smoke .NET, SQLite, Modbus/S7, paquet autonome, tests graphiques). La tentative 1 avait échoué sur `CheckInterruptedSession` (`AcquisitionChecks.cs:261`, `EndOfStreamException`) : **test intermittent**, code d'acquisition non modifié, cause non démontrée ; à stabiliser au LOT 2 (acquisition) sans affaiblir son assertion.
- Fiches : `docs/science/lot0b/`. Aucun test existant supprimé ni modifié.

## Derniers résultats de tests
- Suite Python locale : 338 tests, 313 sous-tests verts (commit `f6c87f0`) ; CI Windows : voir ci-dessus.
- **Arbitrages du 2026-11-27 reçus** : (1) TCLab : `tests/reference/tclab_dyn_data2_extract.csv` **retiré** du dépôt courant (provenance et licence documentées, test ignoré mais conservé, couverture synthétique équivalente `test_two_heater_unmodelled_input.py`) ; **historique Git non réécrit** (le fichier reste dans le commit `0a7407e` des branches publiées : voir `docs/science/licences/tclab-historique-git.md`, autorisation requise) ; recherche d'un jeu réel CC BY en cours. (2) « PIFINT » = erreur de nomenclature : blocs Schneider à étudier **PIDFF, PI_B, PID_INT** (sources officielles fournies par le client) ; recherche en cours dans `docs/science/schneider/`. (3) Section 42 intégrée.
- Le CI exécute encore `tests/reference/tclab_observed_step.py` sur un fichier cloné à l'exécution depuis `jckantor/CBE30338-book` (licence non établie, aucune copie dans le dépôt) ; équivalent synthétique ajouté.

## Règles à respecter à chaque session
Lire `AGENTS.md` et `docs/science/lot0/`. Ne pas refaire l'audit. Ne jamais fusionner dans `main`. Ne jamais affaiblir un test. Lecture seule sur les automates. Aucune déclaration de qualification ou de supériorité sans preuve. Opus pour les travaux scientifiques complexes, Sonnet pour le courant.
