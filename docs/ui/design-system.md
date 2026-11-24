# Design system MKR

Source de vérité : `src/Mkr.Desktop/Resources`. Les fenêtres ne contiennent **aucun** littéral de couleur ni de pinceau statique : un test (`tests/test_theme_contrast.py`) le vérifie.

## Fichiers

| Fichier | Contenu |
|---|---|
| `Themes/Light.xaml`, `Themes/Dark.xaml` | Jetons de couleur sémantiques (mêmes clés dans les deux thèmes) |
| `Tokens.xaml` | Typographie (Segoe UI Variable, Cascadia Mono), échelle de tailles, grille d'espacement de 4 px, rayons, styles de texte (`MkrCaption`, `MkrSubtitle`, `MkrTitle`, `MkrDisplay`) |
| `Controls.xaml` | Styles implicites : bouton (survol, appui, focus clavier épais, désactivé), bouton principal, liste déroulante plate, champ, case, étiquette, groupe, onglets, tableau, infobulle |
| `ThemeService.cs` | Bascule à chaud (Suivre Windows / Clair / Sombre), mémorisée dans `theme.txt` du dossier de données ; `MKR_THEME` impose un thème (tests, captures) |
| `PlotColors.cs` | Couleurs des tracés scientifiques lues dans le thème |

## Jetons de couleur

Surfaces : `MkrCanvas` (fond), `MkrSurface` (cartes), `MkrSurfaceAlt` (lignes alternées, survol). Texte : `MkrText`, `MkrMuted`. Accent MKR (sarcelle) : `MkrAccent`, `MkrAccentPale`, `MkrOnAccent`. Bordures : `MkrBorder` (séparateurs), `MkrBorderStrong` (limite des champs, 3:1 exigé). États : `MkrSuccess`, `MkrDanger`, `MkrWarning` (+ `…Pale`, `MkrOnStatus`). Volet : `MkrNavy`, `MkrNavySoft`, `MkrNavyText`, `MkrNavyMuted`. Tracés : `MkrPlotAxis`, `MkrPlotLabel`, `MkrPlotGrid`, `MkrSignal1…4`.

Correspondance des signaux (jamais la couleur seule : styles de ligne et légendes aussi) : Signal1 mesure/estimation, Signal2 modèle ou seconde méthode, Signal3 commande, Signal4 résidu/dérivée.

## Règles

1. Une couleur se référence par `{DynamicResource MkrXxx}` ; jamais par `StaticResource` ni par littéral.
2. Un état n'est jamais porté par la couleur seule (texte ou icône en plus).
3. Chaque paire texte/fond des deux thèmes atteint 4,5:1 ; chaque signal, état et limite de champ atteint 3:1 (test automatisé, WCAG 2.1 AA).
4. Espacements multiples de 4 px.
5. Les touches d'accès (Alt+lettre) sont conservées par les modèles de contrôles (`RecognizesAccessKey`).

## Limites actuelles

Pas encore d'icônes ni de composants de navigation, de notification et de graphique. Les 14 fenêtres existantes utilisent les styles implicites mais n'ont pas été reconstruites sur ces composants. Le thème sombre n'a pas été inspecté sur chaque fenêtre.

## Vocabulaire affiché

Les abréviations d'ingénierie (SP, PV, MV, CV) restent dans les données, les en-têtes CSV et le code, mais l'interface utilise des noms compris sans lexique (`Mkr.Core.Terms`) :

| Affiché | Abréviation | Sens |
|---|---|---|
| Consigne | SP | valeur visée |
| Mesure | PV | valeur lue sur le procédé |
| Sortie de commande | MV | commande appliquée au procédé |
| Commande calculée | CV | commande demandée avant limitation |
| Perturbation | d | entrée non commandée |

Les listes de sélection montrent le nom suivi de l'abréviation entre parenthèses (« Mesure (PV) »). Les identifiants d'automatisation (`TrainingMv`, `MvUnit`…) et les rôles stockés ne changent pas. Limite : relecture linguistique par un automaticien francophone à faire ; les messages du moteur Python et certains rapports peuvent encore contenir des abréviations.
