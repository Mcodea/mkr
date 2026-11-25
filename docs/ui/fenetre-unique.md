# Fenêtre unique : architecture des vues d'étape

Décision du mainteneur du 25/11/2026 : une **vraie fenêtre unique** (« Une vraie fenêtre unique »). Statut : phases A et B implémentées, phase C en cours. Rien n'est « validé visuellement » tant qu'une capture Windows réelle n'a pas été relue.

## Principe
- Chaque écran d'étape est une **vue** (`UserControl`, suffixe `View`) qui porte tout le contenu et la logique. Elle implémente `IStepView` (`Cancel()` annule ses calculs en cours quand elle disparaît).
- La fenêtre de dialogue homonyme (`…Window`) n'est plus qu'un **hôte mince**, écrit en code : même titre, même taille, même contenu. Les identifiants d'automatisation (`x:Name`) et les titres de fenêtre étant conservés, les tests UI Automation existants ne changent pas.
- L'espace d'étude (`WindowView`) héberge les mêmes vues **dans la fenêtre** (zone `StepHost` qui recouvre le graphique) selon l'onglet.

## Correspondance onglet → vue (état actuel)
| Onglet | Contenu dans la fenêtre |
|---|---|
| Projet | graphique du modèle et panneau de valeurs ; description, journal d'audit : dialogues |
| Données | import / acquisition : dialogues (phase C) |
| Signaux, Identification, Validation, Réglage | `IdentificationView` (une seule instance partagée par ces quatre onglets : les saisies survivent au changement d'onglet) ; une étude ou un réglage enregistré rafraîchit le tableau et le parcours sans recréer la vue |
| Stratégie | `GuidedTuningView` (étude acceptée affichée ; sinon la raison) |
| Bloc | catalogue et valeurs de bloc : dialogues (phase C) |
| Simulation | `SimulationView` du dernier réglage PI enregistré (sinon la raison) ; boucle ouverte : dialogue (phase C) |
| Rapport | fiche PDF depuis la simulation (phase C) |

## Règles
- Une vue ne ferme jamais « sa » fenêtre : elle lève `CloseRequested` si elle a un bouton Fermer, que l'hôte dialogue traite. Dans la fenêtre unique le bouton est masqué (`ShowClose = false`).
- Les dialogues restants sont ouverts avec `Owner = Window.GetWindow(this)`, que la vue soit dans un dialogue ou dans l'espace d'étude.
- Un onglet fermé (condition d'entrée non satisfaite) affiche la **raison** du parcours guidé à la place de la vue, jamais un écran vide.
- Compilation locale possible sous Linux : `dotnet build src/Mkr.Desktop -p:EnableWindowsTargeting=true` (vérifie le XAML et le code, **pas** l'affichage).

## Reste à faire (phase C)
Données (import, acquisition), signaux (aperçu d'essai, recherche d'essais), blocs (catalogue, valeurs), rapport et revue, laboratoire PID, boucle ouverte, réglage intégrateur ; mode assisté / expert ; revue visuelle sur Windows réel de chaque écran.
