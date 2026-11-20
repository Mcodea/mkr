# UI-01 — Espace projets et design system WPF

## Objectif
Mettre en place un premier écran de référence MKR propre et cohérent sans remplacer la logique métier existante ni introduire de dépendance UI externe.

## Périmètre implémenté
- Tokens sémantiques partagés dans `App.xaml` : surfaces, fond, texte, accents, bordures, couleurs de navigation.
- Styles communs `Button`, `TextBox`, `DataGrid`, `DataGridColumnHeader` et bouton primaire.
- Fenêtre de projets réorganisée : identité MKR, rappel du parcours technique, zone de création, tableau des projets et barre d'état.
- Les commandes existantes restent reliées aux implémentations déjà présentes : `CreateProject`, `OpenProject`, `CheckEngine`.
- Aucun écran de démonstration et aucune valeur scientifique fictive.

## Contraintes et principes
- Design Windows desktop, densité technique, contraste et unités métier lorsque pertinentes.
- Navigation latérale informative : les étapes du parcours ne sont **pas** des boutons fictifs.
- La création et l'ouverture gardent leur fonctionnement historique, ainsi que la journalisation des erreurs et la persistance SQLite.
- Pas de changement dans les ViewModels, le moteur scientifique, le stockage ni la logique d'identification.

## Critères de revue
1. Restaurer puis compiler `MKR.slnx` en configuration Release sur Windows.
2. Lancer l'application et vérifier création, sélection, ouverture et réouverture d'un projet.
3. Vérifier l'action de diagnostic du moteur Python, les messages d'erreur et l'accessibilité clavier (dont raccourcis d'accès des boutons).
4. Vérifier l'affichage à 100 %, 150 %, 200 % DPI, fenêtres à la taille minimale et maximisées.
5. Contrôler absence de chevauchement, lisibilité des libellés, tableaux longs et messages d'état.
6. Retester le parcours CSV / identification / simulation en aval : régression fonctionnelle interdite.

## Niveau de preuve
- Modifications GitHub enregistrées : **oui**.
- Compilation CI Windows : **à vérifier sur le dernier SHA**.
- Test graphique manuel Windows : **non exécuté depuis cette conversation**.
- Validation industrielle : **sans objet à ce stade**.

## Suite proposée
UI-02 : navigation cohérente à l'intérieur d'un projet et design system appliqué à l'import CSV, en conservant les parcours fonctionnels et les tests.
