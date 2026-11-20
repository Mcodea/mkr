---
name: mkr-premium-design-system
description: Créer ou améliorer un design system WPF haut de gamme, cohérent, sobre et dense pour MKR : tokens visuels, layouts, composants, styles, thèmes et états interactifs.
---

# Design System Premium — MKR

## Déclenchement
À utiliser pour un redesign global, une charte graphique, une nouvelle fenêtre majeure, un thème, une navigation ou la création d'un composant WPF réutilisable.

## Référence de qualité
Viser une application desktop d'ingénierie industrielle mature, non une landing page, un tableau de bord web générique ou une imitation de logiciel concurrent. Références UX seulement : LOOP-PRO Tuner, MATLAB PID Tuner et applications CAD/SCADA professionnelles. Ne pas copier d'actifs, de maquettes ou de marques propriétaires.

## Démarche
1. Inspecter `src/Mkr.Desktop/`, styles existants, `docs/DEVELOPMENT_PLAN.md`, besoins du workflow et fenêtres déjà utilisées.
2. Définir l'architecture d'information AVANT l'esthétique : accueil projets → procédé → données/acquisition → identification → réglage → simulation → validation/rapport.
3. Définir un système de tokens dans un ou plusieurs ResourceDictionary : palette sémantique, typographie, échelle d'espacement, tailles, coins, séparateurs, états focus/disabled/error/warning/success, densité compacte et standard.
4. Utiliser styles et templates cohérents pour Button, TextBox, ComboBox, DataGrid, TreeView, TabControl, ToolBar, Dialog, StatusBar, panneaux de propriétés et cartes d'alertes. Privilégier contrôles WPF éprouvés et bindings.
5. Organisation recommandée : ruban/commandes en haut, navigation projet/études à gauche, workspace central pour graphiques, propriétés contextuelles à droite, diagnostics/console en bas, barre d'état persistante. Proposer des panneaux repliables pour les petites résolutions.
6. Concevoir des interactions rapides : raccourcis clavier, commandes contextualisées, recherche, tri/filtre, raccourcis d'actions fréquentes, undo/redo uniquement avec modèle de transaction et historique explicite.
7. Choisir un style premium industriel : sobriété, contraste adapté, surfaces clairement hiérarchisées, grilles alignées, typographie nette, pas d'ombres décoratives excessives, pas d'animations gênantes.
8. Éviter l'abus de couleur : réserver rouge/orange aux alarmes ou erreurs, ne jamais coder un état uniquement par la couleur. Supporter light/dark seulement si les deux sont vérifiés.
9. Garantir design responsive **desktop Windows** : min widths, Grid/SharedSizeGroup, GridSplitter si pertinent, scroll maîtrisé et zéro superposition à 100 %, 150 % et 200 % de DPI.
10. Un écran ne doit afficher ni faux calcul, ni faux résultat, ni action inerte. Toutes les commandes sont reliées à un scénario réel, ou explicitement désactivées.
11. Maintenir et documenter les tokens, composants et conventions; appliquer aux fenêtres existantes par petits lots sans casser le parcours.

## Gate qualité
- Revue du code XAML + ViewModel ; styles sans duplication systématique.
- États vide, chargement, erreur, déconnecté, analyse non qualifiée, validé clairement différenciés.
- Accessibilité clavier et focus, hiérarchie de lecture, contrastes, libellés et infobulles, gestion unités.
- Aucun clipping, chevauchement, texte tronqué non justifié, fenêtre bloquée pendant opération.
- CI WPF ne remplace **pas** une revue graphique réelle sur Windows.

Livrable : code réutilisable, écran de référence intégré, inventaire de composants, protocole de revue, limitations et tests.
