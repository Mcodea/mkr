# Avancement MKR

- Dernier incrément : limite facultative de vitesse de MV dans les deux boucles FOPDT et comparaison des réponses PI à la consigne avec dépassement, temps de réponse à ±5 % et fenêtre affichée.
- Parcours disponibles : projets locaux et description, CSV UTF-8, reprise de l’original et de son rapport, correspondance MV/PV pour l’identification, réglages PI théoriques et simulation PI avec export des séries et stockage SQLite v6.
- Contrôles locaux : trente-trois tests Python, dont un cas analytique et un cas où l’évaluation indépendante se dégrade sans modifier les paramètres. La [CI Windows du premier incrément FOPDT](https://github.com/Mcodea/mkr/actions/runs/44745288939) a réussi la compilation WPF, l’audit NuGet et le parcours SQLite–Python. La [CI du jalon d’identification](https://github.com/Mcodea/mkr/actions/runs/44746322763) a réussi pour les trois familles. Le résultat du jalon réglage et simulation est consigné dans la [PR](https://github.com/Mcodea/mkr/pull/1).
- Restent à vérifier : comportement graphique sur Windows, sensibilité aux données bruitées, domaine d’emploi sur données représentatives. Les erreurs-types locales conditionnelles n’ont pas de couverture statistique garantie.
- Simulation en boucle ouverte disponible depuis une étude FOPDT. Le scénario applique un échelon de MV borné, affiche la réponse PV et exporte les séries en CSV. La simulation est calculée à la demande et l’export conserve les séries.
- Prochaines tâches du jalon simulation : modes de commande et transitions, scénarios comparatifs supplémentaires et revue graphique Windows.
- Blocage matériel : l’environnement local est Linux. L’examen graphique exige une session Windows. La CI utilise un agent Windows pour compiler et exécuter les tests automatisés.

## UI-01 — Espace projets, branche de refonte ciblée

- Création d'un design system WPF minimal dans `App.xaml` : tokens sémantiques et styles communs.
- Refonte de `WindowView.xaml` avec navigation informative, création de projet, liste des projets et commandes existantes inchangées.
- Consignes et essais : `docs/ui/UI-01-project-workspace.md`.
- Sans modification du moteur scientifique ni des données enregistrées; compilation CI Windows et revue visuelle manuelle **à confirmer**.
- Prochain lot UX : réutilisation des styles et navigation dans les écrans CSV / identification / simulation, après validation de UI-01.

## UI-02 — Espace CSV et qualité (branche dédiée)

- Refonte ciblée de `CsvWindow.xaml` : zones source/format, mapping, imports enregistrés, tableau d'aperçu, qualité et provenance, description du procédé et accès à l'identification.
- Tous les contrôles existants, leurs noms et handlers sont préservés. Aucune modification du moteur Python, de la base SQLite ni du stockage des originaux.
- Guide de revue et tests : `docs/ui/UI-02-csv-workspace.md`.
- **À vérifier** : compilation Windows, qualité graphique multi-DPI et non-régression des imports et études.
- Prochain lot UX après preuves : visualisation des signaux et sélection de plages à partir de données sources traçables.

## UI-03A — Diagnostic graphique d'identification (branche dédiée)

- Nouvelle vue WPF de diagnostic avec choix estimation / validation indépendante, PV mesurée / prédite, résidu signé et axes unités.
- Zoom horizontal et remise à zéro sur les points déjà enregistrés; changement d'étude ou d'essai remet à zéro le zoom.
- Implémentation dans `IdentificationDiagnosticsPlot.cs` et `IdentificationWindow.xaml(.cs)`; aucune modification des algorithmes Python, modèles, schémas SQLite et fichiers sources.
- Limite : **zoom visuel seulement**. Les données du graphe proviennent des points de diagnostic déjà échantillonnés pour l'affichage, pas d'une sélection d'entraînement recalculée.
- Recette : `docs/ui/UI-03A-identification-diagnostics.md`. Compilation CI Windows et validation visuelle **à confirmer**.
- Suite : UI-03B, sélection de fenêtres sur courbes MV/PV brutes et traçabilité scientifique sans contaminer la validation indépendante.
