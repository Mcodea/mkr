> **Annexe du LOT 0 — rapport d'audit indépendant (sous-agent, lecture seule), révision auditée c7915cd.** Les constats ont été vérifiés par échantillonnage par le responsable du lot (voir 00-synthese-audit.md, §1) ; tout constat non cité dans la synthèse reste à confirmer avant d'être retenu.

# Lot 0 · C — Données, architecture, contrats, traçabilité, lecture seule

Dépôt `/home/user/mkr`, branche `claude/science-engine-dev`, HEAD `c7915cd`, arbre propre (vérifié par `git status`).
L'audit s'est fait en lecture seule, sans commit. L'interface Windows n'a pas été lancée.

**Exécuté localement (Linux).** `python -m pytest engine tests` donne **350 tests réussis et 330 sous-tests réussis**, en 204 s, avec Python 3.13, numpy 2.3.5 et scipy 1.17.0. La CI utilise Python 3.12.

**Non exécuté.** Les tests .NET (Smoke, Communication, PhysicalLoop) n'ont pas été lancés : la compilation aurait écrit dans `obj/` et `bin/` du dépôt. Leur contenu a été lu.

**Convention.** **[F]** désigne un fait vérifié, avec `fichier:ligne`. **[H]** désigne une hypothèse ou une inférence non démontrée.

---

## 1. Cartographie : les 17 responsabilités du cahier des charges

| # | Responsabilité | Composants réels | État | Remarques |
|---|---|---|---|---|
| 1 | Projets / études | `Project` (`src/Mkr.Core/Projects.cs:3`), tables `projects`, `identification_studies`, `arx_studies`, `pi_studies`, `pi_simulations` (`SqliteProjectStore.cs:25-260`) | **Partiel** | Il n'existe pas d'entité « étude » distincte du projet : une étude est une ligne d'identification. Aucune suppression, aucun archivage, aucun renommage. Un projet correspond à une seule fiche de boucle (`LoopBrief`). |
| 2 | Acquisition | `PlcAcquisition`, `ModbusTcpReader`, `S7Reader`, `PlcDecoding` | **Existe** | Testée contre des stubs et des serveurs pymodbus/Snap7. Aucune CPU réelle. |
| 3 | Historisation | `HistorianRecorder` (segments archivés comme imports CSV, `AcquisitionArchive.cs:9`) | **Partiel** | Le stockage est un BLOB CSV par segment (≤ 5 000 mesures). Pas de base de séries temporelles, pas de requête par période, pas de rétention. La limite de 1 MiB par segment est fixée par `AcquisitionArchive.cs:30`. |
| 4 | Qualification des données | `engine/csv_quality.py` (comptages), `step_finder.py`, `fopdt.read_step`, `DiagnosticView` (verdict calculé dans l'interface) | **Partiel / insuffisant** | Voir §6. Le paquet `mkr_data_import/` (aberrants, saturation, signal figé, excitation, trous) est **orphelin** : il n'est importé que par `tests/`. |
| 5 | Identification | `fopdt`, `sopdt`, `integrator`, `broida`, `arx`, `closed_loop` | **Existe** | `identify_closed_loop` n'a **aucun appelant C#** (ADR 0006, expérimental). |
| 6 | Validation | `validation_gate.py`, `model_comparison.py`, `model_drift.py`, `study_reviews` | **Existe** | La décision est persistée (`IdentificationStudy.Assessment`). La revue humaine suit un automate à états (`StudyReview.cs`). |
| 7 | Stratégies | `strategy_selection.py` (`select_strategy`) | **Moteur seul** | Aucun appelant C# (grep dans `src` : 0 occurrence). |
| 8 | Réglages | `pi_tuning`, `pid_tuning`, `pi_tuning_integrator`, `recommend` | **Existe** | Seuls le réglage PI et ses simulations sont persistés en tables. Le réglage guidé, le PID SOPDT et le PI intégrateur ne laissent qu'une ligne de texte dans l'audit (`IdentificationWorkspace.cs:33-37`). |
| 9 | Optimisation | `recommend.py` (réglage guidé), `tuning_comparison.py` | **Partiel** | `compare_tunings` n'a aucun appelant C#. |
| 10 | Simulation | `pi_simulation`, `pid_simulation`, `open_loop_simulation`, `pi_mode_transition` | **Existe** | |
| 11 | Robustesse | `pi_robustness`, `pi_combined_robustness`, `stability_margins` | **Existe (partiel)** | Le résultat est persisté dans `PiSimulationStudy.Robustness`. |
| 12 | Catalogue | `engine/catalogue/schneider.json`, `block_catalogue.py`, `contracts/block-catalogue-v1.schema.json` | **Partiel** | Schneider seulement. L'interface lit le JSON **directement** (`BlockCatalogueWindow.xaml.cs:50`), sans passer par le moteur ni par le schéma. `lookup_blocks` et `recommend_blocks` n'ont aucun appelant C#. |
| 13 | Adaptateurs | `block_adapters.py` (PIDFF, PID_INT, PID_Compact), lecteurs automate | **Partiel** | Les conversions sont « non_qualified » et ne sont jamais prêtes à charger. Le client C# impose cet invariant (`PythonDiagnostics.cs:428-433`). |
| 14 | Mise au point | Relevé de banc textuel (`BlockCatalogueWindow`), simulation du transfert manuel/auto | **Absent / embryonnaire** | Il n'existe ni procédure assistée, ni comparaison mesure/simulation après réglage. |
| 15 | Rapports | `StudySheet.cs` (PDF) | **Partiel** | Le PDF couvre une seule chaîne : modèle, PI, simulation (`StudySheet.cs:14-20`). Il n'existe aucun rapport ARX, SOPDT-PID, réglage guidé, conversion de bloc ou qualité des données. |
| 16 | Qualification | `docs/validation/*`, `ReviewStage`/`ReviewState` | **Documentaire** | Aucun flux de qualification dans le code, en dehors de la revue par étape. |
| 17 | Orchestration du parcours | `StudyJourney.cs` | **Partiel / trompeur** | Voir §4. L'étape 3 « Analyse et qualification » passe à **Done dès qu'un import existe** (`StudyJourney.cs:35`). Les étapes 6 à 10 ne sont jamais Done (`:45-54`). |

**Composants orphelins** : implémentés et testés, mais non branchés sur le produit.
- `mkr_data_import/` entier, avec signal_quality, time_quality, transformations, signal_mapping et l'import Excel. [F] grep `mkr_data_import` : seuls `tests/*.py` l'importent. Il n'est pas empaqueté (`scripts/package-windows.ps1`).
- `PhysicalLoop` / `PhysicalLoopValidator` : plages physiques, contraintes et vitesses (`src/Mkr.Core/PhysicalLoop.cs`). [F] Aucun usage dans Desktop ni Infrastructure, aucune table SQLite. Les limites physiques ne servent donc pas à détecter les saturations.
- Opérations moteur sans appelant : `identify_closed_loop`, `select_strategy`, `compare_tunings`, `lookup_blocks`, `recommend_blocks`.
- `openpyxl` et `et-xmlfile` sont livrés dans le paquet Windows (`engine/requirements.txt`) mais ne servent qu'au code orphelin.

---

## 2. Contrats C# ↔ Python (IPC v1)

**Mécanique [F].**
- Un processus par requête : `python -I mkr_engine.py`, JSON sur stdin et sur stdout (`PythonDiagnostics.cs:791-855`).
- Enveloppe de requête `{version:1, requestId, operation, payload}`.
- Côté Python, les clés sont exactes, les clés dupliquées sont refusées, la taille est bornée à 2 900 000 octets et `requestId` est validé par une expression régulière (`mkr_engine.py:11-45`).
- Côté C#, la réponse doit avoir exactement 4 propriétés, `version == 1`, l'identique `requestId` et un `engineVersion` non vide (`PythonDiagnostics.cs:826-834`).
- Une exception Python devient `engine_failure`, et un NaN/Inf devient `engine_non_finite` (`mkr_engine.py:201-212`).

| Critère | Constat |
|---|---|
| Explicite | **Partiel.** `docs/communication/ipc-v1.md` décrit l'enveloppe et environ 15 opérations, puis s'arrête. **0 mention** de `identify_arx`, `recommend_tuning`, `check_model_drift`, `calculate_integrator_pi`, `simulate_pi_transition`, `simulate_pid_experimental`, `simulate_pi_combined`, `simulate_pi_robustness`, `convert_block_settings`, `identify_broida`, `select_strategy`, `compare_tunings`, `lookup_blocks`, `recommend_blocks` et `identify_closed_loop`. Il n'existe aucun schéma JSON des messages, sauf celui du catalogue. Le document annonce aussi un délai de 10 s ; le code applique 10 s ou 30 s selon l'opération (`PythonDiagnostics.cs:796-797`). |
| Versionné | **Partiel.** Le protocole est figé à `version: 1` alors que 15 opérations ou plus ont été ajoutées. Le moteur est versionné (`ENGINE_VERSION = "0.10.0"`, `mkr_engine.py:10`). Chaque résultat scientifique porte son `algorithmVersion` (contrôle de non-vacuité côté C#). En revanche, **le client ne vérifie jamais la compatibilité de version du moteur**, sauf pour la simulation PID, épinglée à `"0.2.0"` (`PythonDiagnostics.cs:578`). Le ping affiche la version sans la comparer (`WorkspaceViewModel.cs:88`). |
| Validé des deux côtés | **Oui, côté sémantique : point fort.** Python valide les clés exactes, les types et les bornes de chaque charge utile. C# revalide la cohérence de chaque réponse : décision ARX en cohérence avec les candidats, verdict de dérive avec les paramètres signalés, familles et constantes de temps, monotonie de l'aperçu, etc. (`PythonDiagnostics.cs:82-117, 129-141, 170-176, 241-265, 284-296`). Le garde `Guard` transforme toute réponse mal formée en `InvalidDataException` (`:779-789`). Le faux moteur `tests/fakes/mutating_engine.py`, avec des valeurs hostiles sur chaque feuille JSON, est exercé par Smoke (`tests/Mkr.Smoke/Program.cs:826-873`). |
| Résultat structuré | **Partiel.** <br>**Valeurs, avertissements, motifs de refus, critères, décision, version d'algorithme** : présents. <br>**Unités** : absentes des réponses IPC ; elles sont tenues côté C# (`IdentificationStudy.MvUnit/PvUnit`). <br>**Hypothèses et limites** (domaine d'emploi) : pas de champ structuré, seulement des codes d'avertissement. <br>**Erreurs** : un **code seul**, sans message, sans champ en cause ni contexte (`mkr_engine.py:188`). Une erreur sans `code` chaîne devient « Résultat Python absent » (`PythonDiagnostics.cs:836-840`). stderr est jeté (`:815`). <br>**Preuves** (points ajustés, `BroidaEvidence`, candidats) : présentes pour l'identification. |
| Robustesse du transport | La sortie standard est lue sans plafond de taille (`ReadToEndAsync`, `:816`). [H] C'est un risque mémoire en cas de moteur défaillant. Le processus est tué à l'expiration du délai (`:849-853`). |
| Divergence de contrat | Les motifs d'horodatage acceptés par le moteur (8, `engine/csv_format.py:129-132`) ne correspondent pas à la liste proposée côté C# (5, `src/Mkr.Core/ImportedData.cs:24-31`). |

---

## 3. SQLite : schéma, versions, migrations, traçabilité

**Schéma réel [F]** (`SqliteProjectStore.cs`) :

| Version | Contenu |
|---|---|
| v1 | `projects(id, name, created_at)` |
| v2 | Ajout de `description`, `csv_imports(id, project_id, file_name, sha256, imported_at, original BLOB, metadata JSON)` |
| v3 | `fopdt_studies` |
| v4 | Renommage en `identification_studies` |
| v5 | `pi_studies(model_id → identification)` |
| v6 | `pi_simulations(pi_id → pi)` |
| v7 | `study_reviews` (journal ajout seul, par séquence) |
| v8 | 4 colonnes de fiche de boucle sur `projects` |
| v9 | Reconstruction d'`identification_studies` : un CHECK autorise deux plages d'un même import (`:149-187`) |
| v10 | `audit_events`, chaîne SHA-256 et déclencheurs anti-UPDATE/DELETE (`:194-226`) |
| v11 | `arx_studies` (`:232-261`) |

Le versionnement repose sur `PRAGMA user_version`. Une version inconnue (au-delà de 11) est **refusée** (`:132-133`).

**Migrations.**
- Les étapes 0 → 8 sont faites dans une transaction unique (`:17-136`). Les étapes 9, 10 et 11 sont chacune dans leur propre transaction, idempotentes car la version est relue (`:137-139`).
- La migration 9 suit la procédure officielle (clés étrangères désactivées, copie, `foreign_key_check`, `:151-185`).
- **Aucune sauvegarde automatique avant migration.** Les commentaires renvoient à une « sauvegarde du projet » que le code ne crée pas (grep `backup|VACUUM INTO` : 0 occurrence) [F].
- Il n'y a pas de retour arrière : une version ancienne de MKR refuse une base plus récente.
- Les tests de compatibilité Smoke partent de bases v1 et v3 fabriquées à la main (`tests/Mkr.Smoke/Program.cs:54-70, 164-187`) et refusent `user_version = 99` (`:45`). Je n'ai pas trouvé de test partant d'une base v8, v9 ou v10 réelle. [H] La couverture passe par la chaîne de migration.

**Ce qui est conservé.**

| Élément | État |
|---|---|
| Données brutes | **Oui** : octets originaux et SHA-256, vérifiés à l'écriture (`:539`) et à **chaque relecture** (`:530-531`). |
| Configuration de lecture | **Oui** : délimiteur, format, correspondance des colonnes, provenance d'acquisition complète (cible, plan, statistiques, lacunes). Le tout est sérialisé en JSON dans `metadata` (`:680`). |
| Versions d'algorithmes | **Oui**, par résultat (`AlgorithmVersion`) et version du moteur (`EngineVersion`). |
| Résultats | **Oui** pour l'identification (y compris **refusée**, avec motifs et critères), l'ARX (y compris refusé), le PI, la simulation et la robustesse. **Non** pour le réglage guidé, le PID SOPDT, le PI intégrateur, les conversions de bloc, les rapports de dérive, la recherche d'essais et la simulation en boucle ouverte : il n'en reste que du texte d'audit, ou rien. |
| Historique sans écrasement | **Oui** pour les tables scientifiques (INSERT uniquement ; aucun UPDATE ni DELETE dans `src`, grep). **Non** pour la fiche de boucle et la description : `UPDATE projects …` écrase (`:465, :492`). L'audit `loop.brief.saved` ne garde que le repère, la grandeur et l'objectif, pas l'ancienne valeur complète (`CsvWorkspace.cs:39`). |

**Écarts de traçabilité par rapport à l'exigence.**

| Exigence | État |
|---|---|
| Identifiant projet / étude | Oui (GUID). |
| Origine des données | Oui : fichier, empreinte, cible automate, horodatage `receiver_utc_midpoint`. |
| Transformations | Aucune transformation n'est appliquée (bien), mais seules les plages de lignes sont tracées. Le module `transformations.py` est orphelin. |
| Versions d'algorithmes | Oui. |
| Versions de **bibliothèques** | **Absentes**. Ni numpy, ni scipy, ni Python, ni l'OS ne sont enregistrés dans les études (grep `__version__` dans `engine` : 0). Seul le manifeste de paquet contient la version de Python (`scripts/package-windows.ps1:11,49`). |
| Configuration scientifique | Tolérances MV/PV et critères oui. Les bornes du solveur sont implicites (codées dans le moteur). |
| Modèles acceptés **et rejetés** avec raisons | Oui pour l'identification et l'ARX (`Comparison.Candidates[*].RejectionReasons`, `Assessment.Reasons`, `ArxCandidate.Reasons`). |
| Contraintes | **Absentes** : `PhysicalLoop` n'est pas persisté. Seules les bornes MV des scénarios sont gardées. |
| Références scientifiques | **Absentes** des enregistrements. Elles figurent dans `docs/science`. |
| Limites et domaine | Codes d'avertissement seulement. |
| Statut final | Partiel : `study_reviews` (Calculated → Verified → Approved/Rejected) existe, mais `run_id` n'a pas de clé étrangère et `pi_studies`/`pi_simulations` n'ont pas de `project_id` (le rattachement se fait par jointure). |

L'audit est une chaîne SHA-256 avec export JSONL vérifiable hors MKR (`Audit.cs`). C'est un point fort.

---

## 4. Invalidation des résultats dépendants

**Non implémentée** [F] : grep `obsol|stale|périm|superseded` ne trouve qu'un commentaire d'interface (`IdentificationView.xaml.cs:307`). Les tables n'ont ni colonne de statut ni graphe de dépendances.

La conception compense en partie par l'**immuabilité** :
- un import n'est jamais modifié, et une nouvelle donnée crée un nouvel import ;
- chaque résultat référence son parent (`training_id`/`validation_id` → `model_id` → `pi_id`) par clé étrangère.

Il n'existe donc pas d'« obsolescence par modification de la donnée ». Restent trois trous :
1. Un changement de la fiche de boucle (UPDATE) ne marque rien. Le PDF le dit (« au moment de l'export », `StudySheet.cs:51`).
2. Un **nouvel algorithme ou moteur** ne signale jamais qu'une étude ancienne a été calculée avec une version antérieure ou corrigée.
3. Un **rapport de dérive** (`check_model_drift`) qui conclut « drift » ne change pas le statut du modèle, ni de ses réglages ou simulations. Le modèle reste « accepté » (`IsAcceptedForTuning`, `Identification.cs:50-52`), et le rapport n'est pas persisté.

Le parcours (`StudyJourney`) repose sur des **comptes** (`ProjectFacts(described, imports, studies, accepted)`, `WindowView.xaml.cs:299-301`) et non sur une chaîne de dépendances :
- l'étape 1 est « Done » dès qu'un seul champ de la fiche est non vide (`:300`) ;
- l'étape 3 est « Done » sans aucune décision de qualité (`StudyJourney.cs:35`).

---

## 5. Sécurité en lecture seule : preuve par le code

**Modbus TCP** (`src/Mkr.Infrastructure/Plc/ModbusTcpReader.cs`) :
- Une seule construction de trame, `ReadRegistersAsync` (`:64-90`). Le code fonction est `Table == HoldingRegisters ? 0x03 : 0x04` (`:66`). Un enum hors domaine donne donc 0x04, jamais une autre valeur.
- L'unique écriture réseau est `stream.WriteAsync(request)` (`:75`), avec la requête MBAP de 12 octets construite aux lignes `:68-74`.
- `EmittedFunctionCodes = {0x03, 0x04}` (`:14`) est **déclaratif** : il n'existe pas de garde à l'exécution, contrairement au client S7.
- Il n'existe aucun code FC05, 06, 0F, 10, 16, 17 ou 2B.

**S7comm** (`src/Mkr.Infrastructure/Plc/S7Reader.cs`) :
- Trames émises : demande de connexion COTP 0xE0 (`:46-50`), `Job(0xF0)` setup (`:57`), `Job(0x04)` lecture de variable (`:86`).
- `Job()` **refuse à l'exécution** toute fonction hors `EmittedFunctions = {0xF0, 0x04}` (`:17, :122`).
- Les seules écritures réseau sont `WriteTpktAsync` → `stream.WriteAsync` (`:169`), appelées uniquement avec ces trois charges.
- Il n'existe ni 0x05 (écriture), ni 0x28/0x29 (contrôle et arrêt de la CPU), ni 0x1A à 0x1F (chargement et téléversement de blocs).

**Absence d'autre chemin réseau [F].**
- grep `TcpClient|Socket|NetworkStream|HttpClient|WriteAsync(` dans `src` ne trouve que ces deux lecteurs, `PlcReaders.ConnectTcpAsync` (`PlcReader.cs:40-60`) et stdin vers Python.
- La fabrique `PlcReaders.Create` ne connaît que ces deux classes (`PlcReader.cs:33-38`).
- Le Desktop n'instancie que `PlcAcquisition` et `HistorianRecorder` (`AcquisitionWindow.xaml.cs:248, 293, 366`).
- Le moteur Python n'importe aucun module réseau (grep `socket|urllib|requests` : 0). pymodbus et python-snap7 ne sont que dans `tests/interop/requirements.txt`, ils ne sont pas livrés.

**Tests qui démontrent la lecture seule.**
- `AssertReadOnlyModbus` inspecte **chaque trame reçue** par le stub, contre la liste et contre les littéraux 3 et 4 (`tests/Mkr.Communication.Tests/Program.cs:50-55`). Il est appelé 4 fois.
- Le test S7 nominal vérifie que toutes les fonctions reçues sont dans {0xF0, 0x04} (`:404-416`).

**Limites des preuves.**
- La vérification sur les trames ne couvre que les scénarios où l'assertion est appelée. Le test d'interopérabilité pymodbus/Snap7 vérifie les valeurs lues, **pas les trames ni l'invariance de la mémoire du serveur** (`:574-611`).
- Il n'existe pas de test statique, du type « aucun autre site d'écriture réseau » ou analyse de l'assembly.
- Il n'y a pas de capture réseau sur banc (reconnu par `docs/communication/acquisition.md`).
- Une connexion S7 de type PG et l'activation de PUT/GET affaiblissent la protection de la CPU : c'est un risque d'exploitation, pas d'écriture (ADR 0003).

**Verdict.** La lecture seule est **démontrée par lecture du code et testée sur simulateurs**. Elle n'est **pas qualifiée sur banc**.

---

## 6. Qualité des données : contrôles existants et exigence du §11

### 6.1 Contrôles effectivement branchés au produit

| Contrôle | Où | Seuil codé en dur | Justification |
|---|---|---|---|
| Taille et lignes du CSV | `csv_quality.py:8-9` | 1 048 576 o ; 10 000 lignes | Limite technique, non scientifique |
| En-têtes | `csv_quality.py:24` | 1 à 64 colonnes, nom ≤ 120 caractères, unicité | Technique |
| Unité | `csv_quality.py:47` | ≤ 40 caractères | Technique |
| Horodatages invalides, en double, à rebours | `csv_quality.py:58-72` | Comptages | Descriptif, sans seuil |
| Pas d'échantillonnage | `csv_quality.py:88-89` | min et max du pas positif | Descriptif |
| Valeurs manquantes, invalides, min/max | `csv_quality.py:73-83` | — | Sur **une seule colonne** (`valueColumn`) |
| Pas irrégulier (verdict) | **WPF** `DiagnosticView.xaml.cs:72, :121` | `max > 1.5 × min` | **Non justifié, codé dans l'interface, non versionné, non persisté** |
| Heure locale inexistante ou ambiguë | `csv_format.py:166-176` | Refus | IANA, justifié |
| Ligne de base stable (conditions initiales) | `fopdt.py:74, :83-84` | ≥ 4 points avant l'échelon ; écart ≤ `pvTolerance` | Tolérance déclarée par l'utilisateur |
| Échelon franc (excitation) | `fopdt.py:70, :78-81` | saut > 2·tolMV ; amplitude > 10·tolMV ; plateaux ≤ tolMV | Non justifié dans le code |
| Réponse significative | `fopdt.py:85-86` | > 4·tolPV | Non justifié |
| Fenêtre de réponse | `fopdt.py:74` ; `step_finder.py:121` | ≥ 12 points | Non justifié |
| Horizon de réponse | `fopdt.py` (bloc `identify`, ≈ l. 257-258) | `< 4τ` donne `short_horizon`, bloquant (`validation_gate.py:18-21`) | 4τ ≈ 98 %, conventionnel |
| Valeur absurde | `fopdt.py:32` ; `trial_preview.py:72` | \|x\| > 1e100 | Technique |
| Rampe ou changements multiples | `step_finder.py:102-103` | sauts sur échantillons consécutifs | Heuristique |
| Base trop courte ou instable | `step_finder.py:24, :112-119` | 4 ≤ base ≤ 60 points | Non justifié |
| Réponse stabilisée | `step_finder.py:137-140` | queue = max(6, n/8) ; ptp ≤ 2·tolPV | Non justifié |
| Rapport signal/bruit faible | `step_finder.py:141-142` | < 5·σ de la base | Non justifié (avertissement) |
| Paires d'essais | `step_finder.py:28-29, :162-164` ; `fopdt.py:245` | ratio d'amplitude 0,5 à 2 ; base partagée 8 points | Non justifié |
| Limites de recherche | `step_finder.py:25-26` | 200 candidats, 20 paires | Technique |
| Mode manuel (MANUAL) | `step_finder.py:65-67, :86-87` | `MANUAL == 1` | Explicite ; **seulement dans find_steps** |
| Échantillonnage uniforme (ARX) | `arx.py:38, :72-73` | gigue relative 1e-3 | Commenté |
| Excitation (ARX) | `arx.py:74-75` | ptp(MV) > 10·tolMV | Non justifié |
| Durée minimale (ARX) | `arx.py:67` | ≥ 40 échantillons | Non justifié |
| Bruit, critère proposé | `validation_gate.py:15` | RMSE ≤ 2σ | Commenté (« proche de σ ») |
| Blancheur des résidus | `fopdt.py:103-108` | 2,576/√N | Référencé (Ljung 1999) |
| Acquisition : période | `PlcDecoding.cs:95` ; `Acquisition.cs:143` | 100 ms à 60 s | Charge automate |
| Acquisition : coupure tolérée | `PlcDecoding.cs:96` (≤ 600 s) **contre** `Acquisition.cs:148` (≤ 3 600 s) | **Incohérence** : un plan historien de 601 à 3 600 s, validé par `HistorianPlan.Validate`, est ensuite rejeté par `PlcDecoding.Validate` dans `HistorianRecorder.cs:172-174` (exception) |
| Acquisition : délai | `PlcReader.cs:31` | 5 s | Commenté |
| Valeurs non finies (automate) | `PlcReader.cs:93-95` | Lacune de qualité | Justifié |

### 6.2 Contrôles demandés, état par contrôle

| Contrôle | État dans le produit |
|---|---|
| Horodatages | **Oui** (descriptif). |
| Pas d'échantillonnage | **Oui** (min/max). Le verdict d'irrégularité est dans l'interface. |
| Manquants | **Oui**, sur une colonne. La MV et la SP ne sont jamais qualifiées à l'import : l'acquisition n'analyse que PV (`AcquisitionArchive.cs:31`). |
| Aberrants | **Non** dans le produit (orphelin : `signal_quality.py`). |
| Bruit | **Partiel** : σ de la base dans l'identification et la recherche d'essais. |
| Saturations | **Non** dans le parcours ouvert. Seul le module en boucle fermée, non exposé, le traite (`closed_loop.py:26, :147`). `PhysicalLoop` n'est pas utilisé. |
| Blocages de signal (valeur figée) | **Non** dans le produit (orphelin). L'acquisition compte une valeur répétée comme valide. |
| Changements de mode | **Partiel** : colonne MANUAL, seulement dans `find_steps`. `fopdt.read_step` et `arx` **ignorent MANUAL** (grep : MANUAL seulement dans `step_finder.py` et `closed_loop.py`). Une plage choisie à la main peut donc contenir des lignes en automatique. |
| Excitation | **Partiel** : seuils de tolérance ci-dessus. |
| Boucle ouverte ou fermée | **Déclaratif** : le client envoie toujours `loop = "open"` à l'ARX (`PythonDiagnostics.cs:104`). Le refus `arx_closed_loop` est donc **inatteignable** depuis l'application. Il n'y a pas de détection de corrélation de la MV avec la PV. |
| Conditions initiales | **Oui** (base stable). |
| Durée exploitable | **Partiel** (12 points, 4τ, 40 échantillons). |

### 6.3 Verdict à cinq niveaux du §11 (exploitable / sous conditions / à compléter / insuffisant / bloqué)

**Absent [F].** L'interface affiche un binaire, « aucune anomalie de comptage » en vert ou « N constats à traiter » (`DiagnosticView.xaml.cs:76-77`). Ce binaire n'est ni calculé par le moteur, ni versionné, ni persisté.

Le parcours marque la qualification « Done » sans verdict (`StudyJourney.cs:35`).

La seule notion d'« exploitabilité » réelle est **implicite**, essai par essai, au moment de l'identification (refus `fit_*`). Elle n'est jamais rattachée à l'import comme statut.

---

## 7. Acquisition : progression, disponibilité, exploitabilité

- **Progression temporelle [F]** : `AcquisitionProgress` porte l'état, les mesures reçues sur demandées, les manquées, le temps écoulé et le **temps restant**. Ce dernier vaut (mesures non planifiées) × période (`PlcAcquisition.cs:37-39`). La durée prévue est affichée avant le lancement (`Acquisition.cs:89, :138` ; `AcquisitionWindow.xaml.cs:120, :126`).
- **Disponibilité des données [F]** : mesures manquées, lacunes horodatées avec leur cause, reconnexions, aller-retour moyen et maximal, retard maximal (`PlcAcquisition.cs:41-57, :143-145`).
  - [H] Les lacunes sont datées sur l'horloge planifiée (`startUtc + index·period`, `:44`), alors que les mesures sont datées sur l'horloge murale (`DateTimeOffset.UtcNow`, `:89, :99`). Une correction NTP sur 23 jours d'historique peut désaligner les deux.
  - Dans `HistorianRecorder`, les « reconnexions » d'un segment sont estimées par le nombre de lacunes hors `overrun` (`HistorianRecorder.cs:213`). Ce n'est pas le même nombre.
- **Fraîcheur [F]** : seul le dernier aller-retour est connu. Il n'existe pas d'âge de la dernière valeur côté automate, ni de détection de valeur figée ou d'horodatage source. Une sonde bloquée qui répond reste « valide ».
- **Exploitabilité scientifique** : seul `SamplesNotInManual` est signalé à l'arrêt (`AcquisitionWindow.xaml.cs:323, :393`). Pendant l'acquisition, rien ne vérifie l'excitation, la stabilisation, la saturation ou le rapport signal/bruit.
- **Estimation de la durée d'acquisition nécessaire** (fondée sur τ et θ attendus et sur le nombre d'échelons) : **absente**. Seule la durée planifiée est calculée.

---

## 8. Dépendances tierces et licences

| Composant | Version | Licence | Usage |
|---|---|---|---|
| Microsoft.Data.Sqlite.Core | 10.0.8 | MIT | Stockage |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.5 (SQLite 3.53.4 résolu) | Apache-2.0 | Stockage |
| PDFsharp-MigraDoc | 6.2.4 | MIT | PDF |
| System.Security.Cryptography.Pkcs | 10.0.13 | MIT | **Aucun usage dans `src`** (grep). [H] Épinglé pour l'audit NuGet d'une dépendance transitive. |
| Transitives | Microsoft.Extensions.*.Abstractions 8.0.x | MIT | `project.assets.json` |
| numpy | 2.3.5 | BSD-3 [H : roues avec OpenBLAS et libgfortran, exception d'exécution GPL] | Moteur |
| scipy | 1.17.0 | BSD-3 [idem] | Moteur |
| openpyxl / et-xmlfile | 3.1.5 / 2.0.0 | MIT | **Code orphelin uniquement**, mais livré |
| tzdata | 2025.2 | Apache-2.0 | Fuseaux |
| pymodbus | 3.8.6 | BSD | Tests seulement |
| python-snap7 | 2.0.2 | MIT (bibliothèque Snap7 en LGPL-3.0) | Tests seulement |

Toutes les versions sont épinglées. Les cibles sont `net10.0` et `net10.0-windows`. Le projet impose `TreatWarningsAsErrors` et `NuGetAuditMode=all` (`Directory.Build.props`). La CI utilise Python 3.12 ; l'exécution locale ici s'est faite en 3.13. La mention des licences tiers dans l'interface est générique (`Localization.cs:77`). [H] Aucun fichier de notices tierces n'a été trouvé dans le paquet ; non vérifié.

---

## 9. Règles scientifiques dupliquées dans les vues WPF (`src/Mkr.Desktop`)

1. **`DiagnosticView.xaml.cs:72, :121`** : règle « pas irrégulier si max > 1,5 × min » et verdict de qualité dans la vue. C'est une règle scientifique, non versionnée.
2. **`TrendReader.cs`** : **second analyseur CSV** dans l'interface.
   - Découpage naïf, sans guillemets.
   - `DateTimeOffset.TryParse(InvariantCulture, AssumeUniversal)` (`:50`) **ignore le motif et le fuseau déclarés** (`CsvFormat.Time`). Pour un export français `jj/mm/aaaa` heure locale, l'axe et les marqueurs d'événements de l'interface peuvent différer de la lecture du moteur. [H] Cela peut aller jusqu'à l'inversion jour/mois ou à un décalage de fuseau.
   - Seuils de 90 % et 50 % (`:58, :72`).
   - « Période d'échantillonnage » calculée comme médiane dans l'interface (`:9-19`, affichée à `CsvView.xaml.cs:322`). Le moteur, lui, rapporte min et max.
3. **`IdentificationView.xaml.cs:864-886, :952-956`** : FIT (NRMSE), RMSE, R² et IAE recalculés dans l'interface sur les points stockés, et sur une plage A–B. Ces calculs sont documentés comme indicateurs d'affichage, mais ils s'affichent à côté de la RMSE du moteur et peuvent en différer.
4. **`BlockCatalogueWindow.xaml.cs:50`** : lecture directe du catalogue JSON, qui contourne le moteur et le schéma.

---

## 10. Tests : inventaire et tests auto-référents

**Inventaire.**
- Python : 318 `def test_` dans `engine/` et 32 dans `tests/`. Résultat local : 350 réussis.
- .NET Smoke : 159 `Require` (`tests/Mkr.Smoke/Program.cs`, `AcquisitionChecks.cs`).
- Communication : environ 40 `Check`, avec interopérabilité pymodbus et Snap7 si `MKR_INTEROP_PYTHON` est défini.
- PhysicalLoop : 53 lignes. Il teste un **modèle orphelin**.
- CI Windows : `.github/workflows/verify.yml`, avec scripts UI Automation sur le paquet autonome.

**Oracles indépendants (bons).**
- `test_csv_quality.py:23-32` : comptages calculés à la main.
- Décodage IEEE 754 et complément à deux sur constantes.
- Serveurs pymodbus et Snap7 tiers.
- Vérificateur d'audit Python indépendant.
- Données TCLab réelles (refus attendu, CI).

**Auto-référents ou faibles.**
- Les tests de `mkr_data_import` (`tests/test_signal_quality.py`, `test_time_quality.py`, `test_transformations.py`, `test_signal_mapping.py`, `test_excel_import.py`) valident du code **non branché**. Ils ne prouvent rien sur le produit.
- `test_synthetic_fixtures.py` régénère les fixtures avec le même générateur, puis compare à l'identique (non-régression, sans oracle). Il passe par l'import orphelin.
- `Mkr.PhysicalLoop.Tests` teste un modèle sans usage.
- La vérification de lecture seule s'appuie en partie sur l'ensemble déclaré par le code lui-même (`EmittedFunctionCodes`). Les littéraux 3/4 et F0/04 la rendent tout de même indépendante.
- `test_step_finder.py:67` : les fenêtres listées sont acceptées par les estimateurs (même lecteur `read_step`). C'est une cohérence, pas une exactitude.
- `GetVersionAsync == "0.10.0"` (Smoke `:52`) : valeur épinglée.

**Absents.**
- Test de qualité sur MV et SP.
- Test MANUAL sur `identify_*` avec plage manuelle.
- Test d'incohérence de la coupure tolérée de l'historien.
- Test de migration depuis une base v9 ou v10 réelle.
- Test d'absence de chemin d'écriture par analyse statique.

---

## 11. Classement des statuts (selon AGENTS.md §8)

| Élément | Implémenté | Compilé | Testé auto | Vérifié indépendamment | Qualifié |
|---|---|---|---|---|---|
| Import CSV, empreinte, relecture vérifiée | Oui | CI (non relu ici) | Oui | Partiel (comptages calculés à la main) | Non |
| Qualité des données (verdict) | Partiel (interface) | — | Non (règle 1,5×) | Non | **Non qualifié** |
| `mkr_data_import` | Oui | n/a | Oui | Partiel | **Non branché** |
| Recherche d'essais et MANUAL | Oui | — | Oui | Synthétique | Non |
| Contrat IPC v1 | Oui | — | Oui (faux moteur hostile) | Partiel | Non (doc incomplète) |
| SQLite v1 → v11 | Oui | — | Oui (v1 et v3 vers v11) | Non | Non (pas de sauvegarde) |
| Audit chaîné | Oui | — | Oui | Oui (vérificateur Python) | Non (revue de sécurité externe) |
| Invalidation des dépendances | **Non** | — | — | — | — |
| Lecture seule Modbus / S7 | Oui | — | Oui (stubs, interop) | Oui (pymodbus, Snap7) | **Non qualifié sur banc** |
| Historien | Oui | — | Oui | Partiel | Non (bug de coupure > 600 s) |
| Parcours `StudyJourney` | Oui | — | Oui | Non | Trompeur à l'étape 3 |
| Rapport PDF | Partiel | — | Oui (nombre de pages) | Non | Non |
| Catalogue / adaptateurs | Partiel | — | Oui | Documentaire | **Non qualifié** (assumé) |

---

## 12. Risques et actions priorisées

### P0 (bloquants pour la crédibilité scientifique et la sécurité)

1. **Absence de verdict de qualification à cinq niveaux**, et étape 3 « Done » sans décision (`StudyJourney.cs:35`). Il faut un verdict calculé par le moteur, versionné, persisté avec l'import, et porteur de motifs.
2. **Règles de qualité dans l'interface** (`DiagnosticView.xaml.cs:72`, `TrendReader.cs`). Il faut les déplacer dans le moteur et supprimer le second analyseur CSV. Celui-ci ignore le format et le fuseau déclarés.
3. **Saturation, valeur figée, aberrants et changements de mode non contrôlés dans le parcours**. `read_step` et `arx` ignorent MANUAL. Le paquet `mkr_data_import`, qui fait ces contrôles, est orphelin. Il faut décider : l'intégrer par ADR, ou le supprimer.
4. **ARX : le client force `loop="open"`** (`PythonDiagnostics.cs:104`). Le refus en boucle fermée est inatteignable. Il faut une déclaration explicite de l'utilisateur, ou une détection (MANUAL ou corrélation).
5. **Aucune invalidation ni signalement de dérive**. Un modèle en « drift » reste accepté pour le réglage. Il faut au minimum persister le rapport de dérive et bloquer ou avertir.

### P1

6. **Documentation IPC incomplète** : 15 opérations ou plus sans contrat écrit. Il faut un schéma JSON par opération, un message ou champ d'erreur structuré, et une vérification de compatibilité de version du moteur côté client.
7. **Pas de sauvegarde avant migration SQLite**, alors que la doc y renvoie. Il faut un test de migration depuis v8, v9 et v10.
8. **Versions de bibliothèques** (numpy, scipy, Python) absentes des études. Les résultats du réglage guidé, du PID SOPDT, du PI intégrateur, des conversions, de la dérive et de la boucle ouverte ne sont pas persistés.
9. **Incohérence de la coupure tolérée** : 3 600 s côté historien, 600 s côté décodage (`Acquisition.cs:148` contre `PlcDecoding.cs:96`). L'erreur apparaît à l'exécution.
10. **Fiche de boucle écrasée par UPDATE**, sans historique. `PhysicalLoop` (contraintes, plages) n'est pas persisté ni utilisé.
11. **Lecture seule** : ajouter une garde d'exécution Modbus symétrique à S7 (`Job`), un test d'interopérabilité qui audite les trames et l'invariance de la mémoire, et une capture Wireshark sur banc.

### P2

12. Fraîcheur (âge, valeur figée) et estimation de la durée d'acquisition nécessaire.
13. Désalignement possible entre l'horloge planifiée et l'horloge murale pour les lacunes. Reconnexions estimées par segment.
14. Plafond de taille sur la sortie standard du moteur. Conserver stderr pour le diagnostic.
15. Harmoniser les motifs d'horodatage C# et Python (5 contre 8).
16. Dépendances livrées sans usage (openpyxl, et-xmlfile, Pkcs). Notice des licences tierces dans le paquet.
17. `pi_studies` et `pi_simulations` sans `project_id`. `study_reviews.run_id` sans clé étrangère.
18. Rapport PDF limité à la chaîne FOPDT, PI et simulation. Pas de section sur la qualité des données ni sur les versions de bibliothèques.
