# SIM-05B — Sensibilité combinée FOPDT, grille bornée et enveloppes PV

## Objet
Compléter SIM-05A en étudiant les **interactions simultanées** entre les trois paramètres d'un modèle nominal FOPDT : gain `K`, constante de temps `τ` et retard `θ`. L'approche est un balayage déterministe, et non une étude de confiance statistique ou une preuve de stabilité.

## Contrat scientifique
Pour une variation positive `v` en pourcentage, chaque paramètre utilise trois niveaux `(1-v/100), 1, (1+v/100)`. Les 3³ = **27 combinaisons** couvrent la grille complète ; le cas nominal est affiché en premier, suivi d'un ordre reproductible. Le gain signé conserve son signe (v ≤ 50 %) et un retard nominal nul reste nul. Pas, durée, SP, limites MV, perturbations, limitation de vitesse et surtout **coefficients Kp et Ti IMC/SIMC** restent fixes.

Chaque combinaison appelle le même `pi_simulation.calculate` que la simulation existante. Ses indicateurs sont IAE, ISE, erreur finale, durée de saturation, durée à vitesse limitée, pas d'intégration gelée, dépassement et temps de réponse lorsqu'ils sont définis.

## Enveloppe de PV
Aux temps échantillonnés communs à tous les essais, l'outil retourne, séparément pour IMC/Lambda et SIMC, les minima et maxima PV observés **parmi les 27 simulations calculées**.

Ces deux bornes sont des *extrema ponctuels simulés* ; elles ne représentent **ni une courbe d'un même procédé**, ni un intervalle de confiance à 95 %, ni une zone de fonctionnement garanti. Les points qui réalisent les minima/maxima peuvent provenir de combinaisons différentes à des instants distincts.

Dans la vue simulation, les enveloppes sont représentées en zones translucides derrière les réponses nominales. Les échelles PV incluent également leurs extrêmes. Le tableau des 27 cas présente les paramètres et indicateurs enregistrés. L'export CSV nominal existant reste inchangé.

## Persistance
Le contrat existant `PiRobustnessResult` est prolongé par le champ optionnel `Envelope`, absent des anciennes analyses SIM-05A. Aucune migration SQLite. L'interface retrouve les 27 cas et les bandes après réouverture du projet. Les résultats détaillés de chaque variante sont réduits à leurs indicateurs et aux enveloppes globales : la série PV individuelle de chaque variante n'est **pas** conservée.

## Limites, incertitude et sûreté
- La grille ne couvre pas les paramètres entre les niveaux imposés ni les combinaisons en dehors de ±v %.
- Les 27 essais ne prouvent pas une stabilité globale, une marge de phase, une marge de gain, une garantie de comportement industriel ou la robustesse face à une erreur structurelle du modèle.
- Les variations sont hypothèses choisies par l'utilisateur, pas les incertitudes déduites de l'identification.
- L'absence de divergence sur un horizon fini n'est pas une démonstration de stabilité.
- Pour θ = 0 les variantes de retard se confondent : il peut y avoir moins de 27 modèles physiquement distincts malgré les 27 essais calculés.
- Le modèle hydraulique réel, l'actionneur physique et la mise en œuvre du régulateur constructeur restent à qualifier.
- Aucune écriture automate et aucun raccordement PLC.

## Contrôles d'acceptation
- Tests Python : cardinalité, nominal, K/τ/θ combinés, gain inverse, retard nul, garde-fous, cohérence min/max avec les 27 sorties directes du simulateur, IPC.
- CI Windows : compilation WPF, tests Python, smoke .NET–Python et SQLite.
- Validation visuelle réelle : lecture 27 cas, bornes PV et courbes nominales, zoom/redimensionnement, 100/150/200 % DPI, alertes de sens des enveloppes.
- Relecture persistée d'une étude nouvelle et d'une étude SIM-05A ancienne (sans champ Envelope).
- Pas de qualification industrielle déduite de la compilation.

## Suite
Étudier les trajectoires individuelles et les scénarios réalistes d'incertitude paramétrique, le coût d'exécution sur de longues simulations et la qualification scientifique sur données de procédés représentatifs.
