# UI-03A — Diagnostic graphique de l'identification

## Objectif et contraintes de source
Améliorer les fonctions d'analyse visuelle d'une étude d'identification **déjà enregistrée**, sans modifier le contenu source des essais, les paramètres estimés, leur validation indépendante ni le stockage SQLite.

MKR dispose dans `IdentificationResult` de deux diagnostics distincts (`Training` et `Validation`), chacun contenant des points `FitPoint(Seconds, Observed, Predicted)`. Les données de chaque diagnostic débutent après l'échelon. Le moteur plafonne volontairement les points de tracé à 500 pour les modèles qui suivent cette convention ; les indicateurs scientifiques sont calculés sur tous les points utiles, pas seulement les points affichés.

## Fonctionnalités du sous-lot
- Sélecteur **Estimation / Validation indépendante**.
- Affichage PV mesurée (bleu) et prédite (orange pointillé) en unité PV.
- Graphique de résidu `r = PV_mesurée - PV_prédite`, en unité PV, avec une référence zéro.
- Axe de temps en secondes **après l'échelon**, sans prétendre représenter des timestamps absolus.
- Zoom par glissement horizontal dans le graphique ; sélection d'au moins deux points représentés.
- Bouton de retour à l'horizon complet. Réinitialisation du zoom lors du changement d'étude ou d'essai.
- Les métriques et avertissements scientifiques de l'étude restent affichés et inchangés.

## Garanties
- Aucune opération d'estimation déclenchée par le zoom.
- Aucun filtrage, interpolation, rééchantillonnage ou écrasement de données enregistrées.
- Aucun changement des modèles Python, du mapping des variables, de la persistance SQLite ni du moteur de calcul.
- Aucun fichier de données ou paramètre constructeur inventé.

## Limites actuelles
- **Le zoom est seulement un zoom d'affichage**, pas une sélection de nouvelle fenêtre d'identification. Modifier l'échantillon d'estimation exigera une spécification scientifique séparée, une trace des périodes sélectionnées, une validation indépendante et des changements au moteur.
- Les points affichés peuvent être un sous-ensemble des observations initiales ; le zoom ne récupère donc pas des points qui ne sont pas dans `FitDiagnostics.Points`.
- Un graphique de résidus aide au diagnostic, mais ne suffit pas à valider une hypothèse statistique ni à qualifier le modèle industriellement.
- Les couleurs doivent aussi rester explicites par la légende; ne pas inférer qualité à partir de leur apparence.

## Recette exigée
1. CI Windows : restauration, compilation WPF Release, tests Python et smoke .NET–Python–SQLite.
2. Sélection d'une étude contenant un modèle enregistré : comparer le graphe d'estimation et celui de validation.
3. Vérifier que la forme, l'unité et le signe du résidu correspondent à `Observed - Predicted`.
4. Vérifier que les deux séries de l'essai choisi ont le même horizon et restent liées au bon import.
5. Zoom et reset avec au moins deux points et avec sélections trop courtes; changement d'étude et d'essai.
6. Vérifier aucun changement de résultat persisté, K, τ, θ ou métriques RMSE/MAE.
7. Windows 100 %, 150 %, 200 % DPI, souris, clavier, résolution minimale, fermeture et réouverture.
8. Jeux synthétiques représentatifs : gain positif/négatif, retard fractionnaire, courbe plate, résidu nul et données très bruitées.

## Statut
- Code GitHub écrit : oui.
- Compilation Windows / exécution graphique : **en attente de preuve**.
- Qualification scientifique sur historique industriel : **non effectuée**.
- Prochain sous-lot : inspection des courbes MV/PV brutes, sélection traçable des périodes d'estimation et de validation, mise à jour du contrat scientifique et des tests d'identifiabilité.
