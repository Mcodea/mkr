# ADR 0002 — Identification à partir d’échelons mesurés

Statut : implémenté pour revue.

Pour le premier FOPDT, ajuster la réponse exacte d’un premier ordre à un échelon. Un estimateur général avec convolution de commandes arbitraires couvrirait plus de cas, mais demanderait une qualification de l’interpolation, du maintien, du retard et de l’excitation. Un réglage déterminé par lecture graphique de deux points amplifie les erreurs de mesure et n’utilise qu’une fraction des observations.

NumPy et SciPy fournissent les calculs et l’optimisation bornée. Les dépendances sont épinglées et la première installation Windows utilise des wheels. Le gain est ajusté analytiquement pour chaque couple constante de temps et retard. Les deux fichiers restent dans le projet. L’étude conserve colonnes, unités, tolérances, provenance et diagnostics avec les versions. Toute étude de cette méthode est exploratoire.
