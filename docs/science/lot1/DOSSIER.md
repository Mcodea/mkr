# LOT 1 — Socle et orchestration : dossier préalable (AGENTS.md §5)

**Statut : dossier de cadrage, avant tout code du lot.** Prérequis : clôture du LOT 0b (CI verte du commit de référence ; P0-6 reporté au LOT 2).

## Exigences d'origine
Cahier directeur §6 (architecture), §25 (orchestration), §31 (traçabilité), §33 (qualité d'architecture), §34 (LOT 1) ; constats P0-5, P0-8 et P1 du LOT 0 (`docs/science/lot0/00-synthese-audit.md`) ; P0-3c (période du régulateur).

## Périmètre
**Dans le lot**
1. **Contrats C#↔Python versionnés et documentés** : liste exhaustive des opérations (≈ 25, dont ≈ 15 non décrites dans `ipc-v1.md`), schémas d'entrée/sortie, version de contrat, contrôle de compatibilité de version du moteur par l'application, erreurs avec code ET détail, unités dans les résultats.
2. **Moteur d'orchestration** : état de chaque étape du parcours parmi *Non commencé, En cours, À compléter, À vérifier, Validé pour le domaine étudié, Bloqué, Non applicable, Résultat obsolète* ; l'étape « Qualification » ne passe plus à *faite* à l'import (elle dépend du verdict du LOT 2 : jusque-là *À vérifier*) ; règles déterministes, versionnées, testées.
3. **Dépendances et invalidation** : empreinte des entrées de chaque résultat (identifiants et SHA-256 des données, configuration, versions d'algorithmes) ; un résultat dont les entrées ont changé est *obsolète* et ne peut plus apparaître comme valide (données → identification → réglage → simulation → conversion bloc).
4. **Traçabilité** : enregistrer pour chaque recommandation les versions du moteur, de Python, de numpy et de scipy, la configuration scientifique, les contraintes appliquées, les références ; historique de la fiche de boucle (plus d'UPDATE écrasant) ; saisie et persistance de la **période d'exécution du régulateur** (P0-3c).
5. **Migrations SQLite** : sauvegarde automatique avant migration, tests de migration depuis v8, v9, v10, v11, compatibilité des anciens projets.
6. **Règles scientifiques hors des vues** : `TrendReader` (second analyseur CSV) et les calculs FIT/RMSE/R²/IAE recalculés dans l'interface sont déplacés vers le moteur ou supprimés.

**Hors lot** : verdict de qualification des données (LOT 2), identification en boucle fermée et mode manuel (LOT 2), reclassement de Broïda (LOT 3), toute nouvelle méthode scientifique.

## Risques
- Régression des études existantes (mitigation : migrations testées, aucune suppression de colonne, lecture seule des anciennes données).
- Modification des écrans validés (mitigation : seuls des états et libellés sont ajoutés dans l'interface existante ; captures comparées).
- Surcoût de calcul de l'invalidation (mitigation : empreintes calculées à l'écriture, comparaison à la lecture).

## Critères d'acceptation mesurables
1. Un test de cohérence échoue si une opération du moteur n'est pas documentée dans le contrat (liste générée depuis `mkr_engine.py`).
2. Un moteur dont la version de contrat est incompatible est refusé au démarrage avec un message explicite (test avec faux moteur).
3. Modification d'une donnée, du modèle, du régulateur, du bloc ou d'une limite d'exploitation ⇒ les résultats dépendants passent à *Résultat obsolète* (un test par arête du graphe de dépendances).
4. Tout projet créé en v8, v9, v10 et v11 s'ouvre après migration avec les mêmes études, imports et empreintes ; une sauvegarde est créée avant la migration ; une base v12 est refusée par une version antérieure.
5. Chaque recommandation enregistrée contient les versions (moteur, Python, numpy, scipy, algorithme) et la période du régulateur ; deux exécutions avec mêmes entrées donnent les mêmes sorties dans les tolérances documentées.
6. L'étape « Qualification » reste *À vérifier* tant que le verdict du LOT 2 n'existe pas.
7. Aucun test existant supprimé ni affaibli ; CI Windows verte ; captures des écrans touchés comparées à la référence UI/UX réceptionnée.

## Tests prévus
Unitaires Python (contrat), smoke .NET (migration, invalidation, orchestration), tests graphiques Windows pour les statuts affichés, tests de compatibilité des anciens projets (jeu de projets v8–v11 versionné, **données synthétiques**).

## Données nécessaires
Projets de test anciens (à générer par les versions successives du schéma, synthétiques) ; aucun donnée industrielle.

## Preuves impossibles à obtenir ici
Rendu réel des statuts à l'écran (CI Windows uniquement) ; migration sur de vrais projets utilisateurs.

## Dépendance avec la section 42
La section 42 du cahier (complément v1.1) n'a pas été reçue au moment de la rédaction : sa répartition dans les lots reste à faire et pourra modifier ce périmètre.
