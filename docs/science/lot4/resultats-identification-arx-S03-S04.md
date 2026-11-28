# Identification ARX (engine/arx.py v1.0.0) sur S03 et S04 — résultats du 2026-11-28

Mode opératoire pré-enregistré avant exécution (`protocole-evaluation-donnees-reelles.md` §6, commit antérieur). Données : S03 `CascadedTanksFiles.zip` (SHA-256 `eb0fa058…1b51`, ensembles estimation/test **des auteurs**), S04 `complete_data.csv` (SHA-256 `6b2470f4…78d`), fenêtres de 81 échantillons, apprentissage A / validation C. Script : `tests/reference/real_data/identification_s03_s04.py` ; sortie complète : `resultats-identification-arx-S03-S04.json`. **Données expérimentales réelles, boucle ouverte, un banc par jeu.** `maximumRmse` balayé à 2, 5, 10, 20 % de l'étendue de la sortie de validation.

## Résultats
| Essai | Étendue sortie | Erreur du prédicteur constant (RMSE / étendue) | Meilleur candidat (erreur de simulation / étendue) | Décision à 2 % · 5 % · 10 % · 20 % | Modèle retenu quand accepté (erreur / étendue) |
|---|---|---|---|---|---|
| S03 cascade (estimation → test) | 7,836 | 26,8 % | 7,5 % | refusé · refusé · refusé · refusé | — |
| S04 u→h1, rép. 1 → 7 | 25,815 | 27,8 % | 4,7 % | refusé · refusé · **accepté** · accepté | (1,1) nk=2, 7,1 % |
| S04 u→h2, rép. 1 → 7 | 7,714 | 30,2 % | 4,6 % | refusé · refusé · refusé · **accepté** | (1,1) nk=5, 15,9 % |
| S04 u→h1, rép. 2 → 8 | 25,815 | 27,9 % | 4,6 % | refusé · refusé · accepté · accepté | (1,1) nk=2, 6,9 % |
| S04 u→h2, rép. 2 → 8 | 7,714 | 30,3 % | 3,1 % | refusé · refusé · refusé · accepté | (1,1) nk=5, 17,1 % |
| S04 u→h1, rép. 3 → 9 | 25,815 | 28,0 % | 4,7 % | refusé · refusé · accepté · accepté | (1,1) nk=2, 7,1 % |
| S04 u→h2, rép. 3 → 9 | 7,714 | 30,4 % | 5,4 % | refusé · refusé · refusé · accepté | (1,1) nk=4, 16,4 % |

## Constats
1. **S03 : refus justifié du modèle linéaire.** À tous les niveaux le moteur refuse : les résidus de validation ne sont pas blancs et restent corrélés à l'entrée (structure non modélisée). Le meilleur candidat explique l'essentiel de la variation (erreur 7,5 % de l'étendue contre 26,8 % pour un prédicteur constant) mais pas sa structure : c'est le comportement attendu pour un procédé non linéaire (saturation, débordement) et correspond à l'objectif « détecter les situations où un modèle linéaire est insuffisant » (protocole §1, §10). **La qualité d'ajustement n'a pas suffi à faire accepter le modèle.**
2. **F-ARX-1 — La décision dépend d'une limite d'erreur saisie par l'utilisateur.** Pour S04, la voie ARX exige `maximumRmse` : h1 est accepté dès 10 % de l'étendue, h2 seulement à 20 %, avec une erreur retenue de 16–17 % de l'étendue (le prédicteur constant en fait 30 %). **Le moteur ne propose aucune limite d'erreur automatique pour une excitation arbitraire** (la voie par échelons déduit les critères du bruit mesuré). Cela contredit l'exigence « sans seuil saisi par l'utilisateur » et fait varier la décision avec un réglage arbitraire : **faiblesse démontrée**.
3. **Le modèle retenu est le plus simple qui passe (1,1), pas le plus précis** (règle du moteur : moins de paramètres d'abord) : l'erreur retenue (7 à 17 %) dépasse celle du meilleur candidat (3 à 5 %). C'est conforme à la règle documentée, à discuter si le but est la régulation.
4. **Des candidats instables** apparaissent dans les refus de S04 h2 (`unstable_model`) : la porte les écarte comme prévu.
5. **Limites** : S04 = fenêtres très courtes (81 échantillons) et répétitions de la **même séquence d'entrée** (indépendance du bruit seulement) ; l'unité des niveaux de S04 n'est pas établie ; S03 est un cas non linéaire. Aucune conclusion sur l'aptitude à la régulation (aucun réglage n'a été calculé ni simulé ici).

## Conclusion exploitable
- **Conserver** la porte ARX (elle refuse correctement S03).
- **Développement justifié** : un critère d'erreur **déduit du bruit** pour l'identification ARX, comme pour la voie par échelons, afin d'éviter la limite saisie ; à étudier sur A (développement), calibrer sur B, comparer sur C. Les ensembles B de S01 et d'autres jeux restent à employer ; S04 ne fournit pas assez de répétitions indépendantes de l'excitation pour trancher.
