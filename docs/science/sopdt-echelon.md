# Identification SOPDT sur essais d’échelon

Sélectionner SOPDT dans la fenêtre d’identification. Les deux essais, les tolérances et le mapping suivent les contrôles de la [méthode FOPDT](fopdt-echelon.md). Les paramètres sont évalués sur un deuxième essai distinct. Pour comparer les familles, créer deux études avec les mêmes fichiers, colonnes et tolérances, puis relever les erreurs de prédiction du deuxième essai et les avertissements de chaque étude.

## Méthode 1.0.0

MKR-SPEC-001 §6 définit `G(s) = K exp(−θs)/((τ₁s + 1)(τ₂s + 1))`. Le modèle proposé couvre deux constantes réelles positives. Avec `x = max(t−θ, 0)`, la réponse normalisée vaut `1 − (τ₁e^(−x/τ₁) − τ₂e^(−x/τ₂))/(τ₁−τ₂)`. Pour un pôle double, la limite est `1 − (1 + x/τ)e^(−x/τ)`. Le calcul emploie une forme stable avec `expm1` et la limite exacte pour les constantes égales. La PV prédite est le niveau initial plus cette réponse multipliée par K et par l’amplitude MV signée.

Le gain est estimé analytiquement à chaque itération. SciPy minimise la somme des carrés par 27 départs déterministes bornés. Les constantes sont ordonnées, entre le quart du pas médian et l’horizon d’estimation. Le retard varie de zéro à la moitié de cet horizon. La seconde constante est paramétrée entre la borne basse et la première. Les bornes, les pôles proches, une constante rapide sous le pas médian, le retard peu résolu, l’horizon court (moins de cinq constantes lentes après retard) et la corrélation résiduelle sont signalés.

Les études SOPDT sont exploratoires. La méthode ne fournit pas d’erreurs-types SOPDT. Un RMSE inférieur sur le deuxième essai ne suffit pas à valider les deux pôles physiques. Les tests vérifient une réponse analytique avec deux pôles distincts, la limite du pôle double et la prédiction sur un autre essai de sens inverse. Ils comparent aussi les erreurs FOPDT et SOPDT sur le même cas connu.
