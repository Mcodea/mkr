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

## UI-03B1 — Prévisualisation des essais MV/PV originaux (branche dédiée)

- Nouvelle opération Python `preview_trial`, lecture en mémoire du CSV source conservé, horodatages UTC et signaux strictement validés, aperçu limité à 600 points d'affichage indexés.
- Raccordement .NET via `TrialPreview`, port `IIdentificationEngine`, `PythonDiagnostics` et `IdentificationWorkspace` ; aucun changement du schéma SQLite.
- Fenêtre WPF en lecture seule permettant inspection de MV/PV séparément, contrôle d'une période et effacement de la sélection, sans changement des modèles calculés.
- Tests Python de contrat, origine UTC, indices source, décimation, invalidité temporelle/numérique.
- Documentation : `docs/ui/UI-03B1-trial-preview.md`. Compilation Windows/CI et revue visuelle **à confirmer**.
- **La sélection de période n'est pas appliquée à l'estimation** ; prochaine phase UI-03B2 nécessite une conception scientifique et de traçabilité spécifique.

## UI-03B2 — identification sur plages sources immuables

- Recherche comparative documentée : LOOP-PRO Tuner et MathWorks System Identification (sélection visuelle, données d'estimation/validation séparées), voir `docs/ui/UI-03B2-windowed-identification.md`.
- Envoi facultatif de deux plages `rowStart` / `rowEnd` (bornes inclusives, ligne 0 après l'en-tête), distinctes pour estimation et validation; validation Python des bornes, puis application des gardes scientifiques existants.
- Interface permettant de confirmer une période depuis la prévisualisation MV/PV d'origine; liaison de la sélection à l'import et au mapping; absence de plage = comportement historique intégral.
- Enregistrement des plages optionnelles dans `IdentificationStudy` sérialisée en SQLite, sans migration de schéma ni remplacement des originaux.
- Tests scientifiques Python et test smoke C# ajoutés pour la traçabilité et la persistance. CI Windows et revue visuelle **à exécuter**.
- Limites : rendu décimé pouvant masquer les événements courts; ce lot n'ajoute ni prétraitement automatique, ni classification de boucle fermée, ni preuve de qualification industrielle.

## UI-03C — Affichage scientifique des grands historiques

- `trial_preview.py` passe à l'algorithme d'aperçu 1.1.0 : réduction à 600 points maximum en conservant les extrema et principales transitions dans chaque portion chronologique (premier/dernier, min/max MV et PV, voisinage des plus fortes variations).
- Les points affichés conservent leurs indices originaux. L'identification sur une plage UI-03B2 exploite toujours **toutes** les lignes du CSV original incluses dans les indices retenus.
- Avertissement graphique révisé : une réduction visuelle ne constitue pas une garantie de couverture de tous les événements.
- Régression automatisée : pics courts, échelon, source entière, limites d'indices et test IPC sur CSV anomalique.
- Spécification et recette : `docs/ui/UI-03C-extrema-preview.md`. Tests CI Windows/inspection graphique réels à contrôler.
- Prochain lot : UI-04, simulation PI et graphiques comparatifs professionnels.

## UI-04A — Comparaison de simulations PI (branche dédiée)

- Refonte de `SimulationWindow.xaml` en sections scénario, indicateurs et analyse scientifique, basée sur les styles WPF de MKR.
- Nouveau `PiComparisonPlot.cs` : SP/PV comparées, commandes CV/MV distinctes par méthode (IMC/Lambda et SIMC), bornes de MV et instant de perturbation, avec unités.
- Les graphiques reposent directement sur les séries de simulation persistées; aucun recalcul de modèle ni changement des paramètres scientifiques.
- Rapport affichant les fenêtres de réponse **pour les deux** méthodes, et export CSV existant conservé.
- Documentation et recette : `docs/ui/UI-04A-pi-comparison.md`.
- CI Windows et inspection visuelle sur Windows réel : **à vérifier**.

## UI-04B — Inspection des simulations PI (branche dédiée)

- Clic pour lire un échantillon réellement enregistré : temps, SP, PV IMC/SIMC et CV/MV pour chaque méthode.
- Zoom horizontal commun aux trois pistes et retour à l'horizon complet, sans transformation scientifique ni modification de l'export.
- Courbes et échelles reconstituées uniquement à partir des données persistées dans l'intervalle visible; les métriques globales restent calculées sur le scénario complet.
- Guide d'essais : `docs/ui/UI-04B-plot-inspection.md`.
- Validation automatique Windows à confirmer sur le SHA final; validation visuelle réelle non effectuée.

## Estimation d'avancement du plan (20/11/2026)

**Indice de développement pondéré : environ 41 % du périmètre cible**, calculé par pondération explicite des jalons, avec jugement d'ingénierie (pas un pourcentage issu d'un outil de suivi, ni un résultat de qualification) :

| Jalon | Poids | Réalisation estimée | Contribution |
|---|---:|---:|---:|
| Socle Windows, IPC, stockage | 12 % | 80 % | 9,6 |
| Données hors ligne / traçabilité | 14 % | 55 % | 7,7 |
| Identification et diagnostics | 18 % | 60 % | 10,8 |
| Réglages P/PI/PID | 16 % | 25 % | 4,0 |
| Simulations et comparaisons | 18 % | 45 % | 8,1 |
| Catalogue blocs constructeur | 8 % | 0 % | 0 |
| Acquisition PLC lecture seule | 7 % | 0 % | 0 |
| Livraison, UX Windows et qualification | 7 % | 10 % | 0,7 |
| **Total** | **100 %** | | **40,9 %** |

Hypothèses : les contributions prennent en compte le code rédigé dans les branches/PR MKR non fusionnées, dont le socle et les lots d'interface. Les pourcentages arrondis sont révisables, car il n'existe pas de registre des exigences clos et approuvés. Une CI réussie atteste la compilation et les tests automatisés exécutés, pas la qualification scientifique sur données industrielles, la conformité du catalogue automate, le test graphique Windows ni la facilité de livraison à un utilisateur final. L'avancement de la branche `main` peut donc être inférieur à cet indice.

## SIM-05A — Sensibilité FOPDT des réglages PI

- Opération scientifique Python `simulate_pi_robustness`, utilisant sept scénarios (nominal, gain ±v, constante τ ±v, retard θ ±v) pour un pourcentage défini par l'utilisateur (0 < v ≤ 50, 20 % par défaut).
- Coefficients PI IMC/Lambda et SIMC **fixes** ; chaque scénario utilise le moteur PI et ses métriques actuels, sans nouvelle loi de commande.
- Contrat versionné `PiRobustnessResult`, contrôles .NET, étude nominale + matrice de sensibilité optionnelle enregistrée dans les métadonnées SQLite v6 sans migration.
- Interface WPF : bouton de calcul, saisie du pourcentage, tableau comparatif des sept cas, pire IAE dans cet ensemble fini.
- Tests Python (cas nominal, gain inverse, variations indépendantes, domaine et IPC) et smoke test .NET–Python–SQLite de relecture.
- Document scientifique : `docs/science/SIM-05A-pi-sensitivity.md`.
- **Qualification non acquise** : aucune marge de stabilité ni intervalle de confiance ne peut être déduit de cette analyse; les effets combinés et essais industriels restent à couvrir.
- L'indice global de réalisation estimée passerait d'environ 41 % à **43 %** si les tests CI Windows de ce lot réussissent. Estimation pondérée non contractuelle ; interfaces visuelles et validations terrain toujours en attente.

## SIM-05B — grille combinée K × τ × θ

- `engine/pi_combined_robustness.py` calcule 27 variantes déterministes du FOPDT nominal à niveaux −v/0/+v % pour K, τ et θ, **coefficients PI fixes**.
- Retourne, pour les deux réglages IMC/SIMC, les métriques des 27 cas et une enveloppe PV min/max point par point calculée sur tous les scénarios au même horizon.
- Exposition IPC `simulate_pi_combined`, validation .NET typée, persistance optionnelle dans l'étude existante sans migration SQLite.
- Interface : action 27 combinaisons, tableau comparatif et zones d'enveloppe translucides derrière les réponses nominales, avec avertissement explicite « pas de bande de confiance ».
- Tests Python et smoke .NET/SQLite ajoutés; document `docs/science/SIM-05B-combined-sensitivity.md`.
- Les résultats ne constituent ni marge de stabilité ni garantie de robustesse ; simulation WPF réelle et qualification industrielle restent à faire.
- Estimation globale du périmètre cible : **environ 45 %**, indicative, si la CI complète réussit; les PR restent non fusionnées.

## PID-06A — Prototype scientifique filtré (branche dédiée)

- Nouveau moteur `engine/pid_simulation.py` en forme PID idéale/dépendante, P/I sur l'erreur et dérivée filtrée sur la seule PV (Euler implicite), avec anti-windup par intégration conditionnelle, saturation, rampe MV, retard fractionnaire, perturbation.
- Contrat IPC `simulate_pid_experimental` et port .NET `IPidPrototypeEngine`, avec points PV/CV/MV, dérivée, métriques et avertissements.
- Pas d'interface WPF de réglage PID dans ce lot et **aucune méthode de tuning automatique**. Aucun coefficient fabricant ni écriture PLC.
- Vérifications : comparaison exacte avec le moteur PI si Td = 0, oracle récursif du filtre, limite Td → 0, gain inverse, garde-fous, IPC et smoke .NET–Python.
- Documentation et hypothèses : `docs/science/PID-06A-filtered-controller.md`.
- Développement et test scientifique automatisé en cours de qualification CI ; validation Windows graphique et qualification terrain distinctes.
- Prochain lot : PID-06B, parcours graphique d'essais manuels PID et comparaison avec PI, sans remplacement automatique d'un bloc automate.
- Estimation de réalisation du périmètre total : **environ 47 % sous réserve de tests CI réussis** ; reste une estimation d'ingénierie, pas une qualification industrielle.

## PID-06B — Laboratoire PID manuel hors ligne et qualité scientifique

- Nouvelle fenêtre WPF PID expérimentale, déclenchée depuis l'espace Simulation PI, sans sauvegarde et sans connexion automate.
- Même scénario, modèle FOPDT, limites, perturbation et pas pour le PI IMC de référence et le PID manuel filtré ; égalité des versions moteur et des horodatages vérifiée avant affichage.
- Tableau comparatif IAE/ISE/erreur finale/saturation/vitesse/gels I, courbes PV/SP, MV appliquée et contribution D avec unités physiques distinctes.
- Validation .NET renforcée indépendamment du moteur Python : contrôle de la grille temporelle, du contenu de tous les points et des bornes/vitesse MV.
- Tests scientifiques supplémentaires : raffinement temporel vers une référence fine, absence de pic dérivé sur un échelon initial SP, avertissement de filtre sous-échantillonné.
- Gouvernance de qualité : `docs/validation/PID-06B-industrial-gates.md` sépare convention documentée, tests numériques, validation Windows, qualification de banc constructeur et autorisation terrain.
- **Statut : conception et implémentation ; compilation Windows et essais CI à vérifier. Aucune qualification industrielle et aucun paramètre constructeur validé.**

## PID-06C1 — Qualification numérique du capteur et filtre D

- Simulation PID avec **bruit de capteur déterministe facultatif** sur la mesure PV, sans modifier directement le procédé ; PV physique et mesurée désormais distinctes dans le résultat.
- Contrôle PID sur PV mesurée, indicateurs IAE/ISE sur PV physique (vérité simulée) ; aucun tirage aléatoire implicite et aucun enregistrement caché du vecteur de bruit.
- Résultats moteur version 0.2.0 ; port .NET accepte facultativement des échantillons de bruit explicites, sans modifier le laboratoire WPF non bruité.
- Tests d'oracles indépendants : récurrence D échantillonnée, choc de bruit, filtrage plus lent, erreurs de cardinalité/type/NaN/Inf, zéro bruit identique au nominal, et smoke Python–.NET.
- `docs/validation/PID-06C1-noise-qualification.md` documente les limites, hypothèses et conditions avant usage industriel.
- **Qualification industrielle non acquise** ; aucune donnée terrain ni bloc constructeur testé. CI Windows à vérifier sur le SHA final.

## ID-07A — Identification Broïda des procédés FOPDT

- Nouveau modèle d'identification `BroidaFOPDT` explicitement distinct de l'optimiseur FOPDT existant.
- Moteur scientifique `engine/broida.py` : passages interpolés 28 %/40 % relatifs au premier échantillon MV modifié, `τ=5.5(t40-t28)`, `θ=2.8t28-1.8t40`, `K=ΔPVfinal/ΔMV`.
- Réutilisation stricte du lecteur des deux essais indépendants, des indices d'origines, de la sélection de fenêtres et des diagnostics de validation.
- Plateau final mesuré obligatoire sur six échantillons, réponse monotone, horizon ≥4τ après θ, rejet des domaines impossibles et de retard négatif ; aucune interpolation cachée du signal.
- Passage des preuves t28/t40/PVfinal dans le contrat .NET et la persistance SQLite ; lecture explicite à l'écran d'identification.
- Absence de fausses barres d'incertitude et **pas de génération automatique de réglages de régulateur sur cette nouvelle méthode**.
- Tests scientifiques Python et smoke .NET–Python–SQLite ; documentation `docs/science/ID-07A-broida.md`. CI et inspection graphique Windows à vérifier avant acceptation.
- **TUNE-08, Ziegler–Nichols**, reste à développer séparément après qualification du protocole gain ultime et des équations de réglage.
