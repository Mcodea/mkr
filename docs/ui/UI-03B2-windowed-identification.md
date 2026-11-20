# UI-03B2 — Fenêtres originales utilisées dans l'identification

## Inspiration concurrentielle (sources publiques, consultées le 20/11/2026)

- **Control Station LOOP-PRO Tuner** : sélection de la portion de données dans la phase d'ajustement, avec une approche visuelle des tendances. Documentation : https://controlstation.atlassian.net/wiki/spaces/LT/pages/129499183
- **MathWorks System Identification Toolbox** : sous-ensembles sélectionnables, jeux d'estimation et de validation distincts, validation de modèle sans réinjecter les données de validation dans l'ajustement. Documentation : https://www.mathworks.com/help/ident/select-data-for-estimation.html et https://www.mathworks.com/help/ident/ug/specifying-estimation-and-validation-data.html
- **ControlSoft INTUNE** : utilisation de données historiques pour analyses de réglage et suivi de leur qualité. Documentation : https://www.controlsoftinc.com/software-solutions/pid-loop-tuning-tools/

**Adoption MKR :** conserver seulement les principes de sélection traçable, de distinction des expériences et de revue des historiques. Ne pas copier l'interface, les algorithmes propriétaires ou les réglages constructeur.

## Portée livrée

1. L'utilisateur prévisualise séparément les séries MV et PV des CSV originaux pour l'essai d'estimation et celui de validation.
2. Il trace une plage et confirme son usage. L'interface conserve l'identifiant de l'import, les colonnes et deux indices de lignes originales.
3. Les indices sont **inclusifs** et **en base zéro** ; l'en-tête CSV n'est pas une ligne de données.
4. Lors du prochain calcul, le moteur Python lit le CSV original, extrait le sous-ensemble demandé, puis applique toutes les gardes existantes de l'identification (échelon, plateaux MV, PV initiale, minimum 4 points avant et 12 après, validité des dates et données).
5. Une plage non choisie conserve le comportement ancien : tout le CSV.
6. Deux plages indépendantes existent : une pour l'estimation, une pour la validation. La validation n'entre pas dans l'ajustement des paramètres.
7. La nouvelle étude enregistre les deux `StepWindow` optionnelles dans son JSON SQLite, avec les identifiants des imports et les colonnes. Les études existantes avec champs absents restent lisibles avec des plages nulles.
8. Un changement d'import réinitialise la plage. Un changement de mapping après sélection bloque le calcul plutôt que d'appliquer silencieusement une sélection devenue incohérente.
9. Aucun original n'est modifié ; aucun accès réseau ou PLC et aucune écriture automate.

## Forme du contrat

Dans les objets `training` et `validation` de l'opération `identify_*` :
- état historique : `raw`, `delimiter`, `timeColumn`, `mvColumn`, `pvColumn` ;
- état avec plage : les mêmes clés, plus `rowStart` et `rowEnd` (entiers, indices inclusifs, 0 ≤ start < end < nombre de lignes).

Les deux clés doivent être présentes ensemble ; les valeurs booléennes ne sont pas des indices. Le moteur rejette `fit_window` si bornes invalides. Les indices sélectionnent des **lignes d'origine**, et non les points décimés du graphique; le graphique ne renvoie que les numéros de lignes source des points retenus.

L'ajustement utilise uniquement la plage déclarée. Le diagnostic « nombre de points » compte les données à partir de l'échelon détecté à l'intérieur de cette plage. Les deux essais restent nécessairement séparés sur la chronologie réelle, en plus des contrôles existants d'indépendance entre imports.

## Risques et restrictions

- Le sous-ensemble peut invalider les hypothèses (paliers absents, excitation insuffisante, essai trop court). Dans ce cas, **rejet plutôt qu'ajustement artificiel**.
- Le rendu graphique peut être décimé à 600 points : des événements courts peuvent être invisibles. La plage utilise néanmoins les indices de lignes originales et le moteur revalide toutes les lignes retenues. Ce mécanisme n'est pas suffisant pour garantir à lui seul la pertinence de la sélection humaine.
- **Ni filtrage, ni resampling, ni correction silencieuse.** L'algorithme scientifique demeure celui du modèle choisi, sans méthode supplémentaire.
- La plage validée est attachée au résultat enregistré, pas à un artefact de prévisualisation indépendant. Les historiques source restent conservés intégralement dans SQLite.
- Le stockage actuel ne conserve pas la liste complète des horodatages de chaque ligne sélectionnée dans l'étude : récupération reproductible à partir du CSV original, des indices, du mapping et du SHA-256. La traçabilité peut encore être renforcée avec une empreinte de sélection.
- La sélection de fenêtres ne rend pas l'identification valide pour les données en boucle fermée ou avec perturbations multiples.

## Recette exigée

### Scientifique
- `python -m unittest discover -s engine -v` : FOPDT avec deuxième échelon hors fenêtre, contrôle indépendant validation, plages limites, indices inclusifs, absence de fuite.
- Vérifier par test analytique que sélectionner une plage propre redonne K, τ et θ et que les données hors fenêtre n'affectent pas l'estimation.
- Les algorithmes SOPDT et intégrateur héritent du même lecteur de séries; ajouter une campagne sur des historiques représentatifs avant qualification.

### Intégration
- `dotnet restore MKR.slnx`, `dotnet build MKR.slnx -c Release --no-restore`, `dotnet run --project tests/Mkr.Smoke -c Release --no-build` sur Windows.
- Reouvrir une étude fenêtrée SQLite et retrouver les indices originaux. Reouvrir les études historiques sans plage.
- Vérifier qu'un changement de colonnes bloque toute application d'une plage obsolète.
- Validation manuelle à plusieurs DPI, essai avec 600 points affichés et plus de lignes originales, sortie de fenêtre, changement de sélection et retour au CSV complet.

**Niveau de preuve : code et tests enregistrés, pas encore compilé / validé graphiquement sous Windows lors de la préparation de cette PR.**
