> **Annexe du LOT 0 — rapport d'audit indépendant (sous-agent, lecture seule), révision auditée c7915cd.** Les constats ont été vérifiés par échantillonnage par le responsable du lot (voir 00-synthese-audit.md, §1) ; tout constat non cité dans la synthèse reste à confirmer avant d'être retenu.

# Lot 0 / B — Audit scientifique indépendant : réglage PID, simulation, robustesse

Dépôt `/home/user/mkr`, révision `c7915cd` (branche `claude/science-engine-dev`). Audit en **lecture seule** : aucun fichier du dépôt n'a été modifié. J'ai seulement lancé des tests ciblés (`test_stability_margins`, `test_pi_tuning`, `test_pid_tuning`, `test_strategy_selection`, `test_tuning_comparison`, `test_pi_tuning_integrator` : 26 tests et 25 sous-tests réussis) et trois scripts de contre-vérification écrits hors du dépôt (`scratchpad/lot0/scripts/check1.py`, `check2.py` et `check3.py`). La suite complète n'a pas été relancée.

Conventions de ce rapport :
- **[F]** fait vérifié, avec la référence fichier:ligne ou le résultat d'un de mes scripts ;
- **[H]** hypothèse, ou point que je ne peux pas confirmer ;
- **« à vérifier »** : une référence citée par le code dont je n'ai pas pu confirmer le contenu exact. Je n'ajoute aucune référence que le code ne cite pas.

---

## 0. Synthèse exécutive

1. **Le cœur numérique est solide et bien vérifié.**
   - La dynamique FOPDT et SOPDT avec retard fractionnaire est exacte sous bloqueur d'ordre zéro (ZOH).
   - Les marges sont calculées par une recherche dense, puis affinées par `brentq`.
   - Les simulateurs PI et PID sont comparés à des oracles réellement indépendants : `scipy.signal.dlsim` et `cont2discrete`, avec une algèbre polynomiale écrite dans les tests.
   - Niveau atteint : **Vérifié indépendamment**, sur modèles synthétiques.
2. **Le réglage guidé (`recommend.py`) cherche une seule cible sous contrainte.**
   - Il retient la τc SIMC la plus petite, ou le Ki maximal, sous la contrainte Ms ≤ limite.
   - La limite vient de trois objectifs codés en dur : 1,4, 1,7 et 2,0.
   - Il n'y a **ni front de Pareto, ni préférence de préservation de l'actionneur, ni préférence de rejet de perturbation**.
   - Les saturations et la limitation de vitesse ne sont pas prises en compte.
3. **Deux défauts scientifiques reproduits par mes scripts** (§9, P0) :
   - **(a) Règle de la moitié (SOPDT avec τ2 ≤ θ).** La robustesse est jugée sur le modèle *réduit*, pas sur le SOPDT identifié. Sur le SOPDT vrai, Ms vaut 1,432 à 1,439 pour l'objectif « robuste » (limite 1,4). La recommandation annonce 1,369.
   - **(b) Retard d'échantillonnage pris égal à h/2.** Quand θ ≈ 0 et que h est grand devant τc, le Ms de la boucle échantillonnée réelle dépasse l'objectif. Avec la discrétisation de MKR (Euler explicite), Ms = 1,805 pour θ = 0, h = 1 s et τ = 10 s, avec l'objectif « équilibré » (limite 1,7, Ms annoncé 1,676).
   - Aucun test ne couvre ces deux cas.
4. **Plusieurs exigences ne sont pas couvertes, ou seulement en partie** :
   - la sélection automatique P/PI/PID n'est qu'une heuristique SIMC ; P et I seul ne sont jamais proposés ;
   - la comparaison avec un réglage existant n'existe pas ;
   - le refus d'un modèle non validé n'est appliqué que côté C#, le moteur ne l'applique pas ;
   - aucune détection de défaut matériel (frottement, jeu, oscillation) n'existe ;
   - la période d'échantillonnage utilisée est celle des **données**, pas le cycle du régulateur (côté C#, `PythonDiagnostics.cs:322`).

---

## 1. Équations implémentées, par module

### 1.1 `pi_tuning.py` (version 1.2.0)

Le module renvoie trois réglages.

| Réglage | Kp | Ti | Ligne |
|---|---|---|---|
| Clé `imc` (règle Lambda, ou Dahlin) | τ/(K(λ+θ)) | τ | `:16-17`, `:40` |
| Clé `simc` | τ/(K(τc+θ)) | min(τ, 4(τc+θ)) | `:18` |
| Clé `imcPublished` | (τ+θ/2)/(K(λ+θ/2)) | τ+θ/2 | `:35-36` |

Notes :
- λ et τc prennent tous deux la même valeur, `responseSeconds`.
- Le docstring renvoie à « MKR-SPEC-001 §8 », une spécification interne, et non à une publication.
- Le code attribue `imcPublished` à Rivera, Morari & Skogestad (1986).
  - **À vérifier** : je sais que ces expressions correspondent à la ligne PID de la table IMC avec approximation de Padé du premier ordre, après suppression de Td.
  - En revanche, je ne peux pas confirmer que la ligne « PI » publiée est exactement celle-ci : la table de 1986 contient plusieurs lignes PI.
  - Le test `test_pi_tuning.py:43-65` est une dérivation algébrique indépendante, mais il prouve l'expansion **PID**. La troncature en PI est un **choix de MKR**.
- La forme est idéale : u = Kp(e + ∫e/Ti), avec e = SP − PV. Le signe de Kp suit celui de K, ce qui gère l'action inverse.
- Les marges nominales sont calculées par `stability_margins.analyze`. Si l'analyse échoue, la valeur est `None` (`:26-31`).
- Refus :
  - clés inattendues, ou valeurs non numériques, ou |x| > 1e100 → `pi_input` ;
  - K = 0, τ ≤ 0, θ < 0 ou réponse ≤ 0 → `pi_domain` ;
  - coefficients non finis ou nuls → `pi_range`.
- Avertissements :
  - `exploratory_model` et `actuator_limits_unassessed` sont toujours émis (`:22`) ;
  - `tight_response` est émis si τc < θ (`:23`).

### 1.2 `pi_tuning_integrator.py` (version 1.0.0)

- Règle SIMC pour un procédé intégrateur : Kc = 1/(k'(τc+θ)) et Ti = 4(τc+θ) (`:52-53`).
- Référence citée : Skogestad 2003. La règle est conforme à ce que je connais de cet article.
- Les marges sont calculées par `loop_margins` :
  - grille logarithmique de 40 000 points, sans affinage local ;
  - seuls le **premier** passage à 0 dB et le premier croisement de −180° sont retenus (constat F-11 du dépôt).
- [F] Précision de `loop_margins` mesurée par `check2.py` contre `brentq` :
  - erreur sur la marge de gain d'environ 4e-4 en relatif (4,5390 contre 4,5408 ; 158,456 contre 158,491) ;
  - erreur sur la marge de phase de 0,003 à 0,007° ;
  - Ms exact à 1e-5 près.
  - C'est suffisant pour des avertissements.
- Refus : identiques au §1.1, avec K = 0, θ < 0 ou réponse ≤ 0 → `pi_domain`.

### 1.3 `pid_tuning.py` (version 1.1.0)

- PID SIMC série : Kc(1 + 1/(τI s))(1 + τD s), avec τD = τ2 (`:16-17`).
- Conversion en forme idéale, exacte :
  - Ti = τI + τ2 ;
  - Kp = Kc·Ti/τI ;
  - Td = τ2·τI/Ti.
- La dérivée n'est **pas filtrée**. Elle agit sur l'erreur (`controllerForm = continuous_ideal_unfiltered_error`).
- Avertissements émis : `derivative_filter_unspecified` et `vendor_equivalence_unverified`.
- Domaine : 0 < τ2 ≤ τ1, sinon `pid_second_pole`.
- Le module **n'applique pas** la règle « PID seulement si τ2 > θ ». Il produit un PID pour n'importe quel τ2, contrairement à `recommend.py`.

### 1.4 `stability_margins.py` (version 1.0.0)

- Boucle étudiée : L(s) = Kp(1 + 1/(Ti s) + Td s/(Tf s + 1)) · K e^(−θs)/∏(τi s + 1).
  - Ti = ∞ signifie l'absence d'action intégrale.
- Méthode :
  - grille de 24 000 points sur [1e-4/lent, 1e3/rapide] ;
  - la phase rationnelle est déroulée (*unwrap*), puis on retranche ωθ exactement ;
  - les passages à 0 dB et à −π(mod 2π) sont affinés par `brentq` (xtol 1e-14) ;
  - le maximum de Ms est affiné par `minimize_scalar`.
- Marge de retard = PM/ωc.
- Refus :
  - rapport des échelles de temps > 1e8, échelle > 1e12 ou échelle < 1e-9 → `margins_domain` ;
  - signes opposés de Kp et K → `margins_sign`.
- Avertissements :
  - seuils PM < 30°, GM < 2 et Ms > 2 (`:18-20`), cités d'après Åström & Murray (ch. 12) et Skogestad & Postlethwaite (ch. 2) ;
  - ce sont des valeurs usuelles reconnues, mais **à vérifier** quant au chapitre exact.
- Le diagnostic `nominal_unstable` repose sur « GM < 1 ou PM < 0 ». C'est une heuristique valable pour une boucle ouverte stable avec un seul passage. Le module le signale (`multiple_gain_crossovers`).

### 1.5 `recommend.py` (version 1.1.0) : réglage guidé

**Structure retenue (`:138-147`) :**
- FOPDT → PI ;
- SOPDT avec τ2 > θ → PID SIMC (forme série convertie en idéale, Tf = Td/N) ;
- SOPDT avec τ2 ≤ θ → règle de la moitié : τ1 ← τ1 + τ2/2 et θ ← θ + τ2/2, puis PI ;
- intégrateur → PI SIMC pour intégrateur.

Ces règles suivent Skogestad 2003, conformément à ma connaissance.

**Retard effectif :** θ + h/2 (`:148`), cité d'après Skogestad 2003. **À vérifier** sur le texte, que je ne peux pas consulter ici.

**Recherche :**
- τc = m·θeff, avec m ∈ {0,25·1,25^i, i = 0…24}, soit environ 0,25 à 52,9 (`:34`) ;
- la recherche retient le **premier** m tel que la boucle soit stable sur le nominal et sur les 8 coins, avec Ms ≤ limite ;
- le pas de la grille vaut 25 %, donc l'optimum est quantifié à 25 % près.

**Incertitude (`:61-79`) :**
- boîte de demi-largeur max(2σ, v %·|valeur|) sur K, τ1 et θ, soit 8 coins ;
- intégrateur : pente et retard, soit 4 coins ;
- τ2 n'est pas inclus : la note `second_pole_uncertainty_not_propagated` le signale ;
- refus si |K| − hg ≤ 0 ou si τ1 − ht ≤ 0.

**Optimisation PI (`:400-445`), d'après Åström, Panagopoulos & Hägglund (Automatica 34, 1998, 585-601) :**
- maximise Ki = Kp/Ti sous Ms ≤ limite sur chaque coin ;
- grilles : Ti en 18 points sur [0,05τ ; 3τ], Kp en 160 points, ω en 3 000 points ;
- puis 30 bissections, un affinage de Ti sur 9 points, et une vérification à pleine résolution avec un recul de 0,5 % (au plus 40 itérations).
- Le critère « IAE ≈ 1/Ki » de rejet de charge est conforme à ce que je connais de l'article, mais reste **à vérifier**.

**Optimisation PID (`:511-546`) :**
- Td = r·Ti, avec r ∈ {0,05 ; 0,1 ; 0,15 ; 0,2 ; 0,25} (`:457`), et Tf = Td/N ;
- même critère de maximisation de Ki ;
- vérification finale sur une grille de 40 000 points.

**Critères de performance (`closed_loop_performance`, `:572-643`) :**
- simulation par Euler explicite, avec un tampon de retard arrondi au pas (`int(round(delay/dt))`) et dt ≤ θ/20, soit une erreur sur le retard ≤ 2,5 % ;
- P et I agissent sur l'erreur, D sur la mesure (filtre Euler explicite) ;
- sorties : IAE, ISE et ITAE pour un échelon de consigne et pour un échelon de charge à l'entrée ; dépassement ; établissement à 2 % ; peakMv ; |Kp| ; gain haute fréquence Kp(1 + Td/Tf).

**Bruit vers commande (`:557-569`) :**
- valeur efficace de |C/(1 + CP)| sur une grille linéaire de 0 à π/Ts ;
- le repliement de spectre et le bloqueur sont ignorés, ce qui est annoncé dans le code.

### 1.6 `strategy_selection.py` et `tuning_comparison.py` (version 1.0.0)

- `compare` réalise une **dominance de Pareto sans pondération** sur des critères choisis par l'appelant : iae, ise, itae, saturationSeconds, rateLimitedSeconds, mvTravel (`tuning_comparison.py:50-60`).
  - Un front de taille 1 produit une préférence.
  - Sinon, le résultat est `no_unique_preference` ou `equal`.
  - **Le front n'est pas renvoyé en tant que tel.** Il faut le reconstruire à partir des lignes de `runs`.
- `select` simule P, PI et PID **fournis par l'appelant** (`strategy_selection.py:23-45`) :
  - refus si l'erreur finale dépasse le seuil ;
  - un PID sans bruit de mesure reçoit `derivative_noise_response_unassessed`.
  - Une seule réserve suffit pour qu'**aucune** stratégie ne soit retenue (`tuning_comparison.py:47`).
- [F] Aucun appel C# à `select_strategy` ou à `compare_tunings` n'a été trouvé dans `src/`. Ces deux fonctions ne sont accessibles que dans le moteur.

### 1.7 Simulateurs : `fopdt_simulation.py`, `pi_simulation.py` 1.1.0, `pid_simulation.py` 0.2.0, `open_loop_simulation.py` 1.1.0, `pi_mode_transition.py` 1.2.1

**Procédé FOPDT (`plant_step`, `fopdt_simulation.py:31-47`) :**
- discrétisation **exacte** sous ZOH ;
- chaque pas est découpé à la fraction de retard ; on utilise u[k − m − 1] sur la durée f, puis u[k − m] sur h − f.
- [F] `check2.py` : erreur maximale de 8,9e-16 contre la forme fermée, pour θ = 1,37 s et h = 0,2 s.

**Procédé SOPDT (`second_order_step`, `:50-80`) :**
- cascade de deux constantes de temps, avec un terme de couplage exact ;
- j'ai refait la dérivation : (dt/f)/(dt(1/f − 1/s))·(e^(−dt/s) − e^(−dt/f)), qui correspond bien au code.
- Petite incohérence : `plant_step` contient une correction `isclose` lorsque la fraction vaut h (`:35-36`), mais `second_order_step` ne la contient pas. L'effet est négligeable [H].

**Régulateurs :**
- PI : u[k] = Kp·e[k] + I[k], puis I[k+1] = I[k] + Kp·h/Ti·e[k] (Euler explicite), sans retard de calcul.
- PID : D_k = Tf/(Tf+h)·D_{k−1} − Kp·Td/(Tf+h)·(y_k − y_{k−1}), soit Euler implicite sur la mesure.

**Actionneur :**
- saturation en amplitude, puis limitation de vitesse ±r·h autour de la MV précédente (initialisée à 0 en écart) ;
- anti-windup par intégration conditionnelle, qui gèle l'intégrale si la sortie brute dépasse la borne effective et que l'incrément pousse dans le même sens.

**Transfert manuel → automatique :** l'intégrale suit la MV **appliquée**, ce qui donne un transfert sans à-coup.

**Indicateurs :**
- IAE et ISE par sommes à gauche ;
- ITAE par somme à gauche ;
- temps de montée 10-90 % par interpolation ;
- établissement à **5 %** (`pi_simulation.py:14`), alors que `recommend.py` utilise **2 %** (`:621`). Les définitions ne sont pas homogènes entre modules.

### 1.8 `pi_robustness.py` et `pi_combined_robustness.py` (version 1.0.0)

- Coefficients PI fixes.
- `pi_robustness` fait varier un facteur à la fois, ±v % (7 cas).
- `pi_combined_robustness` utilise une grille 3×3×3 (27 cas), avec des enveloppes ponctuelles de la PV.
- Les avertissements sont honnêtes : `not_a_stability_proof`, `not_a_confidence_band`.
- [F] La variation est multiplicative (`pi_robustness.py:39`, `pi_combined_robustness.py:41`). **Un retard nominal nul ne reçoit donc aucune incertitude de retard.**
- Aucune marge ni aucun Ms n'est calculé dans ces modules : seules des métriques temporelles le sont.

### 1.9 `model_drift.py` (version 0.2.0) et `closed_loop.py` (version 0.1.0)

**Dérive (`model_drift.py`) :**
- prédiction d'un nouvel essai par le modèle stocké ;
- verdict selon les critères de la porte de validation ;
- ré-estimation des paramètres et indicateur « dérive » si les trois conditions sont réunies :
  - écart > 3σ ;
  - écart relatif > 10 % (gain), 15 % (τ), 30 % (τ2) ou 25 % (θ) ;
  - écart résoluble au regard de l'intervalle d'apparition (`:19-22`, `:148-153`).
- [F] Le verdict de dérive **ne bloque pas** le réglage. Aucun lien n'a été trouvé entre `check_model_drift` et `recommend_tuning`.

**Boucle fermée (`closed_loop.py`) :**
- méthode en deux étapes, citée d'après Van den Hof & Schrama (Automatica, 1993) :
  - régression FIR de la MV sur la consigne, avec au plus 150 coefficients et une régularisation 1e-9·trace ;
  - ajustement FOPDT par `least_squares`.
- Le modèle discret est `lfilter([0, (1−a)K], [1, −a])` sur une entrée interpolée linéairement au retard fractionnaire.
  - Cela mélange ZOH et interpolation linéaire [H : biais faible sur θ, non quantifié].
- Bootstrap par blocs : 30 tirages, graine fixe 12345 (`:178`).
- Refus testés : saturation, mode manuel, excitation insuffisante, échantillonnage non uniforme.

---

## 2. Hypothèses, domaine et comportement hors domaine

| Module | Hypothèses | Comportement hors domaine |
|---|---|---|
| Tous | Modèle linéaire, variables d'écart, procédé initialement à l'équilibre, sens d'action donné par le signe de K | `ValueError` avec un code (`pi_domain`, `sim_domain`, etc.) ; pas de résultat partiel |
| `pi_tuning`, `pid_tuning` | FOPDT ou SOPDT stable ; τc choisie par l'utilisateur | `tight_response` si τc < θ ; marges `None` si l'analyse échoue (aucun refus) |
| `stability_margins` | Boucle ouverte stable ; un seul passage attendu | Refus des échelles de temps absurdes ; avertissement en cas de passages multiples ou d'absence de passage |
| `recommend` | Modèle **supposé accepté** (non vérifié par le moteur) ; incertitude en boîte avec pire cas supposé aux coins ; boucle continue + h/2 ; ni saturation ni limite de vitesse | Refus explicite (`decision = refused`, raisons et actions) si l'incertitude exclut le signe de K ou τ > 0, ou si aucun point de grille ne tient l'objectif |
| Simulateurs | Grille de 2 à 5 000 pas ; durées multiples du pas (tolérance 1e-8) ; bornes lower < 0 < upper | Refus `sim_grid` ou `sim_domain` ; `coarse_sample` si h > τ/10 ; `delay_beyond_horizon` |
| Robustesse | Variation ≤ 50 % ; coefficients PI fixes | Refus `robust_domain` ou `combined_domain` |

**Hypothèse « le pire cas est sur un coin »** :
- [H] Elle n'est pas démontrée dans le code ni dans la documentation.
- [F] `check1.py` : sur trois boîtes FOPDT, une grille intérieure de 7³ points ne trouve **aucun** Ms pire que celui des coins. Le pire coin est toujours (gain+, τ−, θ+).
- C'est un indice, pas une preuve, par exemple pour un SOPDT ou pour une boîte large.

---

## 3. Seuils, constantes et paramètres codés en dur

J = justification présente dans le code ; — = absente.

| Fichier:ligne | Valeur | Rôle | J |
|---|---|---|---|
| `recommend.py:29` | robuste 1,4 ; équilibré 1,7 ; performance 2,0 | Ms maximal par objectif | Åström & Hägglund 2006, plage 1,2-2,0 (choix des trois points : —) |
| `recommend.py:31` | N = 10 | Tf = Td/N | « usuel 2-20 » |
| `recommend.py:108` | N ∈ [2, 100] accepté | — | — (la documentation dit 2-20) |
| `recommend.py:32` | COVERAGE = 2σ | demi-largeur de la boîte | « ~95 % gaussien » (vrai par paramètre, pas pour la boîte jointe à 3 dimensions) |
| `recommend.py:34` | τc = 0,25·1,25^i, i < 25 | grille de vitesse | SIMC (justifie τc = θ, pas le pas de 25 %) |
| `recommend.py:115` | variation ≤ 50 % | domaine | — |
| `recommend.py:148` | h/2 | retard d'échantillonnage | Skogestad 2003 (domaine de validité non contrôlé, voir §9) |
| `recommend.py:332` | `_W = logspace(-4, 2, 2500)` | **code mort** (jamais utilisé) | — |
| `recommend.py:373`, `:444`, `:424` | 30 bissections ; recul de 0,995 ; 40 itérations | recherche du Kp maximal | — |
| `recommend.py:406-408` | ω en 3 000 points sur [1e-3/max(τ1, θ) ; 100/θ] ; Kp en 160 points ; Ti sur [0,05τ ; 3τ] en 18 points (intégrateur : [0,5θ ; 400θ] en 22 points) | grilles de l'optimisation PI | partielle (« couvre les passages < 20/θ ») |
| `recommend.py:417` | Ti × [0,8 ; 1,25] en 9 points | affinage | — |
| `recommend.py:457` | Td/Ti ∈ {0,05 … 0,25} | grille de l'optimisation PID | « usuel, Åström & Hägglund 2006 » (à vérifier) |
| `recommend.py:458` | 1 000 fréquences, 48 gains, 10 Ti, 16 bissections | recherche PID | — |
| `recommend.py:539-540` | 40 000 points ; 60 reculs | vérification PID | — |
| `recommend.py:562` | 20 000 points sur [0 ; π/Ts] | bruit vers commande | — |
| `recommend.py:572`, `:579`, `:581` | horizon 40·échelle ; dt = min(échelle/400, τ/10, θ/20, Tf/5) ; plafond 1,5e6 pas | simulation des critères | ordre 1 vérifié par test |
| `recommend.py:582` | si le plafond est atteint, l'horizon est **tronqué en silence** mais `horizonSeconds` annonce l'horizon complet | défaut mineur [F, lecture du code] | — |
| `recommend.py:621`, `:623` | établissement à 2 % ; divergence si |y| > 1e6 | critères | — |
| `recommend.py:634`, `:638` | 240 points affichés ; fenêtre max(8·échelle, 1,6·établissement) | affichage | — |
| `recommend.py:382-383` | dt = max(min(échelle, θ)/40, échelle/4000) ; 60·échelle | dépassement nominal (Euler) | — |
| `stability_margins.py:18-22` | PM 30°, GM 2, Ms 2 ; 24 000 points ; rapport 1e8 | avertissements | Åström & Murray ; Skogestad & Postlethwaite |
| `stability_margins.py:39`, `:49` | grille [1e-4/lent ; 1e3/rapide] ; échelles dans [1e-9 ; 1e12] | domaine | partielle |
| `pi_tuning_integrator.py:15-17`, `:24` | 30°, 2, 2 ; 40 000 points | avertissements | — (dupliqués depuis `stability_margins`) |
| `pi_tuning.py:12` | |x| ≤ 1e100 | domaine | — |
| `fopdt_simulation.py:6`, `:13`, `:16`, `:35` | |x| ≤ 1e12 ; ≤ 5 000 pas ; tolérance de grille 1e-8 ; `isclose` 1e-12 | simulateurs | — |
| `pi_simulation.py:14` | bande de 5 % | temps d'établissement | — (incohérent avec les 2 % de `recommend`) |
| `pi_simulation.py:102` et équivalents | h > τ/10 → `coarse_sample` | avertissement | — |
| `pid_simulation.py:133` | h > Tf → `coarse_derivative_filter` | avertissement | — |
| `pi_robustness.py:30` ; `pi_combined_robustness.py:24` | 0 < v ≤ 50 % | domaine | — |
| `model_drift.py:19`, `:22` | 3σ ; 10, 15, 30 et 25 % | dérive | « non digne d'action » (pas de source) |
| `model_drift.py:72`, `:84` | pas du jacobien 0,05·cadence ou 1e-4 relatif ; conditionnement 1e-6 | erreurs-types | — |
| `closed_loop.py:23-31` | 8 lignes de base ; 150 coefficients ; excitation 20σ ; marge de saturation 0,5 % ; uniformité 1 % ; 30 tirages bootstrap ; blocs = fenêtre/20 ; dispersion maximale 0,3 (gain) et 0,5 (τ) ; décalage-1 0,5 | identification en boucle fermée | partielle ; F-02 resté ouvert pour 20σ |
| `closed_loop.py:166`, `:217` | régularisation 1e-9 ; RMSE maximal = max(2σ, tolérance/2) | — | — |
| `C#` `GuidedTuning.cs:45` | 1,4, 1,7 et 2,0 **dupliqués** dans le C# | risque de divergence entre C# et moteur | contrôle croisé dans `RecommendCoreAsync` |

Aucun module ne code en dur de **cible de temps de réponse** : τc est saisie (`pi_tuning`) ou recherchée (`recommend`). Aucun **λ** n'est imposé : λ = `responseSeconds`.

---

## 4. Simulation du temps mort et précision

| Endroit | Méthode | Précision |
|---|---|---|
| `plant_step` (PI, PID, transfert, boucle ouverte avec limite de vitesse) | ZOH exact, retard fractionnaire par découpe du pas | exact à la précision machine ([F] 8,9e-16) |
| `second_order_step` | ZOH exact de la cascade, découpe fractionnaire | exact (dérivation refaite) ; oracle scipy < 1e-8 (`test_pid_simulation.py`) |
| `open_loop_simulation` sans limite de vitesse | forme fermée continue | exact |
| `recommend.closed_loop_performance` | Euler explicite ; retard arrondi à dt ≤ θ/20 | erreur sur θ ≤ 2,5 % ; ordre 1 vérifié (`test_recommend.py:430-439`) |
| `recommend._nominal_overshoot` | Euler ; dt ≥ échelle/4000 ; retard arrondi | dépassement annoncé à ±3 points selon le test |
| `closed_loop.fit_fopdt` | ZOH avec interpolation linéaire de l'entrée retardée | [H] biais de θ inférieur à un pas, non quantifié |

**Rapport entre pas et retard :**
- les simulateurs exacts n'imposent pas h ≤ θ et n'en ont pas besoin ;
- le seul avertissement porte sur h > τ/10 ;
- en revanche, `recommend` représente l'échantillonnage par **h/2 de retard continu**, sans aucun garde-fou sur h/θ ou h/τc (voir §9, défaut P0-b).

---

## 5. Tests : indépendance des oracles et graines

| Test | Oracle | Indépendant ? |
|---|---|---|
| `test_stability_margins.py` | formes fermées P sur FOPDT, PI avec Ti = τ, Lambda sans retard ; bissection écrite dans le test ; oracle temporel (GM ×0,9 converge, ×1,1 diverge) | **Oui** (analytique). Réserve : l'oracle temporel utilise `pi_simulation` de MKR |
| `test_pi_tuning.py` | formules recopiées ; dérivation IMC/Padé indépendante | Partiel : les formules recopiées sont auto-référentes par nature ; la dérivation est indépendante pour le PID, pas pour la troncature en PI |
| `test_pid_tuning.py` | équivalence série/idéale dans le domaine fréquentiel ; recoupement PID SOPDT = PI FOPDT | Oui (algèbre), mais aucun résultat publié |
| `test_pi_simulation.py`, `test_pid_simulation.py` | `scipy.signal.dlsim` avec algèbre polynomiale du test (cas linéaires) ; seconde implémentation + `cont2discrete` (cas non linéaires) ; test de sensibilité de l'oracle (« détecte un Ti/Td faux ») | **Oui** pour la dynamique. La seconde implémentation **partage la convention** d'anti-windup, ce qui est reconnu dans la documentation (F-10) |
| `test_support_plant.py` | ZOH par `scipy cont2discrete`, avec découpe fractionnaire | **Oui** |
| `test_recommend.py` | Ms par balayage dense écrit dans le test (30 000 points) ; simulation d'Euler écrite dans le test ; formes fermées IAE, ISE, ITAE ; Monte-Carlo pour le bruit (graine 1) | **Oui** pour Ms et la stabilité. Les coins sont recalculés avec la constante `COVERAGE` du module, donc la **définition de la boîte** est auto-référente. **La règle de la moitié n'est jamais jugée sur le SOPDT vrai** (`:75-80`) |
| `test_reference_campaign.py` | chaîne complète sur 8 procédés synthétiques ; jugement sur le procédé **vrai** ; critères fixés d'avance ; graine fixe (`default_rng(3)` via `test_process_diversity.py:43`) | **Oui**. Limites : modèle structurellement exact ; le cas SOPDT relève du PID, pas de la règle de la moitié ; boucle continue + h/2, pas la boucle échantillonnée vraie |
| `test_pid_noise_qualification.py` | réécriture de **la même récurrence** que le code (`:42-51`) | **Auto-référent** (l'intitulé « independent discrete oracle » est trompeur) |
| `test_strategy_selection.py` (25 lignes), `test_tuning_comparison.py` (22 lignes) | cas jouets | Auto-référent et **très mince** : aucun test de front à trois candidats, ni de critère d'effort, ni de cas P avec erreur statique |
| `test_pi_robustness.py`, `test_pi_combined_robustness.py` | comparaison à `pi_simulation` | Auto-référent (cohérence interne) |
| `test_scientific_workflow.py` | forme fermée de la saturation permanente | Oui, dans un cas très particulier |
| `test_process_diversity.py` | récupération des paramètres vrais | Oui (vérité synthétique) |

**Graines :** toutes sont fixées et documentées.
- `Random` 3, 5, 7, 11 et 20261124 ;
- `default_rng` 1, 3, et 0…9 / 0…19 dans les boucles de `test_closed_loop` et `test_model_drift`.

Les taux de fausses alarmes, par exemple 20 graines dans `test_model_drift.py:56`, sont estimés **sans intervalle de confiance**. Avec 20 tirages, un taux de 0/20 reste compatible avec environ 15 % de fausses alarmes à 95 % [calcul élémentaire, borne de la « règle de trois »].

---

## 6. Classement de maturité

Échelle : Implémenté < Compilé < Testé < Vérifié indépendamment < Qualifié dans un domaine. « Non qualifié » s'applique à tout ce qui n'a pas d'essai réel.

| Module | Classement honnête | Commentaire |
|---|---|---|
| `fopdt_simulation` (dynamique) | **Vérifié indépendamment** | ZOH exact, oracles scipy |
| `pi_simulation`, `pid_simulation` | **Vérifié indépendamment** (partie linéaire) ; **Testé** (convention d'anti-windup) | non qualifié |
| `pi_mode_transition`, `open_loop_simulation` | **Vérifié indépendamment** (dynamique) | convention de suivi non qualifiée |
| `stability_margins` | **Vérifié indépendamment** (formes fermées) | modèle nominal |
| `pi_tuning` (Lambda, SIMC) | **Vérifié** (formules et marges) | `imcPublished` : attribution exacte à vérifier |
| `pi_tuning_integrator` | **Vérifié indépendamment** (simulation écrite dans le test) | grille sans affinage, premier passage seulement |
| `pid_tuning` | **Testé et vérifié** (algèbre) | dérivée non filtrée : pas utilisable en l'état |
| `recommend` (FOPDT, intégrateur, PID si τ2 > θ) | **Vérifié indépendamment ; qualifié dans un domaine synthétique** (8 procédés, h petit devant θ) | non qualifié en réel |
| `recommend` (règle de la moitié, ou h grand devant θ) | **Testé seulement ; défaut démontré** | voir §9 |
| `recommend.optimise_pi`, `optimise_pid` | **Vérifié indépendamment** (contrainte, frontière) | grilles discrètes |
| `strategy_selection`, `tuning_comparison` | **Testé** (minimal) ; non relié à l'interface | |
| `pi_robustness`, `pi_combined_robustness` | **Testé** (cohérence) | métriques temporelles seulement |
| `model_drift` | **Testé** (Monte-Carlo, graines fixées) | ne bloque rien |
| `closed_loop` | **Testé** (expérimental) | dispersion sous-estimée d'environ 30 % (documentation) |
| Toutes les fonctions | **Non qualifié** sur procédé ou automate réel | conforme à `docs/validation/etat-de-maturite.md` |

---

## 7. Écarts avec les exigences

| Exigence | État | Constat |
|---|---|---|
| Sélection automatique P/PI/PID | **Partiel** | Heuristique SIMC dans `recommend.py:138-147` : P, I seul et PI+compensation du retard ne sont jamais proposés (F-06 ouvert). `strategy_selection` compare des P/PI/PID **fournis**, en simulation, sans calcul de gains ; il n'est pas relié à l'interface. Le PID optimisé est calculé à côté et comparé au PI par Ki (`pidNotBetterThanPi`), mais la structure recommandée reste celle de SIMC |
| Optimisation multicritère sans pondération (Pareto) | **Partiel ou absent** | `tuning_comparison` fait de la dominance de Pareto sans poids, mais sur des runs existants ; il ne renvoie pas le front et n'est pas relié. `recommend` optimise **un** critère (τc minimale, ou Ki maximal) sous **une** contrainte (Ms) : c'est lexicographique, pas un front Pareto (IAE, effort, robustesse, bruit). Les « alternatives » (voisins sur la grille de τc) sont une coupe 1-D, pas un front |
| Préférences utilisateur (équilibré, stabilité, réactivité, préservation, rejet de perturbations) | **Partiel** | Trois objectifs seulement (robuste, équilibré, performance), qui ne changent **que la limite de Ms**. Ni « préservation de l'actionneur » (pas de contrainte d'effort, de bruit ou de course) ni « rejet de perturbations » (le PI optimisé maximise Ki, mais il est calculé en parallèle et non choisi selon une préférence). La correspondance préférence → objectif n'est documentée que pour Ms |
| Rejet de perturbations | **Partiel** | Échelon de charge à l'entrée du procédé seulement (IAE de charge, Ki). Pas de perturbation en sortie ou mesurée, pas de spectre de perturbation, pas de compensation anticipatrice (*feed-forward*) |
| Effort de commande | **Indicatif** | peakMv, |Kp|, gain haute fréquence, bruit vers commande, mvTravel (simulateurs). Publiés, **jamais utilisés comme contrainte**. MKR ne connaît pas la plage de l'actionneur (documentation de `reglage-guide.md`) |
| Saturations | **Simulateurs seulement** | Absentes du réglage guidé : avertissement `performance_is_nominal_without_actuator_limits` |
| Limitation de vitesse de l'actionneur | **Simulateurs seulement** | idem |
| Robustesse sous incertitude (K, τ, θ) | **Partiel** | Boîte à 8 coins sur K, τ1 et θ (±2σ ou ±v %) : bien. Manques : τ2 non propagé ; règle de la moitié jugée sur le modèle réduit (défaut P0-a) ; pire cas aux coins supposé ; θ = 0 sans incertitude en mode « variation » (`recommend.py:65`, `pi_robustness.py:39`) ; pas de dérive temporelle |
| Comparaison avec le réglage existant | **Absent** | Aucune entrée « réglage actuel » trouvée dans le moteur ni dans `src/` : on ne peut pas calculer les marges ou les performances des gains en place ni les comparer à la recommandation. `stability_margins.analyze` et `pid_simulation` le permettraient techniquement |
| Refus quand le modèle n'est pas validé | **Côté C# seulement** | `IdentificationStudy.RequireAcceptedForTuning` (`Identification.cs:54`), appelé dans `IdentificationWorkspace.cs` (13 appels). Le moteur `recommend_tuning` accepte n'importe quels paramètres, alors que son docstring dit « from an ACCEPTED model ». Le verdict de `model_drift` ne bloque pas le réglage |
| Ne pas recommander un PID si un défaut matériel peut expliquer le comportement | **Absent** | Aucune détection de frottement statique (*stiction*), de jeu, d'hystérésis, de zone morte, d'oscillation ou de capteur figé dans le moteur. `EquipmentProfiles.cs:14` contient un champ `Hysteresis` validé mais inutilisé. `feuille-de-route.md:9` le liste comme manquant |

**Constat supplémentaire (C#) :** la période d'échantillonnage transmise au réglage guidé est `Training.HorizonSeconds/(Samples − 1)` (`PythonDiagnostics.cs:322`), c'est-à-dire la cadence d'**acquisition des données**, et non le cycle du régulateur (période de la tâche automate). C'est une erreur de modélisation potentielle dans les deux sens : un historique enregistré à 1 s pour une tâche à 100 ms, ou l'inverse.

---

## 8. Risques scientifiques et de régression

1. **Promesse de robustesse non tenue dans deux cas** (§9, défauts P0-a et P0-b) : la recommandation affiche un Ms au pire cas que la boucle vraie dépasse.
2. **Contraintes dupliquées entre C# et Python** (1,4, 1,7 et 2,0) : une modification d'un côté seulement casse le contrat. Elle serait détectée par le contrôle croisé de `RecommendCoreAsync`, mais seulement à l'exécution.
3. **Documentation contradictoire** :
   - `reglage-guide.md` dit « Pour un PID, l'optimisation n'est pas disponible (`optimisation_covers_pi_only`) », alors que ce code n'existe nulle part et que `optimise_pid` existe ;
   - « Limites : … sans filtre de dérivée » contredit le §4bis du même document ;
   - « erreurs-types seulement calculées pour FOPDT » contredit l'audit F-04 ;
   - `PID-06A` dit « aucun coefficient PID n'est généré automatiquement », ce qui est périmé.
4. **Code mort ou mal attribué** :
   - `_W` (`recommend.py:332`) n'est jamais utilisé ;
   - dans le docstring de `recommend.py:15`, la règle SIMC pour intégrateur est placée sous la puce Åström & Hägglund, alors qu'elle vient de Skogestad 2003 ;
   - l'expression `-y * (1 if gain > 0 else 1)` (`recommend.py:620`) ne fait rien.
5. **Horizon tronqué en silence** (`recommend.py:581-582`) : pour θ ou Tf très petits devant Ti (rapport supérieur à environ 37 500), les critères sont calculés sur un horizon plus court que celui annoncé.
6. **Grille de τc à pas de 25 %** : la recommandation peut être jusqu'à 25 % plus lente que l'optimum continu. Ce choix est conservateur et non annoncé comme tel.
7. **Définitions d'établissement incohérentes** (5 % contre 2 %) entre le simulateur et le réglage guidé : comparaison trompeuse à l'écran [H selon l'affichage].
8. **Tests de sélection et de comparaison très minces** (47 lignes au total) : toute refonte de la logique Pareto est mal protégée contre les régressions.
9. **`test_pid_noise_qualification` se dit « indépendant » mais réimplémente la même récurrence** : une fausse impression de vérification.
10. **Couverture statistique de la boîte** : 2σ par paramètre donne environ 95 % par paramètre, mais seulement environ 86 % pour la boîte jointe à 3 dimensions si les paramètres sont indépendants (0,954³) [calcul élémentaire]. Les erreurs-types sont aussi des approximations locales du premier ordre. L'affirmation « ~95 % » (`recommend.py:18`, `:32`) est donc optimiste pour la boîte.

---

## 9. Défauts reproduits (détails)

### P0-a : règle de la moitié jugée sur le modèle réduit

- [F] Lecture du code : `recommend.py:144` remplace (τ1, θ) par (τ1 + τ2/2, θ + τ2/2), puis `_corners` et `_evaluate` n'utilisent plus que ce modèle réduit (`tau2 = None`).
- [F] `check1.py` : objectif « robuste » (limite 1,4), h = 0,2 s, sans incertitude.

| SOPDT vrai (K, τ1, τ2, θ) | Ms annoncé | Ms sur le SOPDT vrai (+h/2) |
|---|---|---|
| (1, 10, 4, 5) | 1,369 | **1,439** |
| (1, 10, 5, 5) | 1,369 | **1,432** |
| (1, 20, 8, 10) | 1,369 | **1,439** |
| (1, 5, 4,9, 5) | 1,369 | 1,388 |

- L'objectif est violé dans 3 cas sur 4.
- La correction est simple : juger les coins sur le SOPDT identifié (τ2 conservé dans `analyze`), tout en gardant la règle de la moitié pour *calculer* le réglage.

### P0-b : retard d'échantillonnage pris égal à h/2 hors de son domaine

- [F] `check3.py` : Ms exact de la boucle échantillonnée, procédé ZOH avec retard fractionnaire et PI à Euler explicite identique à `pi_simulation`, comparé au Ms annoncé (objectif « équilibré », limite 1,7).

| (K, τ, θ, h) | Ms annoncé | Ms de la boucle discrète (Euler explicite) |
|---|---|---|
| (1, 10, 1, 0,1) | 1,628 | 1,623 |
| (1, 10, 1, 1) | 1,610 | 1,592 |
| (1, 10, 1, 2) | 1,610 | 1,457 (prudent) |
| (1, 10, 0, 1) | 1,676 | **1,805** |
| (1, 10, 0, 3) | 1,610 | **1,760** |

- Avec un intégrateur d'Euler implicite (`check3.py`, autre réalisation d'automate possible), on obtient Ms = 2,36 et 2,41 dans les deux derniers cas.
- Le résultat **dépend donc de la discrétisation du bloc**, que le moteur ignore.
- [H] Le défaut apparaît quand θ est petit devant h, c'est-à-dire quand τc devient de l'ordre de h. C'est fréquent pour des boucles rapides (débit, pression) lues à la cadence d'un historique.
- Remède proposé :
  - Ms discret exact (z) en plus du Ms continu ;
  - ou garde-fou τc ≥ k·h et refus ou avertissement si h > θ ;
  - ainsi qu'un paramètre « période du régulateur » distinct de la cadence des données.

---

## 10. Consolidations et développements, par priorité

### P0 (justesse de la promesse de robustesse)

1. **Règle de la moitié.** Évaluer Ms et la stabilité des coins sur le **SOPDT identifié**, et non sur le modèle réduit. Ajouter le test correspondant sur le SOPDT vrai, avec les quatre cas du §9.
2. **Échantillonnage.**
   - Ajouter un calcul du Ms de la boucle **discrète** : ZOH, retard fractionnaire, discrétisation du PI ou du PID déclarée (Euler explicite ou implicite, Tustin).
   - Ou, à défaut, refuser ou avertir quand h > θ ou quand τc < 2h [seuil à justifier].
   - Ajouter un test à θ = 0 et h grand.
3. **Période du régulateur.** Ajouter une entrée explicite, distincte de la cadence des données (`PythonDiagnostics.cs:322`). L'utiliser dans h/2 et dans le bruit vers commande.
4. **Refus côté moteur.** `recommend_tuning` doit exiger une preuve d'acceptation (identifiant ou empreinte de la décision de validation), et pas seulement s'en remettre au C#. Il doit aussi refuser, ou avertir, si une dérive (`model_drift` = `drift` ou `inconsistent_*`) est connue pour l'étude.

### P1 (exigences fonctionnelles manquantes)

5. **Front de Pareto réel.** Générer un ensemble de candidats (grille de τc, PI ou PID optimisés, P et I si pertinents) évalués sur des critères non pondérés :
   - IAE de consigne et IAE de charge ;
   - pic de MV et bruit vers commande ;
   - Ms au pire cas ;
   - temps de saturation sous les limites réelles.

   Renvoyer le **front** (les candidats non dominés), et non seulement une préférence unique. Relier `tuning_comparison` à ce flux.
6. **Préférences utilisateur** (équilibré, stabilité, réactivité, préservation, rejet de perturbations). Les définir chacune comme un **choix documenté sur le front** :
   - par exemple, contrainte Ms pour « stabilité » ;
   - contrainte sur le bruit vers commande ou sur peakMv pour « préservation » ;
   - Ki maximal ou IAE de charge minimal pour « rejet » ;
   - τc minimale pour « réactivité ».

   Tout cela sans poids cachés. Mettre à jour la constante `OBJECTIVES` et le C# en même temps, idéalement depuis une source unique.
7. **Limites de l'actionneur dans le jugement.** Transmettre la plage de MV, la vitesse maximale et l'amplitude de l'échelon au réglage guidé. Simuler chaque candidat avec `pid_simulation` (saturation, vitesse, anti-windup, déjà vérifiés). Refuser ou déclasser les candidats qui saturent.
8. **Comparaison avec le réglage existant.** Ajouter une entrée « gains actuels » (avec la forme et les unités du bloc, via les convertisseurs de blocs). Calculer ses marges, son Ms au pire cas et ses critères. Le placer sur le même front que les candidats.
9. **Garde « défaut matériel ».** Avant toute recommandation, détecter dans les données :
   - oscillation en boucle fermée ;
   - frottement statique ou jeu (par exemple, forme MV/PV en cycle limite) ;
   - zone morte ;
   - capteur figé ou quantifié.

   Bloquer la recommandation d'un PID, ou d'un nouveau réglage, quand un défaut matériel explique le comportement. Les méthodes sont à choisir et à citer : je n'en invente pas la référence.
10. **Propager l'incertitude de τ2** (SOPDT) dans la boîte, et le retard nul dans le mode « variation » (borne additive en plus de la borne multiplicative).
11. **Sélection P/PI/PID réellement automatique.** Évaluer P (erreur statique tolérée), I seul ou PI pour un retard dominant (F-06), et le PID, sur le même front. Écarter le PID si son gain en Ki ne compense pas son coût en bruit, selon un critère explicite.

### P2 (consolidation et hygiène)

12. Harmoniser la bande d'établissement (2 % ou 5 %, paramétrable et affichée).
13. Corriger la documentation : `reglage-guide.md` (optimisation PID, filtre, erreurs-types) et `PID-06A` (statut périmé). Revoir l'attribution IMC de `imcPublished` (troncature en PI = choix de MKR) et la puce mal attribuée du docstring de `recommend.py:15`.
14. Supprimer le code mort `_W` et l'expression sans effet `recommend.py:620`. Signaler la troncature de l'horizon (`:581-582`).
15. Renommer ou compléter `test_pid_noise_qualification` (oracle réellement indépendant, par exemple `scipy.signal.lfilter` du filtre dérivé). Étoffer `test_strategy_selection` et `test_tuning_comparison` : front à trois candidats ou plus, critères d'effort, P avec erreur statique, scénarios différents.
16. Ajouter un test de « pire cas aux coins » (grille intérieure de la boîte, FOPDT et SOPDT) pour tenir l'hypothèse sous surveillance. Corriger l'annonce « ~95 % » de la boîte jointe.
17. Donner des intervalles de confiance aux taux de fausses alarmes Monte-Carlo (`model_drift`). Justifier ou paramétrer `RELATIVE_CHANGE`, `EXCITATION_NOISE_RATIO` et `MAXIMUM_RELATIVE_DISPERSION`.
18. Générer les seuils d'objectif dans une source unique partagée par le C# et Python.
19. Ajouter un oracle tiers pour les marges : `python-control` n'est pas installé dans l'environnement. Ce serait utile pour la non-dépendance à scipy seul, sans être indispensable.

---

## Annexe : scripts de contre-vérification (hors dépôt)

Dossier : `/tmp/claude-0/-home-user/befb72bf-6a2a-5d2c-8dbc-fe3eda347b6f/scratchpad/lot0/scripts/`

- `check1.py` : pire cas aux coins contre une grille intérieure de la boîte ; règle de la moitié contre le SOPDT vrai ; sensibilité à τ2.
  - Résultat sur τ2 : le PID SOPDT, déclaré à Ms 1,619 au pire cas, donne 1,674 si τ2 vaut −30 %. La limite de 1,7 est encore tenue, mais le pire cas annoncé est dépassé.
- `check2.py` : précision de `loop_margins` contre `brentq` ; exactitude de `plant_step`.
- `check3.py` : Ms de la boucle échantillonnée exacte (en z) contre le Ms annoncé avec h/2.
