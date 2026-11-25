# Composant graphique commun (`SignalChart`)

Composant interne (aucune bibliothèque tierce, donc aucune licence). Remplace progressivement les tracés dessinés écran par écran.

| Fonction | Réalisation | Preuve |
|---|---|---|
| Pistes synchronisées | Plusieurs pistes, un seul axe du temps, un seul zoom, un seul curseur | Capture CI `04b-courbes-sources` |
| Échelle verticale par piste, unités | Axe Y adapté à la fenêtre visible ; unité dans le titre de la piste | idem |
| Graduations rondes | `ChartMath.NiceTicks` (pas de 1, 2 ou 5 × 10^k) | Test banc `Smoke` |
| Grands historiques | `ChartMath.Decimate` : min/max par colonne de pixels, un pic isolé n'est jamais perdu | Test banc `Smoke` (100 000 points, pic d'un échantillon) |
| Zoom, déplacement | Molette (autour du pointeur), clic droit glissé, flèches, + / −, double-clic ou Origine pour la vue complète | Tests de `ChartMath.Zoom/Pan` ; interaction à vérifier sur poste Windows |
| Curseur synchronisé | Ligne verticale sur toutes les pistes, valeurs dans une bulle et sous le graphique (texte lisible par lecteur d'écran) | À vérifier sur poste Windows |
| Légende interactive | Clic = masquer/afficher une courbe ; chaque courbe a aussi un motif de trait distinct (pas la couleur seule) | À vérifier sur poste Windows |
| Sélection de plage | Clic gauche glissé (écrans qui l'activent) | Existant (essais) |
| Export | PNG de la vue, CSV de toutes les séries (format long, unités, culture invariante) | À vérifier sur poste Windows |
| Thèmes | Couleurs lues dans les jetons du thème ; redessin au changement de thème | Test de contraste |
| Trous de données | Valeur non finie = rupture de trait, jamais interpolée | Relecture de code |

Écrans adoptés : prévisualisation des signaux, espace d'étude, diagnostic d'identification, simulation PI (enveloppes de sensibilité, marqueur de perturbation), prototype PID, simulation en boucle ouverte. Il ne reste plus de tracé dessiné à la main. Hauteurs de pistes pondérées (signal principal ×2). Mode « Agrandir » dans l'espace d'étude.
Limites : pas d'axes multiples sur une même piste, pas de curseurs à deux repères, pas de test de performance chronométré, rendu non vérifié à 4K/150 %.
