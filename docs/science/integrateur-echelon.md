# Identification d’un procédé intégrateur avec retard

Sélectionner Integrateur dans la fenêtre d’identification. Les contrôles d’import, d’horodatage, de mapping et de plateau MV sont ceux de la [méthode FOPDT](fopdt-echelon.md). La PV doit être stable avant l’échelon. L’interprétation suppose que la MV initiale équilibre le procédé dans le régime étudié.

## Méthode 1.0.0

À partir de `G(s) = Kᵢ exp(−θs)/s`, variante à retard explicite du modèle intégrateur du §6 de MKR-SPEC-001, la réponse à un échelon est `PV(t) = PV₀ + Kᵢ ΔMV max(t−θ,0)`. Kᵢ est exprimé en unité de PV par unité de MV et par seconde. Le modèle d’étude ne contient aucune constante de temps finie.

Le retard est borné entre zéro et la moitié de l’horizon d’estimation. Pour un retard donné, le gain minimise analytiquement la somme des carrés. L’ensemble des points actifs varie aux instants échantillonnés. Pour chaque intervalle de retard entre deux de ces instants, l’algorithme compare la valeur aux bornes et au point stationnaire admissible. Le test de référence compare l’erreur optimale à un balayage indépendant avec résolutions moindres carrées.

Dans les deux dernières fenêtres d’un huitième des échantillons (au moins deux points par fenêtre), la différence des moyennes doit aller dans le sens prédit et dépasser quatre tolérances PV. Un échec sur l’essai d’estimation empêche le calcul de l’étude. Un échec sur l’essai distinct est signalé tout en conservant les erreurs de prédiction. Le garde-fou écarte notamment les plateaux établis, mais ne prouve pas qu’une rampe observée restera intégrante au-delà de l’horizon mesuré. Les erreurs-types de l’intégrateur ne sont pas estimées.

La méthode est exploratoire. Comparer avec les modèles autorégulants sur les mêmes essais lorsque l’intervalle mesuré ne permet pas de distinguer une intégration d’une dynamique lente. Ne pas extrapoler une rampe au-delà des limites physiques du procédé.
