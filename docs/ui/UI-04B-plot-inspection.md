# UI-04B — Inspection interactive des simulations PI

## Objectif
Fournir une lecture de la comparaison IMC/Lambda et SIMC comparable, dans son principe, aux workflows d'ingénierie des outils de réglage existants. Les valeurs affichées proviennent **uniquement des séries de simulation persistées** ; aucune simulation implicite n'est relancée par les actions graphiques.

## Fonctions de ce lot
- Clic sur un point : recherche de l'échantillon stocké le plus proche dans la plage affichée, lecture du temps, ΔSP, PV IMC, PV SIMC, CV et MV par méthode, sans interpolation.
- Glissement horizontal d'au moins 10 pixels : choix d'une plage temporelle ; il faut au moins trois points de simulation stockés pour l'appliquer.
- Zoom synchronisé sur les trois pistes (PV/SP, commande IMC, commande SIMC), recalcul des échelles graphiques sur les points visibles, sans toucher aux données scientifiques.
- Bouton « Réinitialiser le zoom » ; état du zoom affiché ; remise à zéro automatique quand on choisit une autre simulation enregistrée.
- Curseur temporel commun affiché sur les trois pistes au temps d'un échantillon source.
- Les valeurs CV et MV manquantes du dernier point, sans intervalle de commande, restent explicitement affichées comme indisponibles « — ».

## Définitions
ΔCV : commande théorique avant écrêtage/limitation de vitesse.
ΔMV : commande réellement appliquée dans la simulation.
Les métriques IAE, ISE, saturation et durée à vitesse limitée continuent à provenir du moteur Python, et NON du zoom ou du tracé.

## Limites
- Le zoom est visuel. Il ne recalcule ni les métriques globales ni les réglages PI.
- Le clic sélectionne le point enregistré le plus proche, sans interpoler.
- Les événements de saturation sont inspectables par comparaison de CV et MV, mais aucun marquage automatique de leurs causes n'est encore ajouté. La différence CV/MV peut résulter de la saturation, de la limitation de vitesse, ou des deux.
- Le contrôle WPF reste une interface exploratoire, non un outil qualifié de réglage en ligne.
- Aucun accès PLC ni modification de blocs constructeur.

## Critères d'acceptation
1. Compiler WPF Release sous Windows et exécuter les tests scientifiques Python et smoke .NET–SQLite.
2. Charger une simulation enregistrée, cliquer en plusieurs instants et vérifier exactement les valeurs du CSV exporté.
3. Tester le dernier point : CV/MV indisponibles.
4. Tester les trois pistes en zoom et l'absence de changement des séries, métriques et export.
5. Tester un zoom inférieur à trois points (rejet) et le bouton de remise à zéro.
6. Vérifier mise à jour de l'état du zoom et remise à zéro au changement de simulation.
7. Inspection graphique Windows manuelle aux DPI 100 %, 150 %, 200 % et en taille de fenêtre minimale.

## Prochaines améliorations
Marquage ciblé des périodes de saturation/limitation de vitesse à partir des signaux enregistrés et des métriques moteur, export d'images scientifiques et scénario multi-études.
