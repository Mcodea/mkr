---
name: mkr-visual-qa
description: Auditer et tester visuellement l'application WPF MKR pour garantir une finition premium, sans chevauchement, dysfonctionnement ou incohérence entre écrans.
---

# Quality Gate visuelle — MKR

## Quand utiliser
Avant d'annoncer qu'une fenêtre ou un parcours WPF est terminé, après refonte du design system, ou lors d'une validation de release.

## Matrice d'essais
1. Installer/exécuter l'artefact de build Windows réel; si environnement non graphique, déclarer « validation visuelle non exécutée ».
2. Vérifier l'ouverture, la fermeture, les fenêtres modales, les états vides, la création de projet, l'import, l'analyse, la simulation et le retour à l'étude sans perte de contexte.
3. Tester DPI 100 %, 150 %, 200 %; fenêtre minimum, standard, maximisée; écran 1366×768 et 1920×1080 lorsqu'accessibles; défilement, redimensionnement et panneaux.
4. Vérifier alignement, rythme d'espacement, cohérence typographique, lisibilité des tableaux, unités, couleurs sémantiques, contrastes, libellés, tooltips et navigation clavier.
5. Contrôler états loading/disable/error, données invalides, opérations lentes, cancellation, périphérique absent et absence du moteur Python.
6. Examiner les graphiques : titres/axes/unités/légendes, rendu de séries invalides, zoom, curseur, anomalies et export.
7. Tester les parcours en données réalistes anonymisées, et NON uniquement des valeurs de démonstration optimales.
8. Relever anomalies avec écran, étapes, comportement attendu/observé, gravité et capture si une capture réelle existe.
9. Après correction, refaire le test concerné et une non-régression ciblée sur les fenêtres partagées.

## Blocages qualité
- Texte chevauché ou masqué, bouton visible non fonctionnel, action imprévisible, erreur invisible, graphique sans unité, confusion mesuré/calculé.
- Interface gelée lors d'une simulation ou d'un import.
- Supposition « test visuel passé » à partir d'une compilation CI sans bureau graphique.

## Livrable
Tableau de vérification par écran et résolution, anomalies classées, captures réelles si disponibles, commandes/tests exécutés et décision de validation limitée au périmètre effectivement contrôlé.
