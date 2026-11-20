# UI-04A — Comparaison industrielle des simulations PI

## Objectif et inspiration
Les interfaces de comparaison PID telles que LOOP-PRO Tuner et MATLAB PID Tuner mettent la réponse temporelle et les compromis de réglage au centre de la décision. MKR adopte **les principes généraux** d'affichage simultané et de mesure de performance, sans reproduire les interfaces ni leurs algorithmes propriétaires.

## Fonctionnalités implémentées
- Trois zones WPF cohérentes avec le design system MKR : scénario, indicateurs scientifiques, analyse temporelle.
- Trois graphiques synchronisés sur le **même axe du temps** :
  1. ΔPV simulée IMC/Lambda (bleu), ΔPV simulée SIMC (orange) et ΔSP constant (vert pointillé) ;
  2. commande IMC : ΔCV calculée avant limitation (pointillé bleu) et ΔMV appliquée (vert), avec limites MV ;
  3. commande SIMC : ΔCV calculée (pointillé orange) et ΔMV appliquée (vert), avec limites MV.
- Instant de perturbation indiqué par un repère vertical violet si une perturbation non nulle est demandée.
- Résultats et fenêtres d'évaluation de la réponse affichés **séparément** pour IMC et SIMC.
- Utilisation exclusive des séries `SimulationPoint` déjà enregistrées dans SQLite, sans relancer le moteur lors de la consultation.
- Export CSV existant et historique des simulations conservés. Aucun changement du moteur Python, ni des schémas SQLite.

## Terminologie scientifique
- ΔSP : consigne en variables d'écart autour du point initial.
- ΔPV : réponse du procédé simulé.
- ΔCV : calcul PI **avant** écrêtage et limitation de vitesse.
- ΔMV : commande de l'actionneur **appliquée** après contraintes.
- La perturbation est un écart ajouté dans le modèle au signal injecté au procédé ; il ne faut pas la confondre avec la commande de l'actionneur.
- Le dernier point de simulation ne comporte **pas** de commande CV/MV puisqu'il ne débute aucun nouvel intervalle.

## Limites
- Graphiques consultatifs; pas de nouveau réglage PID ni de nouvelle stratégie anti-windup.
- Les axes sont indépendants entre PV et commandes pour ne pas comparer visuellement des unités physiques incompatibles.
- Les comparaisons sont sur un modèle FOPDT nominal exploratoire; pas une preuve de robustesse industrielle ni une qualification de bloc constructeur.
- Il reste à développer les curseurs, le zoom graphique et une matrice de scénarios multiprojets avec comparaison côte à côte.
- Les tests d'interface requièrent une exécution Windows réelle à plusieurs DPI.

## Recette
1. `dotnet restore MKR.slnx`, `dotnet build MKR.slnx -c Release --no-restore`, `dotnet run --project tests/Mkr.Smoke -c Release --no-build` sous Windows.
2. `python -m unittest discover -s engine -v` pour vérifier les résultats numériques sans régression.
3. Ouvrir une simulation enregistrée, comparer les points de courbe à l'export CSV existant : CV et MV doivent correspondre.
4. Vérifier la présence des bornes MV, un scénario saturé, un scénario avec vitesse maximale, un scénario perturbé, puis un scénario sans perturbation.
5. Vérifier le dernier point sans CV/MV, et l'affichage des deux fenêtres d'évaluation.
6. Vérifier WPF réel sur Windows en 100 %, 150 % et 200 % DPI et fenêtres minimales/maximisées.

Statut : code enregistré sur une branche dédiée ; validation CI et tests visuels Windows distincts.
