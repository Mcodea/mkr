# MKR-SRS — Traçabilité initiale

Source primaire : MKR-SPEC-001 v1.0, 14 pages, §§1–19 examinées. La mission complète les exigences du PDF, notamment en imposant SQLite et MVVM. La spécification désigne le dépôt comme privé (§ couverture), tandis que GitHub indique une visibilité publique. Les documents sources et données industrielles restent locaux.

| Exigence / source | Module | Livrable / preuve attendue | Statut |
|---|---|---|---|
| SYS-001 §3 | Core, Desktop, Infrastructure, Python | Références des projets, build et test d'intégration | Implémenté, build Windows à vérifier |
| SYS-002 §3 | IPC | Contrat v1, tests requêtes valides/invalides et échange interprocessus | Python testé, intégration .NET à vérifier |
| DATA-001 §§5,13 | Data/Persistence | Octets originaux, empreinte, journal des traitements, test de réouverture | Phase 2 |
| DATA-002 §§4–5 | Data | Jeu annoté avec trous, doublons, unités, saturation et excitation, sans correction silencieuse | Phase 2 |
| SCI-001 §6 | Identification | FOPDT/SOPDT, résidus, incertitudes et validation indépendante | Phase 3 |
| SCI-002 §§7–8 | Control/Tuning | Cas de référence P/PI/PID, IMC/Lambda et SIMC, formes et signes documentés | Phase 4 |
| SCI-003 §9 | Simulation | Scénarios reproductibles, cas analytiques et convergence | Phase 5 |
| COM-001 §§5,10 | Data/Acquisition | Entrées hors ligne et connectées equivalentes après normalisation | Phases 2 et 7 |
| COM-002 §§10,14 | Acquisition | Liste de fonctions autorisées en lecture et preuve de non-écriture | Phase 7 |
| COM-003 §10 | Acquisition | Mapping explicite et tests des types, offsets et ordre des octets/mots | Phase 7 |
| COM-004 §10 | Acquisition | Simulation de pertes et traçabilité de la qualité et des horodatages | Phase 7 |
| CAT-001 §11 | Catalogue | Entrées versionnées, sources officielles et essais | Phase 6 |
| CAT-002 §11 | Catalogue | Compatibilités affichées uniquement avec preuve appropriée | Phase 6 |
| UI-001 §12 | Desktop | États vides/erreurs, scénarios de revue et validation humaine | Socle d'erreurs implémenté, revue métier future |
| REP-001 §13 | Reporting | Rapport reproductible à partir des données, versions et hypothèses | Progressif à partir de la phase 5 |
| TEST-001 §15 | Tests/CI | Preuves logicielles, scientifiques, fonctionnelles et industrielles selon module | Partiel |

## Exigences transversales et arbitrages

- **Explicite** — Windows/WPF, moteur Python indépendant, aucun accès en écriture aux automates, validation humaine, provenance et unités visibles. Convention e = SP − PV. SP est la consigne, PV la mesure, CV la sortie calculée, MV l'action appliquée.
- **Orientations** — Monolithe modulaire, Modbus TCP prioritaire sur Schneider, approches IMC/Lambda et SIMC, catalogue initial, stratégies avancées après MVP. L'intégrateur appartient au périmètre scientifique, sa qualification suit les deux familles du MVP recommandé.
- **Ouvert** — Volumes, rétention, sauvegardes, format de rapport, solveur, tolérances et jeux de validation, charte, versions CPU/firmware, cadence, packaging Windows. L'ADR consigne les choix techniques de cette livraison.
- **Acceptation du cadrage** — Chaque identifiant du §17 est relié à une preuve attendue et une phase. Documentation des risques et des arbitrages relue sur PR.
- **Acceptation du socle** — Compilation Windows, création/réouverture d'un projet, migration idempotente, échange Python v1 et rejet des erreurs de contrat, contrôle des dépendances, procédure de build reproductible et essai graphique manuel.
- **Acceptation de la phase 2** — Importer un CSV réel, conserver l'original et son empreinte, déclarer rôles/unités/horodatage, afficher les lignes et anomalies sans modifier la source, lancer un premier traitement documenté et retrouver l'ensemble à la réouverture.
