# P0-1 — Erreur-type du gain FOPDT sous-estimée

## Anomalie
`engine/fopdt.py::conditional_errors` publiait des erreurs-types **conditionnelles** à la ligne de base mesurée : l'incertitude de cette ligne de base (moyenne de quelques échantillons bruités avant l'échelon) n'était pas propagée. SOPDT et intégrateur la propagent déjà (`model_drift.py:89-93`). Le gain, surtout fixé par le niveau du plateau, est le paramètre le plus touché.

## Preuve
Test `engine/test_fopdt_error_coverage.py` (Monte-Carlo, 80 graines fixes 300–379, vérité connue K=1,5 ; τ=20 s ; θ=4 s ; bruit σ=0,03 ; générateur analytique indépendant de l'estimateur ; critères fixés a priori : rapport ∈ [0,8 ; 1,25], couverture à ±2 erreurs-types ≥ 88 %). **Avant correction : 2 échecs sur 2.**

## Impact
Les intervalles de gain publiés étaient trop étroits d'un facteur ≈ 2 : l'incertitude du réglage guidé (`PythonDiagnostics.cs:326-333`) et les jugements de robustesse « le modèle exclut-il les valeurs nécessaires ? » étaient trop optimistes. Un modèle pouvait paraître mieux déterminé qu'il ne l'était.

## Référence
Propagation de l'incertitude d'une entrée connue avec erreur dans un estimateur par moindres carrés non linéaires : approximation du premier ordre de la covariance, Ljung (1999) ; Söderström & Stoica (1989) — **références non encore vérifiées à la source** (voir `docs/science/references/`). Aucune formule non standard n'est introduite : c'est la propagation linéarisée Σ = σ²(JᵀJ)⁻¹ + s_b²/n_b · (JᵀJ)⁻¹Jᵀ1 · 1ᵀJ(JᵀJ)⁻¹, identique à celle déjà employée pour SOPDT et l'intégrateur dans le dépôt.

## Correction
`engine/fopdt.py` : ajout du terme de ligne de base lorsque `baseline_samples > 1` (même hypothèse que `model_drift.numerical_errors`). Aucun seuil ni critère d'acceptation modifié ; seule la valeur publiée de l'erreur-type change (en augmentant).

## Résultats avant / après (80 tirages, mêmes graines)
| Ligne de base | Paramètre | Rapport avant | Rapport après | Couverture ±2 SE avant | après |
|---|---|---|---|---|---|
| 5 points | gain | 0,344 | **0,977** | 0,545 | **0,922** |
| 10 points | gain | 0,483 | **1,055** | 0,697 | **0,934** |
| 20 points | gain | 0,598 | **1,025** | 0,759 | **0,987** |
| 10 points | τ | 1,115 | 1,115 | 0,961 | 0,961 |
| 10 points | θ | 0,778 | 0,939 | 0,895 | 0,961 |

(Données brutes : sorties du script de mesure du LOT 0b, mêmes graines avant/après.)

## Limites
Domaine : échelon bien conditionné, bruit gaussien indépendant, ligne de base stabilisée. Non vérifié : bruit coloré, plateau non stabilisé (autre anomalie, LOT 3), données réelles.
