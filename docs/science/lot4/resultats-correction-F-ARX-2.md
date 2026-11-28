# Correction F-ARX-2 (arx 1.1.0) — vérification sur graines neuves 3000–3199

Pré-enregistrement : addendum de `protocole-critere-arx-automatique.md` (commit antérieur à la modification du moteur). Données synthétiques à vérité de structure connue ; **graines jamais utilisées**, 200 cas par population et par bruit ; données : `resultats-correction-F-ARX-2.json` ; script `tests/reference/real_data/arx_gate_burnin_study.py`. Comparaison `arx 1.0.0` (amorçage désactivé) / `arx 1.1.0` (amorçage 1 %), même code par ailleurs.

## Cause (établie avant la correction)
La simulation libre est amorcée sur les premiers échantillons **mesurés** (bruités). L'erreur d'état initial décroît avec le pôle dominant et rend les premiers résidus autocorrélés ; le test de blancheur la lit comme une erreur de structure. Contrôle : avec le **vrai** modèle, 35 % des cas étaient refusés (21/60) ; après exclusion du transitoire, 3 % (2/60).

## Résultats — taux d'acceptation (limite fixe de 10 % de l'étendue ; la règle automatique 3 σ̂ donne les mêmes tendances)
| Population | σ | arx 1.0.0 | arx 1.1.0 |
|---|---|---|---|
| P1 FOPDT (adéquat) | 0,01 / 0,04 / 0,10 | 96,0 / 96,5 / 97,0 % | 97,0 / 97,5 / 97,0 % |
| P2 SOPDT (adéquat) | 0,01 | 56,5 % | **74,0 %** |
| | 0,04 | 72,0 % | **83,5 %** |
| | 0,10 | 82,5 % | **92,0 %** |
| P3 Hammerstein (inadéquat) | 0,01 / 0,04 / 0,10 | 41,0 / 52,0 / 34,5 % | **identique** |
| P4 gain non invariant (inadéquat) | 0,01 / 0,04 / 0,10 | 0,0 / 0,0 / 1,5 % | **identique** |
(Règle automatique 3 σ̂ : P2 53,0 → 72,0 % à σ = 0,01 ; P3 0,0 / 12,5 / 33,0 % et P4 0,0 / 0,0 / 2,0 % inchangés.)

## Critères d'acceptation fixés d'avance
- (a) puissance : **satisfait** (P1+P2 jamais en baisse ; P2 à σ = 0,01 : +17 points avec la limite fixe de 10 %, +19 avec 3 σ̂).
- (b) faux positifs de structure : **satisfait** (variation nulle sur P3 et P4 à tous les bruits).
- (c) **S03 reste refusé** aux quatre niveaux de limite (2, 5, 10, 20 %).
- (d) **523 tests Python du moteur passent** sans modification (4 ignorés comme avant).

## Décision et limites
Correction **adoptée** (`arx 1.1.0`). **Il reste une abstention** : à σ = 0,01, 26 % des modèles adéquats P2 sont encore refusés (cause non étudiée : retard, optimisation, ou structure ARX(2,2) comparée à la vérité en deux constantes de temps) ; à ne pas minimiser. Données synthétiques, bruit blanc, un seul échantillonnage ; la vérité est la structure, pas l'aptitude à la régulation. La limite d'erreur saisie par l'utilisateur subsiste (F-ARX-1) : le critère automatique reste non adopté.
