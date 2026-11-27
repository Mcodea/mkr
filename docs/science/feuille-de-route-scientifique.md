# Feuille de route scientifique ORIAS — rebasée (version 1.1 du cahier directeur)

**Base :** révision UI/UX réceptionnée `c7915cd` ; branche `claude/science-engine-dev` ; cahier directeur `docs/requirements/cahier-directeur-orias.md` (41 sections + section 42, version 1.1). Rebaselining autorisé par le client (2026-11-26, AGENTS.md §2). Aucune fusion dans `main` sans autorisation. Un lot n'est clos que lorsque ses critères mesurables sont démontrés.

| Lot | Contenu | Section 42 répartie ici | État |
|---|---|---|---|
| LOT 0 | Audit, référentiel, plan | — | **clos** |
| LOT 0b | Corrections P0 de validité | — | **clos** (P0-6 reporté au LOT 2) |
| **LOT 1** | Socle et orchestration : contrats IPC, statuts (8), invalidation en cascade, traçabilité, sauvegarde avant migration, période du régulateur, règles hors des vues | 42.2 limites d'exploitation, durée maximale et conditions d'arrêt (fiche d'étude) ; 42.2 journal d'événements d'essai ; 42.3 statut « provisoire » vs « définitif » | **en cours** (tranche 1 : contrat IPC, CI en attente) |
| **LOT 2** | Acquisition et qualification des données | **Cœur de la section 42** : 42.1 (mode de boucle comme propriété des données, confirmation de l'origine des historiques, MANUAL pris en compte) ; 42.2 assistant d'essai en direct (connexion, fraîcheur, synchronisation, conditions initiales, stabilisation, excitation détectée, durée réévaluée, suffisance, événements, sauvegarde et reprise) ; 42.4 instructions et courbes en direct ; 42.6 boucles ouverte/fermée accessibles depuis l'interface ; verdict de qualification à 5 niveaux ; P0-6 | à venir |
| LOT 3 | Identification scientifique | 42.2 Broïda automatique (variation finale établie, repères graphiques, enregistrement) ; 42.3 intégrateur sans plateau, grand temps mort ; méthodes adaptées au mode ; reclassement de Broïda en méthode du FOPDT (migration) | à venir |
| LOT 4 | Sélection et validation | 42.2 essai de validation accompagné ; 42.3 refus si modèles indiscernables | à venir |
| LOT 5-6 | Stratégies, réglages, optimisation, robustesse | Réglages à partir de données en boucle fermée (une fois identifiables) | à venir |
| LOT 7 | Adaptateurs constructeurs | Blocs Schneider PIDFF, PI_B, PID_INT (non interchangeables ; recherche en cours) | à venir |
| LOT 8 | Mise au point et rapport | Rapport : preuves des essais en direct et événements | à venir |
| LOT 9 | Scénario complet pression / pompe / M580 | 42.6 : parcours bout en bout sur automates simulés | à venir |
| LOT 10 | Qualification indépendante | Tests de refus, perte de communication, changement de mode, saturation | à venir |
| LOT 11 | Extensions | 42.3 réponses inverses, procédés oscillants, multi-dynamiques, procédés non étudiables en boucle ouverte | à venir |

**Permanent (tous les lots) :** lecture seule des automates (42.5) ; aucune précondition scientifique n'est présentée comme autorisation de sécurité ; distinction implémenté / intégré / testé / qualifié.

**Ordre de dépendance retenu pour la section 42 :** LOT 1 (statuts, fiche d'étude, journal) → LOT 2 (données et assistant) → LOT 3 (méthodes, Broïda automatique) → LOT 4 (validation) → LOT 9 (bout en bout). L'assistant d'essai (42.2) n'est pas un lot isolé : il s'appuie sur le verdict de qualification du LOT 2 et sur la machine à états de l'étude du LOT 1.
