# Registre versionné des jeux de données (protocole §3–§6) — v1.0.0 du 2026-11-28

Données complètes : `registre-jeux-de-donnees.json` (24 champs par jeu). **Aucune donnée brute n'est versionnée.**

| Id | Jeu | Version | Licence | Accès | Intégrité | Boucle | Ensemble prévu |
|---|---|---|---|---|---|---|---|
| S01 | Two-Tank Water System (Univ. A Coruña), 10.5281/zenodo.23529910 | **v2.0** (2026-10-21) | CC BY 4.0 (API + README) | **téléchargé** | MD5 officiel = calculé (`3a70e0b3…`), archive intègre, CHANGELOG v2.0 lu | fermée (PID adaptatif) | A/B/C par essai, jamais D |
| S02 | Dual-Phase Gas-Liquid TMP (Mendeley), 10.5281/zenodo… → 10.17632/r92bsc5ksw.2 | annoncée v2 | annoncée CC BY 4.0, **non confirmée** | **inaccessible** (DOI résolu, notice et fichiers non lisibles) | non vérifiable | — | à décider |
| S03 | Cascaded Tanks Benchmark (4TU), 10.4121/12960104.v1 | 1 (2020-09-21) | **CC BY-SA 4.0** (API + JSON-LD) | **téléchargé** | MD5 officiels = calculés pour les 2 fichiers, archive intègre | ouverte | C, séparation estimation/test des auteurs |
| S04 | Coupled-Tank D-Stabilization (CEFET-MG), 10.5281/zenodo.19856119 | v1.0.0 (2026-04-28) | CC BY 4.0 (API) | **téléchargé** (6 fichiers) | MD5 officiels = calculés (6/6) | ouverte (PRBS 10 niveaux) | A ou B |
| S05 | DaISy (KU Leuven) | non vérifiée | non vérifiée | **inaccessible** (connexion réinitialisée par l'origine) | — | — | à décider |
| S06 | IFT vannes papillon, 10.5281/zenodo.23493909 | publié 2026-10-20 | CC BY 4.0 (API) | téléchargé par l'agent | MD5 déclaré vérifié par l'agent (non recalculé ici) | fermée | C hors domaine |

## Constats vérifiés
- **S01** : la v2.0 corrige Ki/Kd et `Valve_Closure_pct` (CHANGELOG) ; la v1.0 est écartée comme imposé. Les signaux mesurés sont identiques à la v1.
- **S03** : CC BY-SA impose le partage dans les mêmes conditions : ne pas intégrer au dépôt.
- **S04** : **incohérences à lever avant usage** — `operational_parameters.json` donne des unités « mm » et des clés `h3_op`/`h4_op`, alors que le README parle de `h1`/`h2` en cm ; période annoncée 4 s, ~3,85 s observée dans `complete_data.csv`.
- **S02 et S05** : non vérifiés. Rien n'a été téléchargé ni supposé. Je n'ai pas contourné le blocage.

## Ensembles A / B / C / D (§6) — état
- **A développement, B calibration, C validation intermédiaire** : possibles avec S01, S03, S04 en séparant par **essai ou campagne** (jamais par lignes d'un même fichier). La règle de répartition sera figée et commitée AVANT tout usage.
- **D validation finale à l'aveugle : non réalisable à ce stade.** S01, S03, S04 sont publics, avec publications et résultats accessibles, et j'ai déjà lu leur structure : ils ne peuvent pas être aveugles. Aucune validation à l'aveugle n'est revendiquée. Il faut un évaluateur sans accès aux références et des données conservées hors du dépôt de développement (voir `audit-protocole-validation.md`).

## Sources complémentaires (§3)
Pression, débit, température, dosage/chloration, échangeurs, intégrateurs, temps morts importants, multivariables, cascade : seule la liste de recherche existe (l'agent a examiné 13 requêtes Zenodo, voir `donnees-reelles-preparation.md`). Diversité de procédés actuellement couverte : **niveau uniquement** (S01, S03, S04) ; **la pression, la température, le débit, les intégrateurs et les procédés à fort temps mort ne sont pas couverts par des données téléchargées et contrôlées.**
