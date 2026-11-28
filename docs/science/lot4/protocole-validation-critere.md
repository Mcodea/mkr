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

---

## Amendement v1.1 (2026-11-27, **avant toute analyse des résultats**)

**Ce qui a été vu avant l'amendement.** Cinq cas de développement (indice 1, un par population) pour déboguer la chaîne ; aucun n'appartient aux jeux de calibration (0–99) ni d'évaluation (1000–1199). La collecte T1 est en cours ; **aucun résultat de la population n'a été analysé ni lu** au moment où cet amendement est écrit.

**Pourquoi.** La revue de littérature (`docs/science/references/validation-residus-etat-de-l-art.md`) établit, à partir de textes lus : (1) le moteur calcule des **résidus de simulation** (pas de modèle de bruit) ; pour ce cas, la documentation de la boîte à outils de Ljung recommande de juger l'**indépendance résidus/entrée** et de prêter moins d'attention à la blancheur ; (2) SIMC tolère une erreur de structure par construction (Skogestad 2003 : approximations « sûres » ; Δθ/θ = 2,14 pour τc = θ, **vérifié ici dans le texte de l'article**) ; (3) l'erreur qui compte pour la commande est celle proche de la coupure, comparée aux marges ; (4) une covariance classique sous-estime l'incertitude en présence d'erreur de structure (Schoukens & Ljung 2019). Aucun de ces points ne change la **règle d'adoption** (§7), ni la vérité (§2), ni les populations (§3).

**Précisions et ajouts (candidats, mêmes règles d'évaluation, même jeu d'évaluation, même correction de Bonferroni avec k relevé en conséquence) :**
- **E — précision** : ensemble de modèles « compatibles » = paramètres (K, τ, θ) du FOPDT dont la somme des carrés des résidus **sur les deux essais** (estimation et validation) vérifie SSE(θ) ≤ SSE(θ̂)·(1 + p/(N−p)·F_{p,N−p}(0,95)), p = 3 ; **union** avec le SOPDT ajusté par le moteur (échantillon de l'incertitude de structure). Statistique enregistrée : **Ms du pire modèle de l'ensemble** pour le PI calculé sur le FOPDT (balayage indépendant). Décision : pire Ms ≤ M_E, M_E calibré sur le jeu de calibration comme s_D (largest seuil tel que P(accepter | inapte, définition primaire) ≤ α_risque), puis figé. L'ensemble est fondé sur la somme des carrés **observée** : il intègre donc automatiquement l'erreur de structure (pas seulement le bruit).
- **C7 — ajout** : marge de gain et de retard comparée à l'incertitude identifiée. Incertitudes = erreurs types publiées par le moteur (K, τ, θ) × z (2,576) × facteur d'inflation de Birge b = max(1, √(MSE_val/σ²_bruit)) (corrige le sous-dimensionnement par la covariance classique en présence d'erreur de structure). Statistique enregistrée : **Ms du pire coin** de la boîte [K(1±ε_K)] × [τ(1±ε_τ)] × [θ ± ε_θ] (8 coins) pour le PI calculé sur le FOPDT. Décision : pire Ms ≤ M_C7, calibré comme M_E.
- **Combinaisons** ajoutées : **A∪E**, **A∩E** (déjà annoncées au §4), **A∪C7**. k = 10 candidats comparés au total (A1, A2, B, C, D, E, C7, A∪D, A∪E, A∩E) : niveau de Bonferroni 0,05/10.
- **Diagnostic, non candidat** : le test F(n, n) de comparaison FOPDT/SOPDT de `model_comparison.py` ; la revue signale que ses hypothèses (mêmes points de validation, résidus autocorrélés) ne tiennent pas. Sa taille réelle sera évaluée à part (non soumise à la règle d'adoption).
- **Rappel** (revue, §3 de la littérature) : la corrélation croisée résidus/entrée (critère C) a une puissance limitée sous un **échelon** (entrée constante après l'échelon). C reste un candidat ; sa faible puissance, si elle apparaît, est un résultat, pas un défaut de l'étude.

---

## Amendement v1.2 (2026-11-28, arbitrage du client, **avant toute analyse des résultats**)

**Décisions du client.** (1) Le risque de **5 %** est validé **provisoirement**, comme **objectif d'ingénierie pour l'étude, et non comme garantie de sécurité industrielle**. (2) Un seuil plus conservateur de **1 %** est évalué en parallèle, avec les compromis faux positifs / faux négatifs et leurs **intervalles de confiance**. (3) Il faut **distinguer l'acceptation d'un modèle imparfait de l'acceptation d'un réglage instable ou insuffisamment robuste**. (4) Aucun réglage identifié comme instable ne doit être recommandé ; en cas d'incertitude insuffisamment maîtrisée, la recommandation est refusée. (5) La porte actuelle est conservée tant que la pertinence du nouveau critère n'est pas démontrée **sur des données indépendantes**.

**Conséquences sur l'étude (rien d'autre ne change : règle d'adoption §7, vérité §2, populations §3) :**
- **Deux niveaux de risque** : α = 5 % (primaire, règle d'adoption inchangée) et α = 1 % (conservateur, présenté à part, avec la même procédure de calibration sur le jeu de calibration). Tableau de compromis par candidat et par α : **P(accepter | réglage inapte)** et **P(refuser | réglage apte)** avec intervalles de confiance bootstrap à 95 % (B = 2000) sur le jeu d'évaluation.
- **Deux notions séparées, rapportées séparément.** (a) *Le modèle est imparfait* : le procédé vrai n'est pas un FOPDT exact (P2, P3, P6, et P5/P7 en tranche 2) ; accepter un tel modèle n'est **pas** une erreur en soi si le réglage qui en découle est apte. (b) *Le réglage est inapte* : instable sur le procédé vrai **ou** Ms vrai > M_lim (même vérité que §2). L'**erreur grave** est l'acceptation d'un réglage inapte ; l'acceptation d'un modèle imparfait dont le réglage est apte est rapportée comme **bénigne**. Tableau croisé (modèle imparfait ? × réglage apte ?) × décision pour chaque candidat.
- **Statistique de sécurité directe** : fréquence à laquelle le réglage recommandé est **instable sur le procédé vrai**, sur toute la population et parmi les cas acceptés par chaque candidat. Un candidat pour lequel cette fréquence est non nulle parmi les cas acceptés est signalé comme tel ; si elle est non nulle pour la **porte actuelle** elle-même, c'est un constat de sécurité à traiter à part (la recommandation de `recommend` vérifie la stabilité sur le modèle et sur un ensemble de coins de paramètres, pas sur le procédé vrai).
- **Sur les données indépendantes** (§5, §7 iv) : la décision d'adopter ne peut pas reposer sur les seules populations synthétiques. Faute de jeu réel offrant une vérité de commande, la règle §7 iv **ne peut pas être satisfaite** : l'étude conclura alors, au mieux, « complémentarité démontrée **sur données synthétiques seulement** », sans modifier la porte.
