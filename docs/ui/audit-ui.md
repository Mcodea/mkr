# Audit UI/UX de MKR — état au 24/11/2026

Méthode : lecture du code XAML (14 fenêtres, `src/Mkr.Desktop`), exécution réelle du paquet Windows sur la CI avec captures (1024×768), tests UI Automation. **Non fait** : étude d'usage avec des utilisateurs, test sur écrans 4K ou à mise à l'échelle Windows ≠ 100 %, lecteur d'écran. Les logiciels cités par le cahier des charges (MATLAB/Simulink, TIA Portal, Control Expert, LOOP-PRO TUNER, Valmet, DewesoftX, Visual Studio) **n'ont pas pu être consultés** depuis l'environnement de développement (accès réseau restreint) : aucune affirmation sur leurs interfaces n'est faite ici ; seuls des principes généraux d'ergonomie (heuristiques de Nielsen, ISO 9241-110) sont appliqués, sans revendication de conformité.

## 1. Inventaire des fenêtres

| Fenêtre | Rôle | Taille de départ | Observation |
|---|---|---|---|
| WindowView (espace projets) | Liste et création de projets, volet latéral « parcours » purement décoratif | 1120×740 | Le volet n'est pas une navigation : ses lignes 01-06 ne sont pas cliquables |
| CsvWindow (projet) | Onglets Données CSV et Procédé, accès acquisition, catalogue, journal, identification | 1160×820 | Chevauchement à 768 px de haut (corrigé le 24/11) |
| AcquisitionWindow | Source automate, signaux, plan, enregistrement continu | 940×? | Densité élevée, courbes en temps réel absentes |
| IdentificationWindow | Estimation, validation, critères, modèles, réglage, diagnostic | 960×780 | ~15 champs avant le premier résultat ; verdict ajouté le 24/11 |
| StepSearchWindow, TrialPreviewWindow | Recherche d'échelons, aperçu des essais | | |
| SimulationWindow, PidPrototypeWindow, OpenLoopWindow | Simulations PI, PID, boucle ouverte | | Graphiques dessinés à la main |
| BlockCatalogueWindow, BlockTargetWindow, BlockValuesWindow | Fiches constructeur, valeurs de blocs | | |
| IntegratorWindow, ReviewWindow, AuditWindow | Réglage intégrateur, revue, journal | | Cohérents avec le style actuel |

## 2. Parcours actuel

Espace projets → (ShowDialog) Projet → (ShowDialog) Identification → (ShowDialog) Simulation… : une **pile de fenêtres modales**. Conséquences : pas de vue d'ensemble de l'étude, pas de retour libre à une étape, pas d'état global (projet actif, étude active, connexion) visible en permanence, aucun parcours guidé.

## 3. Écarts avec le cahier de refonte

| Exigence | État |
|---|---|
| Coque (barre supérieure, navigation latérale, espace central, inspecteur, barre d'état) | **Absente** : fenêtres indépendantes |
| Design system centralisé | **Créé le 24/11** (jetons, thèmes, styles) ; fenêtres encore à migrer vers les composants communs |
| Thème clair et sombre | **Créé le 24/11** ; contraste vérifié par test (WCAG AA) ; **non inspecté sur toutes les fenêtres** |
| Parcours guidé, modes assisté et expert | **Absents** |
| Graphiques scientifiques (zoom, curseurs, légendes interactives, export, multi-axes, gros historiques) | **Insuffisants** : tracés `Canvas` faits main, zoom par glissement sur un seul graphique, pas d'export, pas de curseurs |
| Messages d'erreur (problème, cause, conséquence, correction) | **Partiels** : codes traduits en une phrase, rarement avec la correction |
| DPI et écrans (1366×768 à 4K) | **Non vérifié**, sauf 1024×768 |
| Accessibilité | Noms UIA et touches d'accès présents ; contrastes maintenant testés ; navigation clavier et lecteur d'écran **non testés** |

## 4. Défauts corrigés au cours de l'audit

Chevauchement des cartes du projet à 768 px ; fenêtres plus hautes que l'écran ; gras du titre de groupe propagé au contenu ; liste déroulante affichant le texte technique de l'objet (modèle de sélection) ; journal d'audit illisible (zone de texte à défilement horizontal) ; absence d'états visibles (survol, focus clavier, désactivé) des boutons.

## 5. Évaluation des bibliothèques de graphiques (à vérifier avant adoption)

Les licences ci-dessous sont **de mémoire et doivent être relues à la source** avant tout choix : je n'ai pas pu les consulter.

| Option | Licence (à vérifier) | Remarque |
|---|---|---|
| Composant maison `Canvas` | Aucune | Pas de zoom/curseur/export ; coût de maintenance élevé pour un niveau « scientifique » |
| ScottPlot (WPF) | MIT (à vérifier) | Zoom, curseurs, export, gros jeux de données ; candidate principale |
| OxyPlot | MIT (à vérifier) | Mûr, moins actif |
| LiveCharts2 | MIT (à vérifier) | Orienté animations |
| SciChart, Syncfusion, Telerik | Commerciales | Hors budget probable ; licence à examiner pour une diffusion commerciale |

Décision proposée, **non prise** : prototyper ScottPlot dans une fenêtre isolée, mesurer 1 million de points et l'export, puis décider.

## 6. Plan par phases

0. Audit (ce document). 1. Design system et thèmes (livré, voir [design-system.md](design-system.md)). 2. Coque et accueil. 3. Parcours guidé et modes assisté/expert. 4. Composant de graphique. 5. Refonte des écrans métiers un à un. 6. Tests ergonomiques, DPI, redimensionnement, accessibilité. 7. Revue finale avec notation sur preuves.
