# UI-03B1 — Inspection graphique des essais MV/PV originaux

## Périmètre effectivement implémenté
Depuis l'écran Identification, l'utilisateur sélectionne un import enregistré et les trois colonnes temps, MV, PV de l'essai d'estimation ou de validation. Un bouton ouvre une fenêtre de prévisualisation **en lecture seule**, sur une opération Python `preview_trial` du contrat IPC v1.

- Lecture depuis les **octets CSV originaux** conservés dans la base, sans remplacement par les 200 premières lignes d'aperçu.
- Vérification de l'empreinte de l'import par le stockage existant et cohérence avec le résumé demandé.
- Vérification stricte des colonnes distinctes, horodatages UTC explicites strictement croissants, MV/PV numériques finies.
- Graphiques MV et PV distincts, avec leurs propres échelles, l'origine UTC, le nombre de points et les unités saisies (ou clairement indiquées non renseignées).
- Délimitation horizontale d'un intervalle inspectable : les bornes affichées correspondent à des points sources effectivement conservés pour le tracé.
- Option d'effacer la sélection.
- Limitation de l'affichage à 600 lignes, en conservant les indices des lignes choisies; aucune interpolation ni modification du calcul scientifique. Avertissement visible lorsque l'affichage est décimé.

## Limite intentionnelle — décision de sûreté scientifique
**La période choisie n'est pas utilisée pour réentraîner le modèle.** La fenêtre sert à inspecter des historiques et à préparer une sélection scientifique future. L'algorithme actuel requiert deux essais complets indépendants avec un seul échelon, quatre échantillons minimum avant l'échelon et douze après. Modifier la fenêtre change potentiellement la détectabilité des plateaux, les amplitudes et les conditions de validation : cela doit être spécifié et testé séparément.

Les périodes choisies ne sont pas persistées à ce stade. Le stockage des imports et études reste intact.

## Protection de la validation indépendante
- Deux entrées distinctes : essai d'estimation et essai de validation.
- Chaque prévisualisation provient de l'import explicitement sélectionné.
- Pas de recalcul scientifique ni de substitution d'un import à l'autre.
- Les graphes d'une étude déjà enregistrée restent ceux du lot UI-03A.

## Recette technique
1. `python -m unittest discover -s engine -v`: contrat, données brutes, index source, décimation, séparateurs, erreurs de mapping / temps / numérique.
2. Build WPF Release et tests d'intégration .NET–Python–SQLite sur la branche de cette PR.
3. Ouvrir deux imports distincts avec mapping correct, confirmer que l'aperçu restitue l'original et l'horizon intégral.
4. Changer de colonnes et d'import : aucune réutilisation silencieuse des séries précédentes.
5. Tester sélection très courte, sélection valable, effacement, fenêtre redimensionnée.
6. Valider 100/150/200 % DPI, fenêtre minimale, clavier et fermeture pendant traitement sur Windows réel.
7. Vérifier qu'aucune étude enregistrée, aucun réglage ni aucune table SQLite n'est modifié.

## État des preuves
Code et tests enregistrés sur GitHub. Compilation Windows, exécution de la CI, inspection visuelle et qualification industrielle **non démontrées dans cette session**.

## UI-03B2 — lot ultérieur
Élaborer un nouveau contrat scientifique de fenêtres avec indices ou horodatages *sur les originaux*, documentation des transformations, contraintes avant et après échelon, prévention de fuite entre estimation et validation, enregistrement immuable des fenêtres, tests de réidentification et de reproduction.
