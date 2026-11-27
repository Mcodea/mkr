# LOT 0 — Audit scientifique initial : synthèse

> **Mise à jour 2026-11-27 :** P0-1, P0-2, P0-3, P0-4 et P0-7 ont été traités au LOT 0b (`../lot0b/`) ; P0-6 est reporté au LOT 2 ; P0-5 et P0-8 relèvent des LOT 1 et 2. Le texte ci-dessous décrit l'état **audité** (`c7915cd`).

**Révision auditée :** `c7915cd` (révision UI/UX réceptionnée ; tag local `ui-ux-recue-2026-11-26`, push du tag à retenter).
**Branche de travail :** `claude/science-engine-dev` (aucune fusion dans `main`).
**Nature du document :** état des lieux factuel et proposition de plan. **Aucun algorithme n'a été modifié.** Le plan n'est pas adopté tant qu'il n'est pas arbitré (AGENTS.md §2).

## 1. Méthode et limites de l'audit

- Cinq audits indépendants menés en lecture seule par des sous-agents (modèle Opus 5.5) : (A) identification et validation, (B) réglage, simulation, robustesse, (C) qualification des données, architecture C#/Python/SQLite, sécurité lecture seule, (D) catalogue constructeurs et rapports, (E) référentiel bibliographique. Rapports complets en `annexes/`.
- **Vérifications faites par le responsable du lot (reproduction ou relecture du code) :**
  - A : erreur-type du gain FOPDT sous-estimée (rapport 0,49 sur 56 tirages), paramètres aberrants du modèle sélectionné sur un intégrateur (pôle 0,999998, τ ≈ 810 000 s) ; la décision d'acceptation de l'ARX **n'a pas été relue**.
  - B : Ms annoncé 1,369 contre 1,432–1,439 sur le second ordre vrai (règle de la moitié) ; temps mort simulé exactement (erreur 8,9e-16).
  - C : étape 3 du parcours « Qualification » marquée faite dès qu'un import existe (`StudyJourney.cs:35`) ; `loop = "open"` forcé (`PythonDiagnostics.cs:104`) ; coupure tolérée 3 600 s contre 600 s (`Acquisition.cs:148`, `PlcDecoding.cs:96`).
  - D : la compatibilité du catalogue traite un champ vide comme compatible (`block_catalogue.py:29`) et la cible envoyée par C# est vide (`PythonDiagnostics.cs:421, 443`).
- **Non vérifié par le responsable** : le reste des constats des annexes (ils sont donnés avec fichier:ligne par les auditeurs) ; les tests .NET (lus, non exécutés) ; tout ce qui exige Windows, un automate de banc ou une version constructeur.
- **Régressions écartées** : `engine/` est identique avant et après la phase UI ; les changements de `Core`/`Infrastructure` depuis la phase UI sont des textes localisés. Les 350 tests Python de l'auditeur C et les 318 de la CI passent.
- **Références bibliographiques : aucune n'est vérifiée à la source** (le réseau de la session bloque doi.org, les éditeurs, NTNU, KU Leuven, IEC). Voir `05-referentiel.md`.
- Le cahier des charges directeur et la spécification MKR-SPEC-001 **ne sont pas dans le dépôt** (seuls ses §§1-19 sont tracés) : les écarts sont mesurés contre les exigences internes du dépôt et contre le cahier fourni en conversation. À confronter au texte original.

## 2. Ce qui existe et fonctionne réellement

| Domaine | État constaté |
|---|---|
| Simulation numérique | Temps mort simulé exactement (bloqueur d'ordre zéro, y compris fractionnaire) ; simulateurs PI/PID comparés à `scipy` (`dlsim`, `cont2discrete`) ; marges vérifiées en forme fermée. **Vérifié indépendamment.** |
| Identification FOPDT / SOPDT / intégrateur | Équations et estimation vérifiées sur réponses analytiques **sans bruit**. Refus corrects : oscillant, retard excessif, dérive, perturbation, saturation de MV, quantification grossière. |
| Porte de validation | Séparation estimation / validation indépendante, refus du modèle non accepté (côté C#). |
| Acquisition industrielle | Strictement lecture seule, **prouvé par le code** : Modbus n'émet que FC03/04 ; S7 que COTP CR, 0xF0, 0x04 avec refus à l'exécution de tout autre code. |
| Stockage | SQLite v1→v11, migrations transactionnelles, octets originaux avec SHA-256 revérifié à la lecture, modèles acceptés et refusés conservés avec motifs, audit chaîné. |
| Rapport PDF | Distingue correctement mesuré et simulé ; empreintes des essais. Limité aux études PI FOPDT. |
| Catalogue de blocs | 3 adaptateurs (PIDFF, PID_INT, PID_Compact V2.0–V3.0), tous « non qualifiés » et jamais « prêts à charger ». |

**Aucun module n'atteint le niveau « Qualifié dans un domaine »** (voir `02-matrice-qualification.md`).

## 3. Défauts bloquants (P0) — validité scientifique

Ils ne sont pas corrigés ici (LOT 0 : audit seulement). `AGENTS.md` §2 autorise la correction urgente d'un défaut de validité scientifique : **je propose de les traiter dans un lot de corrections P0 avant le LOT 1, à arbitrer** (§ 4 du document `03`).

| # | Défaut | Preuve | Annexe |
|---|---|---|---|
| P0-1 | **Erreur-type du gain FOPDT sous-estimée d'un facteur ≈ 2** (plateau initial non propagé, alors que SOPDT et intégrateur le font) ; elle alimente l'incertitude du réglage guidé. | Reproduit (rapport 0,49 ; 1,06 avec le terme de plateau) | A §1 |
| P0-2 | **ARX accepte un intégrateur comme premier ordre** (pôle 0,999998, τ ≈ 810 000 s, K = 20 306 ± 55 636). Seul un refus en aval (`recommend`) empêche un réglage. | Reproduit (valeurs) ; décision `accepted` non relue | A §5 |
| P0-3 | **Robustesse optimiste du réglage guidé** : (a) règle de la moitié jugée sur le modèle réduit ; (b) échantillonnage représenté par h/2 de retard, Ms échantillonné supérieur à la limite pour θ = 0 ; (c) la période transmise est la cadence des données, pas celle du régulateur. | (a) reproduit ; (b)(c) rapport B + `PythonDiagnostics.cs:322` | B §P0 |
| P0-4 | **Refus d'un modèle non validé uniquement côté C#** ; le moteur accepte n'importe quels paramètres ; un modèle en dérive reste utilisable. | Rapport B/C | B, C |
| P0-5 | **Aucun verdict de qualification des données** (5 niveaux, §11) ; l'étape « Qualification » du parcours passe à *faite* dès qu'un import existe ; seuil « irrégulier » calculé dans l'interface (`DiagnosticView.xaml.cs:72`). | Vérifié (StudyJourney) | C §3 |
| P0-6 | **Mode manuel et boucle fermée ignorés** : `loop="open"` forcé, MANUAL ignoré par FOPDT et ARX ; le refus de boucle fermée ne peut pas se déclencher depuis l'application. | Vérifié (PythonDiagnostics) | A, C |
| P0-7 | **Compatibilité constructeur non contrôlée** : champ vide = compatible ; cible vide envoyée par C# ; conversions « non qualifiées » mais sans vérification de CPU, firmware, version. | Vérifié | D §2 |
| P0-8 | **Aucune invalidation des résultats dépendants** (donnée modifiée ⇒ identification, réglage, simulation obsolètes). | Rapport C | C §7 |

## 4. Consolidations importantes (P1, résumé)

- Broïda exposé comme famille de modèles (`ModelFamily.BroidaFOPDT`) au lieu d'une méthode d'estimation du FOPDT ; migration de stockage à prévoir.
- Pas d'AIC/AICc/BIC ; aucun ADR ne justifie leur absence. Sélection ARX sur la série de validation (RMSE publiée optimiste).
- Boucle fermée (`closed_loop.py` 0.1.0) non conforme à l'ADR 0006 et absente de l'interface ; détection de rétroaction de l'ARX annoncée en docstring mais inexistante.
- Pas de vrai front de Pareto, trois préférences réduites à une limite de Ms ; P seul et I seul jamais proposés ; limites d'actionneur et vitesse de sortie absentes du réglage guidé ; pas de comparaison avec le réglage existant ; **aucune détection de défaut matériel** (frottement, jeu, oscillation de vanne).
- Fiche de préparation de mise au point **absente** ; le PDF n'affiche pas les valeurs de bloc.
- Contrat IPC : ≈ 15 opérations non décrites dans `ipc-v1.md`, protocole figé à « v1 », pas de contrôle de version du moteur, erreurs sans unité ni détail.
- SQLite : pas de sauvegarde avant migration ; versions de numpy/scipy/Python non enregistrées ; les réglages guidés, conversions de bloc et dérives ne sont pas persistés.
- Code orphelin : `mkr_data_import` (aberrants, saturation, valeur figée, excitation, Excel), `PhysicalLoop`, et les opérations `identify_closed_loop`, `select_strategy`, `compare_tunings`, `lookup_blocks`, `recommend_blocks` sans appelant C#.
- Environ 60 seuils codés en dur côté réglage, la plupart sans justification ; tests qui passent à vide (`test_closed_loop.py:94-100, 136-138`, `test_standard_errors.py:53-57`) ; un test « indépendant » (`test_pid_noise_qualification`) réécrit la formule du code.
- Documentation contradictoire (`reglage-guide.md`, `PID-06A`, `arx.md`, `blocs-observes.md`, `audit-refonte.md`).

## 5. Points à arbitrer par vous

1. **Corrections P0 avant le LOT 1** (recommandé) ou en les répartissant dans les lots concernés ?
2. **Bibliographie :** autoriser l'accès réseau aux éditeurs/DOI pour vérification, ou faire contrôler la liste `05` par un humain.
3. **SIPPY est LGPL-3.0** : exclu par défaut tant qu'une analyse juridique n'a pas eu lieu.
4. **Licences des jeux publics** (DaISy, Nonlinear Benchmark, TCLab : aucune licence explicite trouvée pour les données) : usage à confirmer avant intégration aux jeux de qualification.
5. **Spécification originale MKR-SPEC-001 et ce cahier directeur** : les placer dans `docs/requirements/` pour qu'ils soient la source de vérité du dépôt.
6. **Reclassement de Broïda** en méthode d'estimation (contrat, stockage, interface).
7. **Jeux de données du scénario pression / pompe à variateur / M580** : aucune donnée de compatibilité M580 dans le dépôt ; données synthétiques à vérité connue à définir avec vous.

## 6. Ce que cet audit ne démontre pas

Il ne qualifie aucune méthode. Il ne compare ORIAS à aucun logiciel concurrent et n'établit aucune équivalence. Il ne remplace ni les essais sur banc, ni la validation industrielle humaine.
