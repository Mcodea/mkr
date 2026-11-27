# Protocole pré-enregistré — étude du critère de validation de modèle pour le calcul PID

**Statut : PROTOCOLE, commité AVANT toute mesure de l'étude.** La règle de décision (§7) est figée ici ; toute modification ultérieure doit être datée, justifiée et signalée comme telle dans le rapport. La porte de validation actuelle (`engine/validation_gate.py`) est **conservée telle quelle** tant que la règle d'adoption n'est pas satisfaite (demande du client du 2026-11-27).

## 1. Question
Peut-on décider **automatiquement**, sans seuil scientifique saisi par l'utilisateur, qu'un modèle identifié est assez fiable pour recommander un réglage PI/PID ? Le critère actuel (erreur quadratique ≤ seuil lié au bruit **et** autocorrélation de lag 1 des résidus de validation ≤ borne de Ljung à 99 %) est-il (a) trop strict pour la commande, (b) trop permissif, (c) complémentable par un critère mieux fondé ? Constat de départ (F-LOT9-1, `docs/science/scenario-pression-pompe-m580/parcours-moteur.md`) : sur un scénario synthétique d'ordre 4 non linéaire, le FOPDT a une erreur proche du bruit mais est refusé par la blancheur, alors que son PI fonctionne sur la boucle fermée du procédé vrai. **Un cas ne prouve rien** : d'où cette étude sur une population.

## 2. Ce que le réglage doit protéger : la « vérité » est le comportement sur le procédé vrai, pas un seuil de validation
Pour chaque cas simulé on connaît le procédé vrai. La vérité d'un cas est tirée de ce que **le réglage recommandé à partir du modèle identifié fait sur le procédé vrai**, mesuré par des outils indépendants de la porte :
- **stabilité** et **pic de sensibilité Ms vrai**, par un balayage fréquentiel écrit pour l'étude (retard vrai + demi-période de maintien), jamais par `stability_margins.py` ;
- **perte de performance** : rapport ITAE (échelon de consigne, simulation temporelle indépendante) entre le réglage recommandé et le réglage SIMC calculé avec les paramètres **vrais** (même règle, même τc).

**Étiquettes « apte à la commande »** (balayées, car aucun seuil n'est « le bon ») : apte ⇔ stable **et** Ms vrai ≤ M_lim, avec M_lim ∈ {1,7 ; **2,0** ; 2,5 ; 3,0}. **Définition primaire : M_lim = 2,0** (borne haute de la plage 1,4 – 2,0 habituelle des spécifications de robustesse PID ; à confirmer par une source lue, voir la revue de littérature). Définition secondaire : apte ⇔ stable et Ms ≤ 2,0 **et** perte ITAE ≤ 2,0 × le réglage de référence. Les conclusions n'ont de valeur que si elles tiennent sous plusieurs définitions (§7 iii).

## 3. Quatre sources d'écart à ne pas confondre (conception des populations)
| Source | Population synthétique | Effet attendu sur les résidus | Conséquence pour le PID |
|---|---|---|---|
| Bruit de mesure blanc | P1 FOPDT exact, SNR ∈ {20, 50, 200, 1000} | résidus blancs | aucune (modèle exact) |
| Perturbation colorée / dérive | P4 FOPDT exact + bruit AR(1) (φ ∈ {0,5 ; 0,9}) + dérive lente | résidus **corrélés sans erreur de structure** | aucune : un critère de blancheur refuse à tort |
| Erreur de structure bénigne | P2 SOPDT (τ2/τ1 ∈ [0,02 ; 0,5]), P3 chaînes de retards égaux (n = 3, 4) | résidus corrélés | faible à modérée (règle de la moitié) |
| Erreur de structure dangereuse | P6 réponse inverse (zéro à droite, β/τ ∈ [0,05 ; 0,5]) | résidus corrélés | instabilité possible : doit être **refusé** |
| Incertitude paramétrique | tirages de (K, τ, θ) par cas ; erreur d'estimation propre au bruit | variable | variable |
| Non-linéarité / dérive du gain | P5 Hammerstein (gain dépendant de l'amplitude, validation d'amplitude différente), P7 gain variant entre essais | corrélations résidus/entrée | variable |

Paramètres tirés par cas (graine fixe, sans lien avec les cas d'évaluation) : θ/τ log-uniforme dans [0,05 ; 2], τ log-uniforme dans [2 s ; 200 s], K de signe aléatoire, période d'échantillonnage = τ/50, durée = max(8τ + θ, 40 Ts), échelon d'amplitude 5 à 10 % de l'étendue d'entrée, validation indépendante d'amplitude opposée et de rapport 0,5 – 2. Deux jeux de graines **disjoints** : **calibration** (50 % : réglage éventuel des seuils d'un critère candidat) et **évaluation** (50 % : seule base de la décision). Taille : ≥ 200 cas par population et par niveau de SNR dans le jeu d'évaluation.

## 4. Critères comparés (décision accepter / refuser pour le réglage)
Tous reçoivent exactement les mêmes essais d'estimation et de validation, les mêmes identifications (FOPDT ; SOPDT pour E) et **aucun seuil saisi par l'utilisateur**.
- **A — critère actuel** : `suggest_criteria` + `assess_model` (RMSE ≤ max(3σ, tolérance/2) ET lag-1 ≤ 2,576/√N sur la validation).
- **A1** : erreur quadratique seule (même seuil). **A2** : blancheur lag-1 seule.
- **B — portemanteau de Ljung-Box** sur les résidus de validation, m = min(20, ⌊N/4⌋) retards, degrés de liberté corrigés du nombre de paramètres (3), niveau 99 % ; ET erreur quadratique (même seuil que A).
- **C — corrélation croisée résidus/entrée** (validité en boucle ouverte seulement), retards 0..m, bornes ±2,576/√N, ET erreur quadratique.
- **D — erreur de structure relative** : NSE = max(0, MSE_val − σ²_bruit)/Δy² où σ²_bruit vient de la **ligne de base** de l'essai (erreur pure) et Δy est l'amplitude de la réponse ; décision NSE ≤ s_D. Le seuil s_D **n'est pas choisi à la main** : il est calibré sur le jeu de calibration pour que P(accepter | inapte) ≤ α_risque (§6), puis figé et évalué sur le jeu d'évaluation.
- **E — robustesse du régulateur sur un ensemble de modèles compatibles avec les données** (identification pour la commande) : ensemble = région de confiance 95 % des paramètres du FOPDT (rapport de vraisemblance sur la validation) ∪ SOPDT ajusté ; critère : le PI calculé sur le FOPDT a Ms ≤ M_E sur **tous** les modèles de l'ensemble (balayage fréquentiel indépendant) **et** l'erreur quadratique passe. M_E = limite de conception de `recommend` (1,7) relevée d'une marge fixée par calibration comme s_D.
- Combinaisons : **A∪D**, **A∪E**, **A∩E** (règles de décision fixées d'avance, pas choisies après coup).

## 5. Données réelles indépendantes
- **TCLab CC BY 4.0** (`tests/reference/tclab_cc_by/`, DOI 10.5281/zenodo.24286966, attribution conservée) : essai SISO réel en boucle ouverte. Usage : (i) décisions de chaque critère sur des paires estimation/validation extraites du fichier ; (ii) erreur de **prédiction** sur un essai tenu à l'écart. **Limite déclarée d'avance** : aucune boucle fermée réelle, donc **aucune vérité de commande** sur ces données ; elles servent de contrôle de non-contradiction (un critère ne doit pas refuser massivement un modèle dont la prédiction sur essai indépendant est bonne, ni accepter un modèle dont la prédiction est mauvaise), pas de preuve de supériorité.
- D'autres jeux réels ne sont utilisés que si leur licence est établie (voir `docs/science/licences/jeux-de-donnees.md`).

## 6. Risque accepté (décision d'ingénierie, à valider par le client)
α_risque = **P(accepter | inapte) ≤ 5 %** sur la population d'évaluation pour la définition primaire. C'est un niveau de risque de **décision**, non un seuil scientifique : il est proposé ici, publié dans le rapport, et doit être confirmé par le responsable avant toute adoption.

## 7. Règle d'adoption (figée)
Un critère X est **adopté** (en remplacement ou en complément du critère A) **si et seulement si**, sur le jeu d'évaluation, avec correction de Bonferroni pour le nombre de candidats comparés (niveau 0,05/k) :
 i. **non-infériorité de sécurité** : P(accepter | inapte)(X) ≤ P(accepter | inapte)(A) + 0,01 **et** ≤ α_risque, pour la définition primaire ;
 ii. **gain** : P(refuser | apte)(X) < P(refuser | apte)(A), l'intervalle de confiance bootstrap apparié (B = 2000 rééchantillonnages de cas) de la différence excluant 0 ;
 iii. **robustesse à la définition** : i et ii tiennent pour la définition primaire **et** pour au moins 2 des 3 définitions de sensibilité ;
 iv. **aucune contradiction** sur les données réelles (§5) ;
 v. **reproductibilité** : le script de l'étude, ses graines et ses sorties reproduisent les mêmes tableaux (empreintes publiées), et un test automatique de taille réduite le vérifie.
**Sinon la porte actuelle est conservée** et le rapport l'indique, quel que soit l'effet sur les scénarios individuels. **Aucun résultat sur un scénario particulier ne peut à lui seul décider.**

## 8. Livrables
`tests/reference/validation_study/` (générateur, évaluation indépendante, script d'exécution, graines, tableaux de résultats + empreintes) ; rapport `docs/science/lot4/rapport-etude-validation.md` (tableaux, intervalles, analyse par population, faux positifs/négatifs, décision selon §7, limites) ; revue de littérature `docs/science/references/validation-residus-etat-de-l-art.md`. Tranches : T1 = populations P1, P2, P3, P4, P6 ; T2 = P5, P7 et le critère E complet si le temps de calcul l'exige. Ce qui n'est pas exécuté est dit non exécuté.

## 9. Menaces à la validité déclarées d'avance
Populations synthétiques (le monde réel est plus riche) ; la définition d'« apte » est un choix d'ingénierie balayé mais pas déduit d'une norme ; ITAE de consigne n'est pas la seule performance (rejet de perturbation) ; une seule famille de modèles (FOPDT/SOPDT) ; les données réelles n'offrent pas de vérité de commande ; un critère calibré sur des populations simulées peut ne pas se transférer.
