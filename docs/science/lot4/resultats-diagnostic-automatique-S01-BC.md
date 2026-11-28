# Diagnostic automatique (engine/signal_diagnosis.py v1.0.0) — évaluation sur S01, ensembles B et C

Règles et paramètres fixés avant l'évaluation (commit `d7b185d`, antérieur à ce résultat) ; **aucune règle ajustée ensuite**. Données : S01 v2.0 (`Dataset_v2.zip` SHA-256 `e31b41da…128e`), ensembles B (11 fichiers) et C (8 fichiers) de `repartition-S01.tsv`, **évalués séparément**. Script : `tests/reference/real_data/s01_diagnosis_eval.py` ; données : `resultats-diagnostic-automatique-S01-BC.json`. **Un seul banc ; données expérimentales réelles.**

## Perte de capteur possible (niveau, vérité = colonne `Sensor_Dropout` des auteurs)
| Ensemble | Pertes annotées | Détectées | Manquées | Signalées non annotées | Rappel | Précision |
|---|---|---|---|---|---|---|
| B | 5 627 | 5 627 | 0 | 36 | 100 % | 99,4 % |
| C | 4 185 | 4 185 | 0 | 43 | 100 % | 99,0 % |

Intervalles de Clopper-Pearson (95 %, bilatéral) du rappel : B [99,93 ; 100] %, C [99,91 ; 100] %. Les signalements non annotés se trouvent surtout dans les essais `Baseline` (1 à 17 échantillons par fichier) : des valeurs exactement égales au minimum observé atteintes par un saut, probablement le zéro de démarrage du réservoir vide ; **je ne l'ai pas démontré**, et la règle n'a pas été ajustée pour les éliminer.

## Saturation de la commande (sans limite déclarée par l'utilisateur)
La règle **retrouve seule les bornes réelles de l'actionneur (0 et 100 %)** dans les 12 fichiers où la commande est saturée (parts de 15 % à 76 % du temps). La part saturée rapportée vaut 95 % à 99 % de la part réellement à la borne : l'écart vient des échantillons isolés (moins de 3 consécutifs), volontairement non comptés. Dans les essais `Baseline`, où la commande ne touche sa borne qu'une à quelques fois (0,01–0,03 %), la règle rapporte une saturation nulle ou quasi nulle (aucune fausse alerte).

## Cadence
Les 19 fichiers présentent des horodatages dupliqués et sont signalés « horloge plus grossière que l'échantillonnage ». Les fichiers `Baseline` (≈ 30 090 doublons : horloge à la minute) sont clairement distingués des autres (53 à 103 doublons : gigue d'acquisition à la seconde). Aucune décision n'est prise sur la valeur de la période déduite ; le fait est rapporté.

## Limites (à ne pas masquer)
- **Un seul banc, une seule convention de codage** : S01 code la perte de capteur par un zéro exact. D'autres installations la codent autrement (valeur manquante, dernière valeur maintenue, valeur hors plage). **La généralisation de la détection n'est pas démontrée.**
- La vérité des pertes de capteur est **annotée par les auteurs** ; je n'ai pas pu la vérifier par ailleurs.
- La règle de saturation par plateau exact ne détecterait pas un actionneur dont la sortie reste *proche* de sa borne sans y être exactement (bruit, quantification d'un convertisseur).
- Le module est **développé et évalué, mais pas encore intégré** à l'application (aucune opération IPC, aucune restitution à l'utilisateur) : il reste à faire (ADR de contrat, parcours Diagnostic, exclusion des fenêtres saturées avant identification).
