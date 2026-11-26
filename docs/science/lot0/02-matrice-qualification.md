# LOT 0 — Matrice de qualification scientifique (état initial)

Niveaux : Non implémenté · Expérimental · Implémenté · Vérifié numériquement · Validé sur données indépendantes · Vérifié sur banc · **Qualifié dans un domaine défini** · Non qualifié · Bloqué.
**Règle :** aucun niveau n'est attribué sans preuve. Toute case « Qualifié » exige des tolérances justifiées, un jeu réservé et une référence indépendante (cahier §27). **À ce jour, aucun élément n'est « Qualifié dans un domaine ».**

| Élément | Version (code) | Niveau constaté | Preuves existantes | Anomalies connues (annexes) | Prérequis pour monter |
|---|---|---|---|---|---|
| Modèle FOPDT | `fopdt.py` 1.4.0 | Vérifié numériquement (analytique, sans bruit) | réponses analytiques, refus de cas dégradés | erreur-type du gain ×0,49 (P0-1) ; plateau non stabilisé : gain −11 % | correctif P0-1 ; Monte-Carlo de couverture ; jeux bruités réservés |
| Modèle SOPDT | `sopdt.py` 1.1.0 | Vérifié numériquement | idem | τ2 non propagé au réglage | idem |
| Modèle intégrateur | `integrator.py` 1.1.0 | Vérifié numériquement ; **seul oracle réellement indépendant** (balayage moindres carrés) | oracle indépendant | — | jeux bruités ; données réelles |
| Broïda (méthode, pas famille) | `broida.py` 1.1.0 | Testé | coefficients recalculés (5,49 ; 2,80 ; 1,80) ; biais +0,27 % sur τ | exposé comme famille de modèles ; tolérance 6 % ; seuils 28/40 % non confrontés à la source | reclassement ; source primaire lue |
| ARX / OE | `arx.py` 1.0.0 | Testé | refus d'un retard de 80 pas | accepte un intégrateur (P0-2) ; détection de rétroaction inexistante ; sélection sur la validation | correctifs ; protocole estimation/sélection/qualification |
| Recherche d'essais | `step_finder.py` 1.2.0 | Testé | cas synthétiques | pas de test de pente avant l'échelon | jeux étiquetés a priori |
| Porte de validation | `validation_gate.py` 1.1.0 | Testé | refus de défauts de terrain | tests passant à vide ; pas d'AIC/AICc/BIC | critères d'information ; Ljung-Box |
| Comparaison de modèles | `model_comparison.py` 1.1.0 | Testé | — | idem | idem |
| Identification en boucle fermée | `closed_loop.py` 0.1.0 | **Expérimental**, hors interface | simulations du dépôt | non conforme ADR 0006 ; dispersion sous-estimée ≈30 % | refonte (LOT 2-3) |
| Règles de réglage IMC/Lambda/SIMC | `pi_tuning.py`, `pid_tuning.py` | Vérifié numériquement (formes fermées) | balayage fréquentiel, simulation, 8 procédés synthétiques | attribution `imcPublished` ; erratum SIMC à lire | références lues |
| Réglage guidé (robustesse) | `recommend.py` | Implémenté, **robustesse non tenue** (P0-3) | tests de cas nominaux | règle de la moitié ; h/2 ; période du régulateur | correctifs ; oracles sur boucle vraie |
| Sélection de stratégie | `strategy_selection.py` | Implémenté, non appelé | 47 lignes de tests | heuristique ; P/I jamais proposés | LOT 5 |
| Optimisation multicritère | `tuning_comparison` | Expérimental | tests minces | pas de front de Pareto | LOT 6 |
| Simulation boucle fermée | `pi_simulation.py`, `pid_simulation.py`, `fopdt_simulation.py` | **Vérifié numériquement (indépendamment)** | `scipy dlsim`/`cont2discrete`, erreur 8,9e-16 sur le temps mort | saturation/vitesse absentes du jugement | — |
| Marges, Ms | `stability_margins.py` | Vérifié numériquement (formes fermées) | idem | Ms échantillonné non calculé (P0-3) | correctif |
| Robustesse paramétrique | `pi_robustness.py`, `pi_combined_robustness.py` | Implémenté | Monte-Carlo à graines fixes | pas d'intervalle de confiance ; pire cas aux coins démontré sur 3 cas FOPDT seulement | LOT 6 |
| Dérive du modèle | `model_drift.py` | Implémenté | tests | un modèle en dérive reste utilisable pour le réglage | P0-4 |
| Qualité des données | `csv_quality.py` | Implémenté (comptages, 1 colonne) | tests de comptage | pas de verdict 5 niveaux (P0-5) | LOT 2 |
| Acquisition Modbus TCP / S7 | `ModbusTcpReader`, `S7Reader` | Testé (stubs, serveurs de référence) ; **lecture seule prouvée par le code** | CI runs 329/330 | pas d'audit de trames en interopérabilité ; pas de banc | LOT 2 |
| Adaptateur PIDFF (Schneider) | `block_adapters.py` | Implémenté, **non qualifié** | modèle écrit par le projet d'après le manuel | oracle non indépendant ; structure parallèle refusée | oracle indépendant ; banc |
| Adaptateur PID_INT (Schneider) | idem | Implémenté, **non qualifié** | oracle et référence écrits dans le test, tolérance 6 % | idem | idem |
| Adaptateur PID_Compact V2.0–V3.0 (Siemens) | idem | Implémenté, **non qualifié** | identité algébrique (tautologique) | idem | idem |
| Catalogue de blocs | `block_catalogue.py` | Implémenté, **compatibilité non contrôlée** (P0-7) | tests de structure | champ vide = compatible ; cible vide côté C# ; 7 fiches « à vérifier » | LOT 7 |
| Rapport PDF | `StudySheet.cs` | Implémenté (PI FOPDT) | lecture PDF en CI | valeurs de bloc, ITAE, dépassement, versions absents | LOT 8 |
| Fiche de mise au point | — | **Non implémenté** | — | — | LOT 8 |
| Détection de défaut matériel | — | **Non implémenté** | — | — | LOT 11 |
| Scénario pression / pompe / M580 | — | **Non implémenté** (aucune donnée M580) | — | — | LOT 9 |
