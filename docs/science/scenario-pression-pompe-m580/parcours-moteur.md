# Parcours complet du moteur sur le scénario pression / pompe (tranche moteur du LOT 9)

> **DONNÉES SYNTHÉTIQUES.** Aucun résultat ci-dessous ne vient d'une installation réelle. Le générateur (`tests/reference/scenario_pression_pompe/generate.py`) n'importe rien de `engine/` : ses paramètres vrais et sa boucle fermée simulée sont des oracles indépendants. Test : `engine/test_scenario_pipeline.py` (6 tests, à travers le contrat IPC).

Statut : **TESTÉ AUTOMATIQUEMENT** (AGENTS.md §8). Pas de validation métier, pas de qualification, pas de banc.

## Chaîne exercée
critères suggérés → identification FOPDT → porte de validation → comparaison FOPDT/SOPDT → réglage (refus sans preuve de validation) → conversion PIDFF et PID_INT → simulation PI → comparaison à la boucle fermée du procédé vrai.

## Résultats mesurés (jeu `ol_nominal`, échelon MV 85 → 90 %, bruit 0,003 bar)
| Grandeur | Vrai (ajustement sans bruit du procédé d'ordre 4) | Identifié | Écart |
|---|---|---|---|
| Gain (bar/%) | 0,097065 | 0,097117 | 0,05 % |
| Constante de temps (s) | 4,6666 | 4,6586 | 0,17 % |
| Retard (s) | 1,2565 | 1,2526 | 0,31 % |

- **Réglage** : PI, kp = 17,5 %/bar, Ti = 4,66 s ; égal à la formule SIMC recalculée à la main (retard conservateur + Ts/2) à 0,1 % ; Ms mesuré par un balayage fréquentiel indépendant sur le **procédé vrai** ≤ 1,7.
- **Simulation contre boucle fermée du procédé vrai** (régulateur réellement appliqué dans `cl_pi`, échelon de consigne 4,0 → 4,3 bar à 60 s, avant la perturbation de vanne) : écart quadratique modèle/observation **0,0042 bar**, soit 1,4 % de l'échelon, pour un bruit de 0,003 bar. Critère fixé avant mesure : 10 % de l'échelon.
- **Conversion PID_INT** : KP = 175, TI = 47 (dixièmes de seconde), TS = 10 ; la troncature entière laisse une zone morte d'intégration d'environ **0,026 bar**, soit environ 9 fois le bruit de mesure : l'avertissement `integer_dead_zone` doit être montré à l'utilisateur.

## Constat F-LOT9-1 — à arbitrer (LOT 4), pas à contourner
Avec les critères **par défaut** de la porte de validation, **aucun modèle n'est validé** sur ce scénario : l'erreur quadratique (0,0044 bar) est proche du bruit, mais les résidus restent autocorrélés (lag 1 : FOPDT 0,42, SOPDT 0,18, borne de blancheur 0,105) parce que le procédé vrai est d'ordre 4 et non linéaire. La comparaison ne retient donc aucun modèle (`selected: null`) et le moteur refuse de calculer un réglage. Le manifeste du scénario annonçait pourtant `intent: accepted`.

Ce n'est pas un défaut démontré : un critère de blancheur est le critère scientifique (Ljung), et il est strict à fort rapport signal/bruit. Mais le parcours complet du scénario n'est atteignable aujourd'hui qu'avec un critère **déclaré par l'humain** (`maximumResidualLagOne` = 0,5 dans le test, jamais le défaut). Décisions possibles, **non prises** : (a) conserver la porte et corriger l'`intent` du scénario ; (b) fonder le critère sur l'erreur de structure relative au bruit ; (c) imposer une revue humaine explicite de l'écart. Le test fige le verdict actuel par `test_F_LOT9_1_…` : il devra être modifié sciemment, avec l'arbitrage, si la porte change.

## Limites
Un seul scénario synthétique, un seul point de fonctionnement, FOPDT seulement dans la chaîne (SOPDT, intégrateur et Broïda exercés ailleurs) ; aucune boucle fermée identifiée ; M580 non couvert (aucune compatibilité établie).
