# Fenêtre unique : architecture des vues d'étape

Décision du mainteneur du 25/11/2026 : une **vraie fenêtre unique** (« Une vraie fenêtre unique »). Statut : l'essentiel du parcours est dans la fenêtre ; il reste des dialogues listés plus bas. Rien n'est « validé visuellement » tant qu'une capture Windows réelle n'a pas été relue.

## Principe
- Chaque écran d'étape est une **vue** (`UserControl`, suffixe `View`) qui porte tout le contenu et la logique. Elle implémente `IStepView` (`Cancel()` annule ses calculs en cours quand elle disparaît).
- La fenêtre de dialogue homonyme (`…Window`) n'est plus qu'un **hôte mince**, écrit en code : même titre, même taille, même contenu. Les identifiants d'automatisation (`x:Name`) et les titres de fenêtre étant conservés, les tests UI Automation existants ne changent pas.
- L'espace d'étude (`WindowView`) héberge les mêmes vues **dans la fenêtre** (zone `StepHost` qui recouvre le graphique) selon l'onglet.

## Correspondance onglet → vue (état actuel)
| Onglet | Contenu dans la fenêtre |
|---|---|
| Projet | graphique du modèle et panneau de valeurs ; description, journal d'audit : dialogues |
| Données | `CsvView` (import, qualité, description de la boucle) ; l'acquisition automate reste un dialogue modal |
| Signaux, Identification, Validation, Réglage | `IdentificationView` (une seule instance partagée par ces quatre onglets : les saisies survivent au changement d'onglet) ; une étude ou un réglage enregistré rafraîchit le tableau et le parcours sans recréer la vue |
| Stratégie | `GuidedTuningView` (étude acceptée affichée ; sinon la raison) |
| Bloc | `BlockTargetView` (valeurs documentaires non qualifiées du dernier réglage PI enregistré) ; le catalogue des blocs reste un dialogue de consultation |
| Simulation | `SimulationView` du dernier réglage PI enregistré (sinon la raison) ; le bouton du ruban « Boucle ouverte » / « Boucle fermée » bascule vers `OpenLoopView` (modèle FOPDT) |
| Rapport | fiche PDF depuis la simulation (pas encore de vue propre) |

## Règles
- Une vue ne ferme jamais « sa » fenêtre : elle lève `CloseRequested` si elle a un bouton Fermer, que l'hôte dialogue traite. Dans la fenêtre unique le bouton est masqué (`ShowClose = false`).
- Les dialogues restants sont ouverts avec `Owner = Window.GetWindow(this)`, que la vue soit dans un dialogue ou dans l'espace d'étude.
- Un onglet fermé (condition d'entrée non satisfaite) affiche la **raison** du parcours guidé à la place de la vue, jamais un écran vide.
- Compilation locale possible sous Linux : `dotnet build src/Mkr.Desktop -p:EnableWindowsTargeting=true` (vérifie le XAML et le code, **pas** l'affichage).

## Dialogues modaux conservés (décision de conception, à valider)
Sélecteurs et consultations qui rendent un résultat à une vue ou affichent une référence : aperçu des signaux, essais détectés, acquisition automate (assistant), catalogue des blocs, valeurs de bloc PIDFF, revue de l'étude, journal d'audit, aide, laboratoire PID, régulateur intégrateur. Ils s'ouvrent au-dessus de la fenêtre unique (`Owner`), comme un sélecteur de fichier.

## Mode assisté / expert
Un sélecteur de la barre supérieure. **Assisté** (par défaut) : un bandeau nomme la prochaine étape que le projet peut faire (`StudyJourney.NextStep`, dérivé des seuls faits stockés) avec un bouton « Y aller ». **Expert** : aucun guidage. Le mode n'est pas encore mémorisé d'une session à l'autre.

## Reste à faire
Vue propre pour le rapport ; laboratoire PID et régulateur intégrateur en vues ; mémorisation du mode ; revue visuelle sur Windows réel de chaque écran (seules les captures de la CI ont été relues, à 1024×768).
