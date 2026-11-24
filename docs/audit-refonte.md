# Audit et plan de refonte — relevé du 24/11/2026

Cet audit complète `docs/validation/etat-de-maturite.md` (maturité scientifique) et `docs/ui/audit-ui.md` (interface). Chaque ligne cite une preuve vérifiable dans le dépôt ou dit « absent ». Aucune fonction n'est comptée comme terminée sans test exécuté en CI.

## 1. Constats d'architecture et de code
- Monolithe modulaire : `Mkr.Core` (domaine), `Mkr.Infrastructure` (SQLite, Python, Modbus/S7), `Mkr.Desktop` (WPF), moteur Python séparé avec contrat IPC versionné. Séparation propre, tests de contrat.
- **Défaut** : l'interface est une pile de fenêtres modales (projet → CSV → identification → simulation…). Pas de navigation libre, pas d'arbre projet/études, pas de panneau de propriétés. La coque ajoutée n'intègre pas encore les étapes.
- **Défaut** : `IdentificationWindow.xaml.cs` (≈800 lignes) mélange logique de présentation et orchestration ; peu de MVVM hors fenêtre principale.
- **Défaut** : les graphiques sont dessinés à la main sur Canvas (un composant par écran), sans pistes synchronisées ni composant commun.
- Fonctions simulées signalées : les acquisitions sont validées contre des serveurs Modbus/S7 de référence, jamais contre un automate réel ; l'interface le dit (« lecture seule », « non qualifiée »).
- Aucun bouton factice repéré par revue des gestionnaires de clic ; les contrôles d'étapes non disponibles sont désactivés.
- Données codées en dur : catalogue de blocs dans `engine/catalogue/*.json` (sources officielles citées) ; seuils scientifiques nommés dans le code (`NOISE_MULTIPLE`, `EXCITATION_NOISE_RATIO`, `MAXIMUM_TIME_RATIO`) avec justification dans `docs/science/` ; **à reprendre** : certaines constantes n'ont pas de justification bibliographique complète (voir §3).

## 2. Matrice des exigences de la mission
Légende : ✔ satisfait (preuve en CI) · ◐ partiel · ✘ absent.

### Parcours principal
| # | Étape | État | Preuve / manque |
|---|---|---|---|
| 1 | Configuration du procédé | ◐ | Fiche de boucle (repère, grandeur, point de fonctionnement, objectif) enregistrée ; pas de modèle de procédé structuré |
| 2 | Acquisition | ✔ | Modbus 03/04, S7 lecture, CSV ; qualité tracée ; tests de trames hostiles |
| 3 | Analyse et qualification des signaux | ✔ | `csv_quality.py`, tests ; excitation, saturation, trous |
| 4 | Identification | ◐ | FOPDT, SOPDT, intégrateur, Broïda ; boucle fermée **expérimentale** ; pas d'ARX |
| 5 | Validation et sélection du modèle | ✔ | Porte de validation obligatoire, comparaison de modèles |
| 6 | Sélection de la stratégie | ◐ | `strategy_selection.py` testé ; non exposé comme étape de navigation |
| 7 | Sélection du bloc constructeur | ✔ | Catalogue Schneider/Siemens, conversions « non qualifiées » |
| 8 | Calcul des paramètres | ✔ | IMC/SIMC (PI, PID, intégrateur), documenté `docs/science/` |
| 9 | Simulation et robustesse | ◐ | Sensibilité PI, marges de stabilité ; pas de Monte Carlo de robustesse |
| 10 | Validation et rapport | ◐ | Fiche PDF avec IAE ; manquent ISE/ITAE dans le PDF, références, mesuré vs simulé, effort actionneur détaillé |

### Moteur scientifique
| Exigence | État | Note |
|---|---|---|
| Identification boucle ouverte | ✔ | |
| Identification boucle fermée | ◐ | Expérimentale, dispersion sous-estimée d'environ 30 % publiée |
| Sans régime permanent initial | ✘ | À spécifier (état initial estimé) |
| FOPDT / SOPDT / intégrateur | ✔ | |
| ARX | ✘ | Absent |
| Incertitudes | ◐ | Erreurs-types FOPDT conditionnelles ; retard publié |
| Analyse des résidus | ✔ | |
| Sélection de modèles | ✔ | |
| IMC, Lambda, SIMC | ✔ | IMC/Lambda et SIMC |
| Optimisation numérique du réglage | ✘ | Pas d'optimiseur sur critère |
| Simulation dynamique, saturation | ✔ | |
| Anti-windup | ◐ | Modèle MKR propre ; non qualifié face aux blocs constructeurs |
| Robustesse (marges) | ✔ | `stability_margins.py` |

### Fonctions industrielles
| Exigence | État |
|---|---|
| Acquisition en ligne lecture seule, historisation | ✔ (simulée seulement, voir maturité) |
| Import CSV | ✔ |
| Import Excel | ✘ |
| Diagnostic de boucles existantes (dérive) | ✔ |
| Comparaison réglages existants / proposés | ◐ (comparaison IMC/SIMC ; pas de tableau Actuel / Proposé) |
| Simulation du bloc constructeur | ◐ (PIDFF, PI manuel/auto ; autres blocs non simulés) |
| Effort de commande, actionneurs | ◐ |
| Gains énergétiques | ✘ (volontairement : données absentes) |
| Rapport professionnel | ◐ |

### Interface
| Exigence | État |
|---|---|
| Système graphique commun, thèmes clair/sombre, contraste mesuré | ✔ |
| Coque : barre supérieure, menu repliable, barre d'état, aide | ✔ (captures CI) |
| Arborescence projets/études | ✘ |
| Panneau de propriétés contextuel | ✘ |
| Graphiques synchronisés, zoom, curseurs, export | ◐ (simulation : zoom, curseur, export ; pas de pistes synchronisées communes) |
| Navigation par étapes avec prérequis | ◐ (texte d'accueil seulement) |
| Aucun chevauchement à 1024×768 | ◐ (captures inspectées partiellement) |
| DPI / 4K / clavier / lecteur d'écran | ✘ non testé |

## 3. Références industrielles
Étudiées par sources publiques seulement : LOOP-PRO TUNER (synthèse fournie, guide V2.8, pages produit — `docs/ui/reference-parcours-industriel.md`). **Non étudiés à ce jour** : Valmet PID Loop Optimizer, MATLAB PID Tuner, System Identification Toolbox, TIA Portal, Control Expert (hors documents de blocs déjà lus). Ce sera fait avant chaque lot concerné, en ne gardant que des principes, jamais d'interface ni de code.

## 4. Avancement calculé
Comptage sur les 40 lignes des tableaux ci-dessus : ✔ = 17, ◐ = 15, ✘ = 8 → **17/40 exigences pleinement satisfaites (42,5 %)** ; en comptant chaque ◐ pour moitié : **24,5/40 (61 %)**. Ce n'est pas un pourcentage de « qualité industrielle » : aucune exigence n'est qualifiée sur matériel réel (voir maturité).

## 5. Plan par lots (un lot = spécification → implémentation → tests CI → captures → documentation → commit)
1. **Lot A — Navigation par étapes réelle** : les 10 étapes dans le menu, avec prérequis calculés à partir de l'état du projet (importés ? modèle validé ?) et message expliquant pourquoi une étape est fermée ; panneau de propriétés contextuel (projet, étude, boucle).
2. **Lot B — Composant graphique commun** : pistes synchronisées, zoom/pan, curseurs, légendes interactives, export PNG/CSV ; adopté par les écrans existants.
3. **Lot C — Étapes intégrées** dans la fenêtre unique (fin des modales) en commençant par Acquisition puis Identification.
4. **Lot D — Rapport** : ISE/ITAE, références, mesuré vs simulé, effort actionneur.
5. **Lot E — Science** : ARX (spécification, références, tests), état initial non stationnaire, incertitudes ; optimisation sur critère.
6. **Lot F — Import Excel**, tests DPI/clavier.
