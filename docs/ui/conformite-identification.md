# Écran Identification — conformité à la maquette 03

Référence : `03_Identification_du_procede.png` (archive MKR_UI2.zip). Preuves : captures réelles de l'application Windows produites par la CI
(`tests/windows/screenshots.ps1`, étape « 10h »), thèmes clair et sombre, écrans de 1920×1080 et de 1366×768 (fenêtre maximisée : la zone de travail
est celle de l'écran moins la barre des tâches, soit 1920×1032 et 1366×720). Les données montrées sont synthétiques (essai de démonstration avec bruit
de mesure déterministe), jamais présentées comme des mesures réelles.

| Élément de la maquette | État | Remarque |
|---|---|---|
| Barre de titre avec logo, titre, boutons de fenêtre | conforme | barre propre à MKR ; zone de travail du moniteur respectée |
| Menus, barre d'outils, en-tête MKR / projet / étude | conforme | barre d'outils masquée sous 820 px de haut (elle double la navigation) |
| Navigation à gauche, puces d'étapes, barre d'état avec « Suivant » | conforme | les puces suivent le parcours réel du produit (pas d'étape « Stratégie ») |
| Graphique principal en haut (mesure et modèle) | conforme | titre centré, axe vertical pivoté, légende encadrée dans le tracé |
| Commande et Résidus côte à côte dessous, temps commun | conforme | curseur et zoom partagés ; barre de séparation redimensionnable |
| Repères A et B et zone teintée | conforme avec réserve | par défaut : intervalle de l'ajustement ; glisser pour restreindre (indicateurs recalculés, sans nouvelle estimation) |
| Barre d'outils des graphiques à icônes | conforme dans l'esprit | sélection, zoom, ajuster, effacer A–B, essai, exports, plein écran |
| Fonction de transfert composée | conforme | fraction avec exposant, sans bibliothèque tierce |
| Paramètres du modèle | conforme | valeurs et unités réelles |
| Qualité du modèle | conforme | FIT (erreur normalisée des points tracés), RMSE, R², IAE |
| Comparaison des modèles | écart assumé | lignes issues d'un calcul réel du moteur (FOPDT, SOPDT) ; FIT affiché seulement si une série ajustée est enregistrée ; pas de ligne « Intégrateur » sans étude correspondante |
| Résultats visibles sans défilement à 1366×768 | conforme | formule, paramètres, qualité, comparaison |
| Boîtes de dialogue d'essais, critères, ARX, réglage, rapport | adaptation | non présentes dans la maquette ; page « Essais et actions » du panneau de droite |

Limites : échelles Windows 125 % et 150 % non testées (le mécanisme de capture ne les change pas) ; icônes et polices un peu plus petites que la maquette à 1366 px ;
jugement visuel de l'auteur, à valider par le demandeur (pas de mesure automatique de similarité).
