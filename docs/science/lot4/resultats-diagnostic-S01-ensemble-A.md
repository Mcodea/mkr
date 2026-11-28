# Diagnostic des données sur S01 (ensemble A) — résultats du 2026-11-28

Script : `tests/reference/real_data/s01_diagnostic.py` (fenêtres contiguës de 6 000 lignes, aucune valeur modifiée) ; données : `resultats-diagnostic-S01-ensemble-A.json` ; archive : `Dataset_v2.zip` SHA-256 `e31b41da…128e`. **Ensemble A seulement** (6 fichiers sur 25, répartition `repartition-S01.tsv` figée avant usage). **Données expérimentales réelles ; observations descriptives, aucune généralisation.**

## Résultats
| Fichier (ensemble A) | Lignes | Pertes capteur annotées | Détectées par la bibliothèque (limites 0–100 déclarées) | Manquées | Zéros réels signalés à tort | Commande à 0 ou 100 % | Part de la commande saturée | Horodatages dupliqués (moteur) | Pas min–max (s) |
|---|---|---|---|---|---|---|---|---|---|
| Baseline_80pct.csv | 30600 | 1 | 1 | 0 | 3 | 8 | 0.0 % | 30084 | 60–60 |
| Disturbance_LowOutflow_50pct.csv | 30600 | 1 | 1 | 0 | 3 | 6 | 0.0 % | 96 | 1–2 |
| PassiveOutflow_100pct.csv | 30600 | 2470 | 2470 | 0 | 2 | 24514 | 80.1 % | 108 | 1–2 |
| PassiveOutflow_20pct.csv | 30600 | 581 | 581 | 0 | 0 | 11860 | 38.8 % | 67 | 1–2 |
| PassiveOutflow_40pct.csv | 30600 | 1237 | 1237 | 0 | 1 | 17720 | 57.9 % | 72 | 1–2 |
| PassiveOutflow_50pct.csv | 30600 | 1662 | 1662 | 0 | 0 | 17641 | 57.7 % | 61 | 1–2 |

« Détectées » : l'outil `mkr_data_import.signal_quality.assess_signal`, avec les limites d'échelle **déclarées a priori** (0–100 %), signale les échantillons du niveau égaux à une borne. « Pertes capteur annotées » : colonne `Sensor_Dropout` des auteurs (le niveau vaut alors exactement 0). Les colonnes « commande » comptent `Control_Signal_pct` ≤ 0 ou ≥ 100.

## Constats (chacun vérifié sur les fichiers)
1. **F-S01-1 — Limite d'import du moteur.** Les fichiers (30 600 lignes, ~2 Mo) dépassent les limites d'`engine/csv_quality` (10 000 lignes **et** 1 MiB ; la taille en octets bloque dès 9 000 lignes de 15 colonnes). Un enregistrement de 8,5 h à 1 Hz n'est donc pas importable tel quel ; j'ai dû le découper. Ce sont des bornes de protection, pas un défaut ; mais elles limitent l'usage sur de vrais historiques. **Aucune limite modifiée** (changement soumis à ADR).
2. **F-S01-2 — Horodatage non fiable dans un fichier.** `Baseline_80pct.csv` a un pas d'horodatage de **60 s** et **30 084 horodatages dupliqués** alors que les données sont à 1 Hz : le moteur le mesure (dupliqués, pas min/max) mais n'en tire aucune décision ni explication. La colonne `Time_s` du fichier est la base de temps fiable. Les cinq autres fichiers ont des horodatages à la seconde avec 61 à 108 doublons et des pas de 1 à 2 s (gigue d'acquisition).
3. **F-S01-3 — Pertes de capteur codées par un zéro valide.** Les 6 fichiers contiennent 1 à 2 470 pertes annotées. Le **diagnostic de l'application ne les voit pas** : `csv_quality` ne compte aucune valeur manquante ni invalide (0), car le zéro est une valeur numérique valide. La bibliothèque `signal_quality`, avec limites déclarées, les retrouve **toutes** (0 manquée), au prix de **0 à 3 zéros réels signalés à tort** (niveau réellement nul au démarrage) ; mais **elle n'est appelée par aucun code de l'application** (recherche dans le dépôt) et exige des seuils saisis par l'utilisateur, contraire à l'exigence du protocole (§13, §22).
4. **F-S01-4 — Saturation massive de la commande.** Les essais PassiveOutflow ont la commande à 100 % pendant **38,8 % à 80,1 %** du temps (11 738 à 24 446 échantillons à 100 %) : l'identification d'un procédé à partir de ces portions serait faussée. Le diagnostic actuel du moteur ne le signale pas sans limites déclarées ; avec limites déclarées, la détection est triviale (elle égale le comptage).
5. **Perte de capteur ≠ signal figé** : `frozen` (étendue globale ≤ tolérance) est faux sur tous les fichiers, donc inutile pour des arrêts locaux ; les plateaux de longueur maximale (7–42 échantillons) ne sont pas ceux d'un capteur bloqué.

## Conclusion exploitable (protocole §19 : une étude → une conclusion)
- **Faiblesse démontrée** : le diagnostic automatique **ne détecte pas, sans seuil saisi par l'utilisateur,** (a) les pertes de capteur codées par une valeur, (b) la saturation de la commande, (c) l'incohérence entre horodatage et cadence réelle.
- **Développement justifié** : un diagnostic automatique qui (i) infère l'échelle d'un signal à partir de ses valeurs extrêmes répétées (plateaux exacts aux extrémités) et signale la **part de temps saturée**, (ii) signale les **valeurs égales à une borne d'échelle déclarée de la mesure** comme perte capteur *possible* (jamais supprimées), (iii) compare la **cadence réelle** à la colonne de temps. Les règles seront fixées à partir de la physique des signaux et **calibrées sur l'ensemble B** (jamais sur A seul), puis comparées sur C. Ce développement n'est **pas encore fait** ; la matrice de qualification reste inchangée.
