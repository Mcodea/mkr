# Les six écrans — conformité aux maquettes contractuelles

Références : `01_Acquisition_des_donnees.png` … `06_Validation.png` (archive MKR_UI2.zip). Preuves : captures réelles de l'application Windows
produites par la CI (`tests/windows/screenshots.ps1`, étape « 21 »), 7 écrans (le Rapport s'ajoute aux six maquettes) × 2 résolutions
(1920×1080 et 1366×768, fenêtre maximisée = zone de travail) × 2 thèmes = 28 captures, issues du run CI 308 (commit f8de513).
Les données sont synthétiques (essai de démonstration déterministe), jamais présentées comme des mesures réelles.
L'écran Identification a son propre tableau : `conformite-identification.md`.

| Écran | Maquette | État | Écarts assumés / à valider |
|---|---|---|---|
| Acquisition (Données) | 01 | **partiel** | formulaires alignés en grille, aperçu des lignes source, message d'état vide ; **pas de courbe de tendance** (le moteur ne fournit que les premières lignes d'aperçu : rien n'est inventé) ; pas de panneau « État de l'acquisition » ni de « Résumé » comme la maquette |
| Diagnostic | 02 | conforme dans l'esprit | tableau des lignes source, qualité mesurée, constats, provenance ; pas de tendance (même raison) |
| Identification | 03 | conforme avec réserves | voir `conformite-identification.md` |
| Réglage PID | 04 | conforme avec réserves | pas de colonne « actuel / proposé » : le réglage en service n'est pas une donnée de MKR ; objectif et réglage guidés, graphiques de réponse et de commande, comparaison des réglages ; pas de Ziegler-Nichols, pas de temps de réponse imposé |
| Simulation | 05 | conforme avec réserves | à 1366×768 la page défile (hauteur minimale des graphiques) ; panneau de comparaison Lambda/SIMC en place de IMC/SIMC/ZN ; pas de sélecteur d'onglets Échelon/Perturbation/Robustesse (boutons d'analyse dans l'en-tête) |
| Validation | 06 | conforme avec réserves | graphique observé/modèle avec commande, panneau de validation, paramètres du modèle ; tableau des études repliable |
| Rapport (fiche d'étude) | — | ajout | recommandation, paramètres finaux, indicateurs, avertissements, export PDF |

## Système de graphique et de style commun
Mêmes barres d'outils (zoom, vue complète, exports), mêmes titres centrés, axes pivotés, légendes encadrées, curseur unique, panneaux à en-tête bleu,
ascenseurs thémés, dates et nombres selon les réglages régionaux, thèmes clair et sombre issus des mêmes jetons.

## Vérifications automatiques réellement exécutées (Windows, CI)
Zoom avant/arrière (retour exact à la vue complète), déplacement par flèche (passe dans une exécution sur deux : la touche est parfois perdue par
l'automate de test, jamais par l'application), ajuster, synchronisation des trois graphiques, curseur reflété, sélection A–B annoncée et indicateurs
recalculés, effacement de A–B : **réussis**.

## Ce qui n'est PAS prouvé
- **Export CSV/PNG** : l'écriture du fichier n'a pas pu être vérifiée par l'automate à cause de l'instabilité du pilotage de la boîte système
  « Enregistrer sous ». Un dossier de sortie imposable par `MKR_EXPORT_DIRECTORY` (tests uniquement) a été ajouté dans le commit cce3091 ;
  son premier run CI n'était pas terminé au moment de la rédaction. Un export PNG a été constaté valide dans une passe, pas de façon répétable.
- Échelles Windows 125 % et 150 % : non testées (le mécanisme de capture ne les change pas).
- Quelques info-bulles de navigation recouvrent des captures du run 308 (défaut de capture, corrigé depuis ; non vérifié sur nouvelle capture).
- Le jugement visuel est celui de l'auteur ; aucune mesure automatique de similarité avec les maquettes.
