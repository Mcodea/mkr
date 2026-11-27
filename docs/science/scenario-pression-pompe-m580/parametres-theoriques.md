# Paramètres théoriques de référence — pression par pompe centrifuge à variateur

> **DONNÉES SYNTHÉTIQUES.** Tous les nombres de ce document sortent de la simulation `tests/reference/scenario_pression_pompe/generate.py` (v1.0.0). Aucun n'est une mesure industrielle. Les valeurs exactes, avec toutes leurs décimales, sont dans `tests/reference/scenario_pression_pompe/data/parametres_theoriques.json`.

Statut : **référence de scénario, vérifiée numériquement.** Ce document n'est ni une validation métier ni une qualification.

## 1. Procédé modélisé

Une pompe centrifuge à vitesse variable refoule dans un collecteur équipé d'un réservoir hydropneumatique. Les consommateurs en aval sont représentés par une vanne équivalente.

| Grandeur | Symbole | Unité | Rôle |
|---|---|---|---|
| Consigne de vitesse envoyée au variateur | MV | % de N0 (100 % = 50 Hz) | sortie du régulateur |
| Pression relative du collecteur, mesurée | PV | bar (relatif) | mesure |
| Consigne de pression | SP | bar (relatif) | consigne |
| Vitesse relative | n = N/N0 | – | état |
| Débit de la pompe | Q | m³/h | état |
| Charge du collecteur | h | m de colonne d'eau | état (p = ρ·g·h) |
| Ouverture équivalente de la vanne aval | u_v | – (0 à 1) | perturbation |

### Équations (unités du code)

1. **Variateur.** La consigne MV, bornée par LSP = 30 % et HSP = 100 %, arrive après une période de transmission (Ts = 0,1 s). Elle passe ensuite par une rampe ACC = DEC de 10 %/s. La vitesse réelle suit cette rampe avec un retard du premier ordre de τ_m = 0,5 s.
2. **Pompe.** H_p = H0·n² − a·Q², avec H0 = 72 m et a = (H0 − H_BEP)/Q_BEP² = 0,0547 m/(m³/h)². Ce modèle respecte les lois de similitude Q ∝ n et H ∝ n² (voir le README, réf. [G], [SM]).
3. **Conduite.** (l/(g·A))·dQ/dt = H_p − r·Q² − h, avec l = 30 m, DN65 et r = 3 m à 16 m³/h. Un clapet anti-retour impose Q ≥ 0.
4. **Collecteur.** C_h(h)·dh/dt = (Q − Q_d)/3600. Le réservoir contient V_ref = 50 L de gaz à 4 bar. Sa capacité vaut C_h = ρ·g·V_gaz/(κ·p_abs), avec V_gaz = V_ref·(p_ref,abs/p_abs)^(1/κ) et κ = 1. L'eau des conduites ajoute A·l·g/a², où a = 1389 m/s est la célérité donnée par la formule de Korteweg pour un acier de 5 mm d'épaisseur. Cet apport représente 0,07 % du total.
5. **Demande.** Q_d = Cq·u_v·√h, avec Cq = 4,056 (m³/h)/√m, calibré pour obtenir 4,0 bar à 85 % et u_v = 0,5.
6. **Capteur.** Transmetteur de 0 à 10 bar avec un filtre du premier ordre τ_s = 0,2 s. S'y ajoutent un bruit blanc gaussien σ, une quantification de 0,001 bar et un écrêtage à l'étendue. L'échantillonnage se fait à Ts = 0,1 s, avec un bloqueur d'ordre zéro sur MV.

L'intégration se fait par RK4 à pas fixe de 5 ms. Les conditions initiales sont l'**équilibre exact** au point de départ, obtenu par la méthode de Brent. La PV est donc plate avant l'échelon, au bruit près.

### Domaine de validité, contrôlé par le code

- La pression doit rester au-dessus de la pression de prégonflage (1,0 bar). En dessous, le réservoir est vide et la capacité chute d'un facteur d'environ 1000. Le générateur lève alors `scenario_outside_validity` au lieu de produire des données fausses.
- Le modèle est à paramètres localisés : il ne décrit pas le coup de bélier. Ce choix est justifié parce que le temps d'aller-retour de l'onde, 2l/a ≈ 0,04 s, est bien inférieur à Ts et aux constantes de temps retenues.
- Hors du modèle : cavitation et NPSH (aspiration supposée à 0 bar relatif sans contrainte), rendement, échauffement, limitation de courant du variateur, hauteur statique en aval (nulle), plusieurs pompes en parallèle.
- La pression maximale atteignable à 100 % avec u_v = 0,5 est de **5,536 bar**.

## 2. Linéarisation au point nominal

Point nominal : MV = 85 %, u_v = 0,5, p = 4,000 bar, Q = 12,96 m³/h.

**Gain statique.** On linéarise l'égalité Q_p(n, h) = Q_d(h) :

K = (∂Q_p/∂n) / (∂Q_d/∂h − ∂Q_p/∂h)

avec ∂Q_p/∂n = H0·n/((a+r)·Q), ∂Q_p/∂h = −1/(2(a+r)Q) et ∂Q_d/∂h = Q/(2h).

Sans hauteur statique, p est proportionnel à n² à ouverture de vanne fixe. La formule se réduit alors à **K = 2p/n = 2 × 4,0/85 = 0,0941 bar/%**. Le jacobien numérique donne le même résultat (0,094118) : c'est un **invariant analytique**, qui sert d'oracle indépendant.

**Constante de temps dominante.** τ ≈ C_h/G, avec C_h = 9,77·10⁻⁴ m³/m et G = ∂Q_d/∂h − ∂Q_p/∂h = 0,739 (m³/h)/m. On obtient τ = **4,76 s**.

Les valeurs propres du jacobien du système d'ordre 4 (n, Q, h, capteur) sont toutes réelles. Elles correspondent aux constantes de temps **4,63 s, 0,50 s, 0,20 s et 0,15 s**. La valeur de 0,15 s vient du couplage entre l'inertie de la conduite et la conductance.

## 3. Temps morts équivalents

| Contribution | Valeur | Nature |
|---|---|---|
| Transmission de la consigne au variateur | 0,10 s | retard pur (hypothèse : 1 cycle) |
| Bloqueur d'ordre zéro et échantillonnage | Ts/2 = 0,05 s | retard équivalent classique |
| Rampe de 10 %/s sur un échelon Δ | Δ/(2·pente) = 0,25 s pour 5 % | **dépend de l'amplitude** (non linéaire) |
| Transport hydraulique (l/a) | 0,02 s | négligé |
| Constantes de temps négligées | voir la règle de la moitié | retard effectif |

**Règle de la moitié** (Skogestad 2003, §2.1, p. 293, éq. (10) et (11)) : on ajoute la moitié de la plus grande constante de temps négligée au retard effectif et l'autre moitié à la plus petite constante conservée.
- **FOPDT** : τ = 4,63 + 0,50/2 = **4,88 s** ; θ = 0,40 + 0,25 + 0,20 + 0,15 = **1,00 s**.
- **SOPDT** : τ1 = **4,63 s** ; τ2 = 0,50 + 0,20/2 = **0,60 s** ; θ = 0,40 + 0,10 + 0,15 = **0,65 s**.

## 4. Paramètres « vrais » retenus pour les jeux de données

Les modèles sont ajustés par moindres carrées sur la réponse simulée **sans bruit ni quantification**, filtre du capteur compris, à un échelon de 85 à 90 %. L'ajustement est fait dans le générateur avec `scipy.optimize.least_squares` et n'utilise pas le code d'ORIAS.

| Modèle | K (bar/%) | τ ou τ1 (s) | τ2 (s) | θ (s) | RMSE (bar) |
|---|---|---|---|---|---|
| FOPDT ajusté | **0,0971** | **4,67** | – | **1,26** | 0,0027 |
| SOPDT ajusté | 0,0969 | 4,36 | 1,03 | 0,44 | 0,00063 |
| Règle de la moitié, FOPDT | 0,0941 (local) | 4,88 | – | 1,00 | – |
| Gain sécant statique, 85 → 90 % | 0,0969 | – | – | – | – |

**Le système est-il FOPDT ou SOPDT ?** La réponse est un SOPDT bien séparé (τ1/τ2 ≈ 4 à 8). Le FOPDT est une approximation acceptable, mais ses résidus sont corrélés : sa RMSE de 0,0027 bar est proche du bruit nominal de 0,003 bar. Il deviendrait nettement SOPDT avec une rampe plus lente, un variateur plus lent ou un capteur plus amorti. Il tendrait vers un premier ordre pur sans retard sans réservoir et avec un capteur rapide, mais le modèle localisé ne vaut alors plus (voir le §1).

**Différence entre les gains.** Le gain local (0,0941) et le gain sécant d'un échelon de +5 % (0,0969) diffèrent de 3 %. C'est la courbure de p ∝ n². Un estimateur exact doit retrouver le gain **sécant** de l'échelon appliqué, pas le gain local.

## 5. Dépendance au point de fonctionnement (non-linéarités)

| MV (%) | p (bar) | K local (bar/%) | τ1 (s) |
|---|---|---|---|
| 55 | 1,675 | 0,0609 | 10,5 |
| 80 | 3,543 | 0,0886 | 5,29 |
| 85 | 4,000 | 0,0941 | 4,63 |
| 90 | 4,484 | 0,0997 | 4,07 |

Le gain croît comme 2p/n. La constante de temps décroît quand p augmente, parce que C_h est proportionnelle à p_abs^(−1−1/κ). Entre 80 et 90 %, K varie de ±6 % et τ de ±13 %. Ces écarts fixent l'ordre de grandeur des tolérances de validation à prévoir au voisinage du point nominal.

Autres paramètres de forme non linéaire :
- κ de 1 à 1,4 change τ d'un facteur pouvant atteindre 1/1,4 ;
- l'ouverture de la vanne modifie G ;
- la saturation HSP intervient au-dessus de 5,54 bar à u_v = 0,5.

## 6. PI de scénario (jeu `cl_pi`)

Règle SIMC (Skogestad 2003, éq. (23)–(24), tableau p. 296) : Kc = τ/(K(τc+θ)) et Ti = min(τ, 4(τc+θ)). Elle est appliquée au FOPDT ajusté avec τc = 2θ. On obtient **Kc = 12,75 %/bar** et **Ti = 4,67 s**.

Le régulateur est un PI idéal discret à Ts = 0,1 s, avec anti-saturation par blocage conditionnel et suivi de la sortie en manuel. C'est une **proposition technique de scénario, pas un réglage d'exploitation**. Ce n'est pas un modèle du PIDFF ni du PID_INT.

## 7. Vérifications numériques réalisées

1. **Reproductibilité.** Deux exécutions donnent les mêmes empreintes `SHA256SUMS`, avec `--check` puis avec une régénération sur place.
2. **Oracle analytique.** Le gain du jacobien est identique à 2p/n à 10⁻¹⁰ près. La constante de temps C_h/G (4,76 s) est cohérente avec la valeur propre dominante (4,63 s), l'écart venant du couplage avec la conduite.
3. **Contrôle externe.** Ce contrôle a été fait avec le moteur ORIAS, qui est l'objet de l'essai et non un oracle. Le script est dans le dossier de travail : il n'a pas été versionné.
   - `ol_nominal` avec `ol_validation`, tolérances MV de 0,01 % et PV de 0,012 bar : K = 0,0971, τ = 4,66 s, θ = 1,25 s, avertissement `correlated_residuals`. C'est l'écart de structure FOPDT/SOPDT attendu.
   - Constat à arbitrer : `ol_disturbance` est **accepté avec un simple avertissement** (K = 0,037, R² = 0,06), alors que l'intention du jeu est un refus.
   - `ol_mv_saturation` et `ol_out_of_domain` ne sont refusés que par `fit_amplitude`, parce qu'ils sont associés à `ol_validation`. Ni la saturation ni la non-linéarité ne sont détectées en tant que telles.
