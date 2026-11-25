# Identification ARX / sortie-erreur avec excitation quelconque

`engine/arx.py`, opération `identify_arx`. Complète l'identification par échelons (FOPDT, SOPDT, intégrateur) : elle accepte des essais **sans régime permanent initial** et une excitation **quelconque** (échelons, impulsions, séquence pseudo-aléatoire). **Statut : implémentée, vérifiée sur données synthétiques ; exposée dans l'écran d'identification (bouton « Identifier par ARX / OE ») ; chaque exécution, refus compris, est enregistrée comme étude ARX ([ADR 0007](../adr/0007-etude-arx-enregistree.md), migration SQLite 11). Elle n'alimente pas encore le réglage guidé.**

## Modèle et méthodes (Ljung, *System Identification: Theory for the User*, 2ᵉ éd., 1999)
Équation aux différences à pas d'échantillonnage régulier Ts :
y(k) = −a₁y(k−1) − … − a_na y(k−na) + b₁u(k−nk) + … + b_nb u(k−nk−nb+1) + d + e(k).
Le terme d absorbe le point de fonctionnement : aucune condition de régime permanent n'est requise. Deux estimateurs, chacun jugé par **ses propres résidus** :
- **ARX** (bruit d'équation) : moindres carrés linéaires ; les résidus de prédiction à un pas doivent être blancs.
- **OE** (bruit de sortie, cas usuel d'un capteur) : les paramètres ARX sont affinés par moindres carrés non linéaires sur l'erreur de **simulation libre** ; les résidus de simulation doivent être blancs. Un ARX simple est biaisé par un bruit de mesure ; l'OE ne l'est pas.

## Critères d'acceptation d'un candidat (série de validation indépendante)
1. modèle stable (pôles dans le cercle unité) ;
2. erreur de simulation libre ≤ RMSE maximale déclarée ;
3. résidus blancs : autocorrélation de retard 1 dans ±2,576/√N (99 %) ;
4. résidus non corrélés aux entrées aux retards 0 à 20 (borne de Bonferroni à 99 % global pour les 21 tests simultanés : sans correction, un modèle correct serait rejeté une fois sur cinq).
Parmi les candidats acceptés, le moins de paramètres l'emporte ; à égalité, la plus petite erreur de simulation. Aucun candidat accepté : refus avec raisons et actions. Le meilleur ajustement seul ne sélectionne jamais un modèle.

## Refus d'entrée
`arx_closed_loop` (données en boucle fermée déclarées : l'ARX serait biaisé, utiliser l'identification en boucle fermée), `arx_excitation` (entrée constante), `arx_sampling` (pas irrégulier : on n'interpole pas les mesures), `arx_ill_conditioned`, `arx_data`, `arx_time`, `arx_mapping`.

## Équivalent du premier ordre
Pour (na, nb) = (1, 1) : équivalent exact du maintien d'ordre zéro, p = −a₁, τ = −Ts/ln p, K = b₁/(1−p), retard = (nk−1)·Ts, avec erreurs-types par la méthode delta. Le retard est un multiple de Ts. Ces valeurs alimentent le réglage guidé.

## Vérification (`engine/test_arx.py`, vérité synthétique générée par l'équation exacte, indépendamment du module)
Gain à 1 %, constante de temps à 3 %, retard en échantillons exact, depuis un départ hors équilibre et un point de fonctionnement décalé (6 essais bruités) ; erreurs-types couvrant la vérité à 3 σ ; processus du second ordre refusé en premier ordre et accepté en second ordre ; chaîne ARX → réglage guidé jugée sur le **vrai** procédé (Ms ≤ 1,8) ; refus : boucle fermée, entrée constante, pas irrégulier, validation non informative, trop peu d'échantillons, mauvais mapping.

## Limites
Procédé linéaire stable, bruit gaussien à peu près blanc, pas uniforme, ordres limités à (1,1) et (2,2), retard jusqu'à 60 pas. Pas de comparaison à des données expérimentales. Pas d'intégrateur ni de retard fractionnaire.
