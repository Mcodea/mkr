# LOT 0 — Périmètre qualifiable proposé et plan de développement priorisé

**Statut : proposition, non adoptée** (AGENTS.md §2). À arbitrer avant toute modification majeure d'algorithme.

## 1. Périmètre scientifique initial proposé (à qualifier, pas déjà qualifié)

À partir de ce que l'audit montre de plus solide (simulation vérifiée, FOPDT/SOPDT/intégrateur vérifiés sur analytique) :

- **Procédés :** réponse indicielle en **boucle ouverte**, point de fonctionnement stabilisé, gain de signe établi, mesure peu bruitée, sans oscillation ni saturation de la commande.
- **Modèles :** FOPDT, SOPDT, intégrateur avec retard. **Broïda** comme méthode d'estimation du FOPDT.
- **Stratégies :** P, PI, PID (sans prédicteur de Smith, cascade, MPC).
- **Réglages :** IMC/Lambda et SIMC, optimisation sous contraintes avec front de Pareto.
- **Hors périmètre de la première qualification :** boucle fermée, données du fonctionnement normal, procédés oscillants/instables, multivariable, non linéaire, stratégies avancées, tout autre constructeur que Schneider, détection de défaut matériel.
- **Conversions constructeur :** aucune n'est qualifiée ; elles restent « Conversion non qualifiée » jusqu'à vérification sur le simulateur ou le banc.

## 2. Règles d'exécution (rappel des garde-fous)

- Pas d'écriture automate ; tests sur simulateurs seulement.
- Aucun seuil modifié pour transformer un échec en réussite ; oracles indépendants ; graines documentées ; jeux de qualification réservés.
- Aucune déclaration d'équivalence ou de supériorité face à un logiciel concurrent sans preuve comparative.

## 3. Plan priorisé

| Ordre | Lot | Contenu principal (sources : annexes) | Preuve de sortie |
|---|---|---|---|
| 0 | **LOT 0** (ce document) | audit, matrices, référentiel, plan | validation de l'audit par vous |
| 0b | **Corrections P0 de validité** *(proposé, à arbitrer)* | P0-1 erreur-type FOPDT ; P0-2 ARX/intégrateur ; P0-3 robustesse échantillonnée et période du régulateur ; P0-4 refus moteur du modèle non validé ; P0-6 `loop`/MANUAL ; P0-7 compatibilité catalogue (refus par défaut) | tests de non-régression existants **inchangés** + nouveaux tests à oracle indépendant ; CI verte ; matrice de qualification mise à jour |
| 1 | LOT 1 — Socle et orchestration | contrats IPC versionnés et documentés, orchestration (étape « Qualification » réelle), invalidation en cascade, traçabilité (versions de bibliothèques, contraintes, références), sauvegarde avant migration, règles hors vues | études anciennes ouvertes sans perte ; tests de migration v8→v11 |
| 2 | LOT 2 — Données | verdict 5 niveaux par le moteur, MV/SP qualifiées, intégration ou retrait de `mkr_data_import`, boucle ouverte/fermée, fraîcheur, bug 3 600/600 s | corpus étiqueté a priori, matrice de confusion |
| 3 | LOT 3 — Identification | incertitudes couvertes (Monte-Carlo), identifiabilité, reclassement de Broïda, plateau non stabilisé | couverture empirique ; jeux bruités |
| 4 | LOT 4 — Sélection/validation | AIC/AICc/BIC, séparation estimation/sélection/qualification, refus scientifique | refus justifiés vs injustifiés |
| 5 | LOT 5 — Stratégies et réglages | P/PI/PID admissibles, règles d'admissibilité, références lues (SIMC + erratum) | oracles indépendants |
| 6 | LOT 6 — Optimisation, robustesse | Pareto, préférences documentées, limites d'actionneur, comparaison avec le réglage existant | benchmark pré-enregistré |
| 7 | LOT 7 — Adaptateurs | catalogue vérifié, oracles indépendants, « Conversion non qualifiée » | tests indépendants ; banc/simulateur Control Expert |
| 8 | LOT 8 — Mise au point et rapport | fiche d'essai, valeurs de bloc au rapport, statuts du §23 | PDF relu |
| 9 | LOT 9 — Scénario complet | pression, pompe à variateur, Schneider M580 | parcours de bout en bout reproductible |
| 10 | LOT 10 — Qualification indépendante | jeux réservés, cas expérimentaux, matrice | décisions de qualification |
| 11 | LOT 11 — Extensions | débit, niveau, température, chloration, boucle fermée | par scénario |

La vérification scientifique est faite **dans chaque lot**. Un lot n'est pas terminé tant que ses critères mesurables ne sont pas démontrés.

## 4. Décisions à valider (voir aussi `00-synthese-audit.md` §5)

1. Lot de corrections P0 (0b) avant le LOT 1 : oui / non.
2. Accès réseau pour la vérification bibliographique, ou contrôle humain.
3. SIPPY (LGPL-3.0) exclu par défaut.
4. Licences des jeux publics à confirmer.
5. Dépôt de MKR-SPEC-001 et du cahier directeur dans `docs/requirements/`.
6. Reclassement de Broïda en méthode d'estimation (migration de stockage).
7. Données et hypothèses du scénario pression / M580.

## 5. Preuves impossibles à obtenir dans l'environnement actuel

Poste Windows réel pour les captures de nouvelles fonctions (la CI y supplée), automate de banc, simulateur EcoStruxure Control Expert, version exacte du bloc constructeur, accès aux éditeurs scientifiques (DOI), données industrielles réelles autorisées.
