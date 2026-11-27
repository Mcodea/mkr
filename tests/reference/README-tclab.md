# Données TCLab — statut : fichier retiré du dépôt (2026-11-27)

`tclab_dyn_data2_extract.csv` (121 points, boucle ouverte, deux chauffages H1/H2, températures T1/T2, un point toutes les 5 s sur 600 s) a été **retiré du dépôt courant** sur décision du client, tant que les droits d'utilisation et de redistribution ne sont pas établis.

## Provenance et problème de licence
- **Provenance indiquée à l'origine** : APMonitor, « TCLab D — Empirical Model Estimation » (protocole https://apmonitor.com/do/index.php/Main/TCLabD ; mesures https://apmonitor.com/do/uploads/Main/tclab_dyn_data2.txt).
- **Fidélité non établie** : le fichier était une transcription de l'extrait publié, fournie dans un classeur (« MKR_TCLab_Donnees_reelles.xlsx ») ; il n'a jamais pu être comparé à la source (hôte bloqué à l'époque).
- **Licence** : aucune licence explicite trouvée sur la source (voir `docs/science/licences/jeux-de-donnees.md`) ; une donnée accessible publiquement n'est pas nécessairement redistribuable.
- **Historique Git** : le fichier est présent dans le commit `0a7407e` et donc dans l'historique des branches publiées (`claude/peaceful-cray-phk6q4`, `claude/science-engine-dev`). Il n'a **pas** été purgé de l'historique : voir `docs/science/licences/tclab-historique-git.md`.

## Utilisation qui en était faite
`engine/test_real_tclab_dynamic.py` : régression des refus du moteur sur ces mesures (aucun échelon exploitable, ARX refusé, contrôle indépendant par moindres carrés). Le test est **conservé, ignoré (skip) tant que la donnée est absente** ; ses assertions n'ont pas été modifiées.

## Couverture de remplacement (sans donnée réelle)
`engine/test_two_heater_unmodelled_input.py` : **données synthétiques** (deux chauffages couplés, récurrence exacte, graine fixe) avec les mêmes instants de commande ; critères posés par la théorie avant exécution. Ce test **ne remplace pas** la donnée réelle : il ne couvre ni les non-linéarités ni le bruit réels. Tant qu'un jeu réel autorisé n'est pas intégré, le niveau de qualification revendiqué est limité en conséquence.

## Contrôle d'échelon observé (jeu externe, non redistribué)
`tests/reference/tclab_observed_step.py` est exécuté par la CI sur un fichier cloné à l'exécution depuis le dépôt public `jckantor/CBE30338-book` (aucune copie dans ce dépôt). Sa licence n'est pas établie : usage de test interne seulement ; équivalent synthétique ajouté dans `test_two_heater_unmodelled_input.py`.

## Alternative recherchée
Jeu réel sous licence permissive (recherche en cours : Zenodo « TCLab Take-Home Labs », CC BY 4.0 annoncée, à confirmer sur la page officielle) : `docs/science/licences/`.
