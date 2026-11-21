# Identification FOPDT sur essais d’échelon

Dans le projet, importer deux CSV historiques du même procédé issus d’essais en boucle ouverte, avec MV appliquée et PV mesurée. Ouvrir **Identification** et choisir FOPDT, sélectionner les deux imports, associer temps/MV/PV, déclarer les unités communes et indiquer les tolérances absolues des plateaux MV et de la PV avant échelon dans les unités correspondantes. La fenêtre demande de confirmer les conditions de test avant estimation.

## Méthode 1.1.0

MKR-SPEC-001 §6 définit `G(s) = K exp(−θs)/(τs + 1)`. Sa réponse à un échelon est `PV(t) = PV₀ + K ΔMV (1 − exp(−max(t−θ,0)/τ))`. `PV₀` est la moyenne avant échelon, `ΔMV` la différence signée des moyennes des deux plateaux. K est exprimé en unité PV par unité MV, τ et θ en secondes. L’origine est le premier échantillon de MV qui s’écarte de plus de deux tolérances MV du premier point. L’instant réel de l’échelon est connu à la résolution de l’intervalle précédent, affichée dans les diagnostics enregistrés.

Au moins quatre points avant l’échelon et douze à partir de l’échelon sont requis. Les horodatages UTC sont strictement croissants. La MV doit rester dans les tolérances déclarées autour de ses plateaux, l’amplitude dépasser dix tolérances MV et la PV stationnaire avant échelon satisfaire la tolérance PV. L’excursion maximale de PV doit dépasser quatre tolérances PV. Les amplitudes absolues des deux essais doivent avoir un rapport entre 0,5 et 2. Les fenêtres temporelles ne peuvent pas se recouvrir. Les valeurs manquantes et non finies empêchent le calcul.

Après fixation de τ et θ, K minimise analytiquement la somme des carrés des résidus post-échelon. Le solveur SciPy ajuste le logarithme de τ et le retard relatif à l’horizon via douze départs déterministes. Bornes : τ entre le quart du pas médian post-échelon et l’horizon, θ entre zéro et la moitié de l’horizon. Un paramètre proche d’une borne, un retard peu résolu ou un horizon court restent signalés. L’étude affiche notamment RMSE, MAE, R² quand défini et autocorrélation des résidus au retard d’un échantillon. Pour cette dernière, le retard est un ordre de ligne, particulièrement sur échantillonnage irrégulier.

Le deuxième essai est prédit avec les mêmes K, τ et θ en utilisant uniquement son niveau initial et son amplitude mesurés. Il ne contribue à aucun ajustement des paramètres. Les diagnostics utilisent tous les points post-échelon et le tracé en montre jusqu’à 500. L’audit conserve les deux références aux imports, les colonnes, unités, tolérances, versions, résultats et avertissements dans SQLite.

## Domaine et vérification

Interpréter ce résultat comme modèle exploratoire pour le régime testé. La comparabilité physique, la saturation, les perturbations et la causalité du retard apparent restent à examiner. Le logiciel fournit une approximation locale conditionnelle, sans couverture statistique garantie ni verdict de qualité industrielle. Les limites d’amplitude et d’excitation sont des choix initialement conservateurs, documentés pour revue.

Tests : réponse analytique, gain signé et échelon de validation inverse, cadences distinctes, retard nul, échec d’excitation, données invalides, étanchéité des fenêtres et validation dégradée sans effet sur l’estimation. Référence numérique : solveur [SciPy least_squares](https://docs.scipy.org/doc/scipy/reference/generated/scipy.optimize.least_squares.html).

### Erreurs-types locales conditionnelles

Pour les points d’estimation, on linéarise la réponse dans les coordonnées gain d’échelon normalisé, log(τ) et retard mis à l’échelle par τ. Si la matrice de sensibilités J a un rang numérique suffisant, on calcule les erreurs-types à partir de la diagonale de s²(JᵀJ)⁻¹ par décomposition en valeurs singulières. La variance résiduelle s² vaut SSE/(n−3). Les erreurs-types sont reconverties dans les unités des paramètres. Elles sont conditionnelles aux paliers, à l’origine de temps et aux écarts de mesure supposés indépendants et de variance constante.

Cette approximation est omise si |autocorrélation à un échantillon| dépasse 0,3, en présence d’un avertissement de domaine, si le résidu est presque nul, si le retard coïncide numériquement avec un échantillon ou si le rapport des valeurs singulières est inférieur ou égal à 10⁻⁶. Ces seuils sont des garde-fous révisables. Ils ne valident pas les hypothèses. Cet indicateur ne quantifie ni l’erreur de modèle, ni les incertitudes du niveau initial, de la MV ou de l’origine de l’échelon. Les tests utilisent un bruit blanc à graine fixe et une perturbation autocorrélée.

### Comparaison sur essai indépendant

L’opération IPC `compare_models` reçoit `trials` (les deux essais du contrat
d’identification), `maximumRmse` en unité de PV et `maximumResidualLagOne`
sans dimension. Les deux candidats utilisent les mêmes essais. La sélection
retient le modèle d’ordre minimal satisfaisant les seuils de validation et les
contrôles de résolution. Si les deux candidats échouent, le résultat contient
`selected: null` et les motifs par candidat. Les seuils sont des critères
d’acceptation de l’étude. Chaque ajustement expose aussi `rangeNormalizedRmse`,
la RMSE divisée par l’étendue observée de PV sur l’essai concerné. Une étendue
nulle donne une valeur indéterminée.
