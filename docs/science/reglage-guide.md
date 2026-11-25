# Réglage guidé : objectif, incertitude, refus

`engine/recommend.py`, opération `recommend_tuning` du moteur. Remplace la saisie d'un temps de réponse par un **objectif de robustesse**.

## Principe
1. Entrée : un modèle **accepté** à la validation (FOPDT, SOPDT ou intégrateur), la période d'échantillonnage, les erreurs-types si elles existent et l'objectif : *robuste* (Ms ≤ 1,4), *équilibré* (≤ 1,7) ou *performance* (≤ 2,0). Ces valeurs sont des **objectifs de conception** choisis par l'utilisateur dans la plage usuelle de 1,2 à 2,0 (Åström et Hägglund, 2006), pas des seuils cachés.
2. Retard effectif : retard prudent du modèle + ½ période d'échantillonnage (Skogestad, 2003).
3. Structure : modèle du premier ordre → PI ; second ordre avec second pôle supérieur au retard → PID (règle SIMC, forme série convertie en forme idéale) ; second pôle ≤ retard → règle de la moitié, puis PI ; intégrateur → PI (SIMC intégrateur).
4. Recherche : constante de temps de boucle fermée τc sur une grille de 0,25 à environ 53 retards effectifs. MKR retient la **plus petite** τc (réglage le plus rapide) pour laquelle la boucle est stable et Ms ≤ limite **sur le modèle nominal et sur les huit coins** de l'incertitude (±2 erreurs-types : gain, constante de temps lente, retard).
4bis. Filtre de la dérivée (PID) : Tf = Td/N, N = 10 par défaut (usuel : 2 à 20) ; il est inclus dans les marges évaluées sur chaque coin.
5. Sortie : le réglage, ses marges au pire cas, les réglages voisins pour comparer vitesse et robustesse, les avertissements (`closed_loop_slower_than_the_process`, `uncertainty_not_quantified`…).

## Réglage optimisé (PI)
En plus du réglage SIMC, MKR calcule le PI qui **maximise le gain intégral Ki = Kp/Ti** sous la même contrainte de sensibilité sur tous les coins de l'incertitude (Åström, Panagopoulos et Hägglund, *Automatica* 34, 1998). Pour une perturbation de charge en échelon, l'erreur cumulée vaut environ 1/Ki : plus Ki est grand, plus la perturbation est rejetée vite. Le compromis est annoncé : davantage de dépassement sur un changement de consigne (dépassement nominal affiché). Pour un PID, l'optimisation n'est pas disponible (`optimisation_covers_pi_only`).

Méthode : pour chaque Ti d'une grille, plus grand Kp stable respectant la contrainte (balayage puis dichotomie, car l'ensemble admissible peut être un intervalle pour un procédé intégrateur), puis affinage de Ti et vérification finale avec la recherche de marges en pleine résolution.

## Variation du procédé sur la plage de fonctionnement
Le champ facultatif « Variation attendue du procédé (%) » élargit la boîte d'incertitude à ±v % du gain, de la constante de temps et du retard (maximum entre cette valeur et ±2 erreurs-types). C'est le moyen de tenir compte d'un procédé dont la dynamique change avec le point de fonctionnement, sans modèle non linéaire.

Mesure du 25/11/2026 (5 procédés synthétiques : débit, pression, température, dosage, chloration), réglage calculé **sans** variation déclarée, jugé ensuite sur le procédé dont gain, constante de temps et retard varient simultanément de ±30 % (huit coins, balayage fréquentiel indépendant) :

| Objectif | Sensibilité maximale observée |
|---|---|
| Robuste (≤ 1,4) | 1,42 à 1,64 |
| Équilibré (≤ 1,7) | 1,69 à 2,25 |
| Performance (≤ 2,0) | 2,17 à 3,01 |

Un réglage « performance » n'est donc pas robuste à une variation de 30 % du procédé ; déclarer la variation attendue rend MKR plus prudent (test `test_a_declared_process_variation_widens_the_uncertainty_box_and_is_met_on_the_true_corners`). Preuve sur données synthétiques uniquement.

## Refus
MKR refuse, sans fournir de valeur, quand l'incertitude exclut le modèle (par exemple le signe du gain ou une constante de temps positive n'est plus établi) ou qu'aucun réglage de la grille ne respecte l'objectif sur l'ensemble d'incertitude. Le refus liste les raisons et les actions : essai plus long ou plus ample, objectif moins exigeant en connaissance du compromis, structure différente.

## Limites
Modèle linéaire, boucle idéale nominale, sans limites d'actionneur ni filtre de dérivée. Les erreurs-types sont conditionnelles et seulement calculées pour FOPDT ; sans elles le résultat l'annonce. Le résultat est marqué `model_based_unqualified` : une simulation de l'actionneur réel et un essai sur banc restent nécessaires.

## Vérification
`engine/test_recommend.py` : formules SIMC et conversion série→idéale ; pic de sensibilité sur les coins par balayage fréquentiel écrit dans le test ; minimalité (le réglage plus rapide viole l'objectif) ; boucle simulée dans le temps ; monotonie (incertitude plus grande ou objectif plus exigeant → jamais plus rapide / jamais plus lent) ; refus ; entrées hostiles. `engine/test_reference_campaign.py` : 8 procédés de référence (voir l'audit).

## PID optimisé et critères de performance (engine/recommend.py, version 1.1.0)
**Statut : implémenté, vérifié sur modèles synthétiques par des oracles indépendants ; non qualifié sur procédé réel.**

- **PID optimisé** : même principe que le PI optimisé (gain intégral Ki = Kp/Ti maximal sous Ms ≤ limite sur **chaque** coin de l'incertitude), avec Td = r·Ti et Tf = Td/N. La recherche porte sur r ∈ {0,05 ; 0,1 ; 0,15 ; 0,2 ; 0,25} et sur Ti ; cette grille est un choix de **recherche** (plage usuelle de Td/Ti, Åström et Hägglund 2006), pas une affirmation : chaque candidat est jugé sur sa sensibilité mesurée. Le critère de stabilité utilise la phase continue exacte (partie réelle du régulateur positive, donc sans repliement) et exige |L| < 1 à chaque croisement de −π (suffisant pour un procédé stable en boucle ouverte, donc prudent). Le résultat est revérifié sur une grille 40 fois plus fine, avec recul de 0,5 % tant que la limite est dépassée.
- **Le PID n'est jamais présumé meilleur** : le résultat publie le rapport de gain intégral au PI optimisé et l'indicateur `pidNotBetterThanPi` ; le prix de la dérivée (gain haute fréquence Kp(1+Td/Tf), amplification du bruit de mesure) est publié à côté. La décision PI/PID reste à l'utilisateur, selon la qualité du capteur.
- **Critères de performance** (`closed_loop_performance`) : simulation temporelle explicite (Euler, tampon de retard, P et I sur l'erreur, D sur la mesure, sans limite d'actionneur) pour l'échelon de consigne (IAE, ISE, ITAE, dépassement, établissement à 2 %) et l'échelon de perturbation en entrée du procédé. Précision d'ordre 1 en pas (écart à la forme fermée divisé par 2 quand le pas est divisé par 2 : test).
- **Vérification** (`engine/test_recommend.py`) : sensibilité maximale et établissement du PID jugés par une balayage dense et une simulation écrits séparément du module sur chaque coin ; boucle intégrale exacte (Ti = τ, sans retard : IAE = τ/a, ISE = T/2, ITAE = T², établissement à T·ln 50, charge IAE = Kτ/a) comme oracle des critères ; convergence en pas ; sérialisation JSON (défaut corrigé : des scalaires numpy remontaient par le pont comme `engine_non_finite`).
- **Limites** : procédé linéaire, sans limite ni vitesse d'actionneur, perturbation à l'entrée du procédé seulement, grille Td/Ti discrète. **Intégrateurs (25/11)** : le PID optimisé et les critères de performance existent aussi pour un procédé intégrateur K e^(−θs)/s (même contrainte sur les coins pente × retard) ; oracle des critères : boucle exacte sans retard (PI sur intégrateur, fonction de transfert évaluée par scipy : IAE de consigne, dépassement dû au zéro du PI, IAE de charge = Ti/Kp).
