# Réglage guidé : objectif, incertitude, refus

`engine/recommend.py`, opération `recommend_tuning` du moteur. Remplace la saisie d'un temps de réponse par un **objectif de robustesse**.

## Principe
1. Entrée : un modèle **accepté** à la validation (FOPDT, SOPDT ou intégrateur), la période d'échantillonnage, les erreurs-types si elles existent et l'objectif : *robuste* (Ms ≤ 1,4), *équilibré* (≤ 1,7) ou *performance* (≤ 2,0). Ces valeurs sont des **objectifs de conception** choisis par l'utilisateur dans la plage usuelle de 1,2 à 2,0 (Åström et Hägglund, 2006), pas des seuils cachés.
2. Retard effectif : retard prudent du modèle + ½ période d'échantillonnage (Skogestad, 2003).
3. Structure : modèle du premier ordre → PI ; second ordre avec second pôle supérieur au retard → PID (règle SIMC, forme série convertie en forme idéale) ; second pôle ≤ retard → règle de la moitié, puis PI ; intégrateur → PI (SIMC intégrateur).
4. Recherche : constante de temps de boucle fermée τc sur une grille de 0,25 à environ 53 retards effectifs. MKR retient la **plus petite** τc (réglage le plus rapide) pour laquelle la boucle est stable et Ms ≤ limite **sur le modèle nominal et sur les huit coins** de l'incertitude (±2 erreurs-types : gain, constante de temps lente, retard).
5. Sortie : le réglage, ses marges au pire cas, les réglages voisins pour comparer vitesse et robustesse, les avertissements (`closed_loop_slower_than_the_process`, `uncertainty_not_quantified`…).

## Refus
MKR refuse, sans fournir de valeur, quand l'incertitude exclut le modèle (par exemple le signe du gain ou une constante de temps positive n'est plus établi) ou qu'aucun réglage de la grille ne respecte l'objectif sur l'ensemble d'incertitude. Le refus liste les raisons et les actions : essai plus long ou plus ample, objectif moins exigeant en connaissance du compromis, structure différente.

## Limites
Modèle linéaire, boucle idéale nominale, sans limites d'actionneur ni filtre de dérivée. Les erreurs-types sont conditionnelles et seulement calculées pour FOPDT ; sans elles le résultat l'annonce. Le résultat est marqué `model_based_unqualified` : une simulation de l'actionneur réel et un essai sur banc restent nécessaires.

## Vérification
`engine/test_recommend.py` : formules SIMC et conversion série→idéale ; pic de sensibilité sur les coins par balayage fréquentiel écrit dans le test ; minimalité (le réglage plus rapide viole l'objectif) ; boucle simulée dans le temps ; monotonie (incertitude plus grande ou objectif plus exigeant → jamais plus rapide / jamais plus lent) ; refus ; entrées hostiles. `engine/test_reference_campaign.py` : 8 procédés de référence (voir l'audit).
