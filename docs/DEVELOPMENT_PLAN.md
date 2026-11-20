# Plan de développement MKR

Référence primaire : MKR-SPEC-001 v1.0. Les [exigences](requirements/traceabilite.md), l’[architecture](architecture/socle.md) et les [ADR](adr/) documentent les arbitrages. Le PDF couvre 14 pages et 19 sections.

| Jalon | Dépendance | Acceptation et reste à produire |
|---|---|---|
| Socle Windows | — | Compilation WPF, IPC versionné, stockage versionné, erreurs et audit. Essai graphique manuel requis. |
| Données hors ligne | Socle | Parcours CSV et provenance exécutés. Compléter import Excel, mapping multivarié réutilisable, courbes sources et règles documentées de qualité. |
| Identification | Données alignées et traçables | FOPDT, SOPDT et intégrateur exploratoires par échelons avec diagnostics distincts. Compléter l’incertitude SOPDT/intégrateur et consolider la comparaison sur données représentatives. |
| P/PI/PID et réglages | Domaine des modèles explicite | Comparaison PI IMC/Lambda et SIMC pour FOPDT, forme et signe documentés. Compléter P/PID et les autres domaines applicables. |
| Simulation et comparaison | Modèles et régulateurs | Comparaison PI en boucle fermée disponible, avec perturbation, saturation et export. Ajouter boucle ouverte, limite de vitesse, modes et indicateurs temporels. |
| Catalogue | Conventions d’algorithmes stabilisées | Chaque bloc a sa source, version, limites et un statut documenté. |
| Acquisition en lecture seule | Modèle de données commun et banc autorisé | Mapping, types, traçabilité, essais de charge et de non-écriture. |
| Livraison et stabilisation | Parcours précédents | Régression, packaging, essais Windows et rapports reproductibles. |

Les rapports s’enrichissent à chaque étape. Les références et les hypothèses restent attachées aux versions de résultats. Les activités sur banc et sur site ont leurs propres critères d’acceptation.
