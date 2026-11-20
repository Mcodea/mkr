# UI-02 — Espace CSV, correspondance et qualité

## Objectif
Donner à l'import de données une interface d'ingénierie hiérarchisée sans modifier le moteur scientifique, les règles de mapping ni les données persistées.

## Fichiers
- `src/Mkr.Desktop/CsvWindow.xaml` : refonte de la vue et réutilisation des styles MKR ajoutés par UI-01.
- `docs/PROGRESS.md` : suivi du lot.

## Fonctionnalités préservées
1. Choisir séparateur virgule ou point-virgule, ouvrir un CSV et inspecter les en-têtes.
2. Associer horodatage, valeur, rôle SP/PV/CV/MV/d et unité explicitement.
3. Lancer analyse et enregistrement, revoir la qualité et la provenance (SHA-256).
4. Ouvrir les imports conservés et exporter l'original brut.
5. Éditer la description du procédé, accéder à l'identification.
6. Conserver l'annulation à la fermeture, les erreurs et les limites CSV existantes.

## Organisation visuelle
- En-tête de parcours MKR, zone de travail claire et indication du mode hors ligne.
- Trois étapes distinctes : source/format, mapping, historique des imports.
- Aperçu de données majoritaire et rapport qualité consultable.
- Barre d'état et action d'identification persistantes.
- Panneaux avec défilement contrôlé afin de préserver l'espace graphique.

## Hors périmètre
- Import Excel, mapping multivarié, tracés temporels interactifs, limites >1 Mio, filtre des séries.
- Aucun changement de format d'étude ni migration SQLite.
- Pas de nouveau bouton fictif : toutes les commandes réutilisent les handlers existants.

## Critères d'acceptation
- Compilation WPF Windows et réussite de la CI sur le SHA de la PR.
- Sélection manuelle des colonnes, refus de mapping invalide.
- Import effectif, lecture des données, conservation de l'original et du SHA.
- Réouverture des imports après redémarrage de l'application.
- Affichage des messages d'erreur et de qualité sans tronquage.
- Validation visuelle Windows à 100/150/200 % DPI, largeur minimale et 1920×1080, tests clavier, titres et contrastes.
- Vérification que l'accès à l'identification et l'édition du procédé fonctionnent.
- Aucun impact sur les algorithmes Python ni sur les schémas SQLite.

**Statut : code XAML enregistré; compilation, essai graphique et parcours Windows non encore prouvés.**
