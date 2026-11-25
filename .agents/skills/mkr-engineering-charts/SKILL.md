---
name: mkr-engineering-charts
description: Créer des graphiques scientifiques WPF industriels haut de gamme : sélection de fenêtres, mesures/modèles, résidus, simulations, comparaisons PI/PID et exports traçables.
---

# Visualisation scientifique avancée — MKR

## Objectif
Produire les graphiques de travail d'un ingénieur régulation, pas seulement des illustrations : analyser les mesures, sélectionner les essais, qualifier un modèle et comparer des réglages.

## Flux de conception
1. Examiner la provenance des séries, leurs unités, les horodatages, le contrat métier et les contrôles graphiques existants avant de choisir une bibliothèque.
2. Évaluer les bibliothèques WPF maintenues pour performance, disponibilité, licence, navigation, rendu DPI, export et tests; ne pas imposer de dépendance supplémentaire sans ADR et essai.
3. Séparer les séries mesurées, simulées et dérivées. Utiliser des styles distincts et une légende explicitant leur provenance; CV calculée et MV appliquée doivent être visibles séparément.
4. Temps : support des timestamps UTC et affichage local explicite, irregular sampling, trous sans interpolation trompeuse, alignement des courbes et choix des unités/échelles.
5. Outils indispensables suivant les écrans : zoom rectangulaire et molette, pan, reset, curseur/valeurs, repères d'événements, sélection de plage d'identification/validation, bornes de consigne, seuils, saturation et annotation des avertissements.
6. Ne pas créer de doubles axes trompeurs : chaque axe indique sa grandeur et son unité. Si deux axes sont nécessaires, l'échelle et la correspondance sont évidentes.
7. Identification : superposition réel / FOPDT / SOPDT, affichage des résidus et partition d'étalonnage / validation; pas de courbe inventée si le modèle est invalide.
8. Simulation : comparer IMC/SIMC/actuel, SP/PV, CV/MV, saturation, limite de vitesse et événement perturbation, avec fenêtres de métriques identiques.
9. Sur grandes séries, conserver les originaux et utiliser un décimateur visuel qui préserve les extrêmes; ne pas confondre décimation d'affichage et rééchantillonnage scientifique.
10. Export CSV sans perte de précision (culture invariante) et export image avec titre, version, source, unité et légende. Ne pas afficher une précision numérique non justifiée.
11. La sélection graphique doit se propager à un ViewModel puis à l'étude via commande explicite; ne jamais altérer silencieusement les données brutes.

## Tests et preuves
- Cas analytique, gain positif/négatif, données très bruitées, données manquantes, grand historique, horodatages irréguliers, milliers de points.
- Vérifier curseurs, zoom/pan, sélection, resize, DPI et export sur Windows.
- Tester que les séries affichées correspondent réellement aux résultats du moteur et aux points exportés.
- Consigner les essais UX non exécutés et la performance mesurée plutôt que la supposer.

Livrable : composant graphique réutilisable, workflow de comparaison réel, tests d'intégration et captures vérifiées.
