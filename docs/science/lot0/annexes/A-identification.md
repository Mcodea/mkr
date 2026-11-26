> **Annexe du LOT 0 — rapport d'audit indépendant (sous-agent, lecture seule), révision auditée c7915cd.** Les constats ont été vérifiés par échantillonnage par le responsable du lot (voir 00-synthese-audit.md, §1) ; tout constat non cité dans la synthèse reste à confirmer avant d'être retenu.

# Lot 0 — Audit A : identification des systèmes dynamiques (moteur Python MKR)

Révision auditée : `c7915cd` (branche `claude/science-engine-dev`), dépôt `/home/user/mkr`, en lecture seule. Aucun fichier du dépôt n'a été modifié.
Bibliothèques épinglées (`engine/requirements.txt`) : numpy 2.3.5, scipy 1.17.0.
Méthode : lecture intégrale des 11 modules et de leurs tests, des ADR 0001 à 0008, de `docs/science/*`, `docs/validation/*` et `docs/methode/guide-utilisateur.md` ; contrôle des contrats C# (`src/Mkr.Core/Identification.cs`, `src/Mkr.Infrastructure/PythonDiagnostics.cs`) ; **4 scripts de sonde** écrits hors dépôt (`scratchpad/lot0/probe/p1.py` à `p4.py`), lancés avec `python3 -I`. La suite complète n'a pas été exécutée.

Conventions : **[V]** = fait vérifié dans le code (fichier:ligne) ou mesuré par une sonde ; **[H]** = hypothèse ou jugement de l'auditeur. Les numéros de ligne renvoient au fichier lui-même.

---

## 0. Synthèse

| Module | Rôle réel | Classement honnête |
|---|---|---|
| `fopdt.py` 1.4.0 | Moindres carrés non linéaires bornés, gain profilé, 12 départs | Vérifié indépendamment (oracle analytique, sans bruit) ; erreurs-types **non calibrées pour le gain** (sous-estimées d'un facteur 2, sonde p4) ; non qualifié |
| `sopdt.py` 1.1.0 | Idem, 2 pôles réels ordonnés, 27 départs | Vérifié indépendamment (analytique, sans bruit) ; couverture des erreurs-types testée sur le gain seulement ; non qualifié |
| `integrator.py` 1.1.0 | Rampe retardée, retard profilé exact par intervalles | Vérifié indépendamment (analytique et balayage `lstsq` indépendant) ; non qualifié |
| `broida.py` 1.1.0 | Méthode à deux points 28 %/40 % (pas d'optimiseur) | Testé (oracle analytique, tolérances lâches) ; formules contrôlées par l'auditeur ; non qualifié ; **présenté comme famille de modèles** |
| `arx.py` 1.0.0 | ARX par moindres carrés linéaires et affinage OE (Levenberg-Marquardt), excitation quelconque | Testé (simulateur ZOH écrit dans le test, structure exacte) ; **accepte un intégrateur comme premier ordre** et **accepte des données de boucle fermée déclarées « ouvertes »** (sondes) ; non qualifié |
| `step_finder.py` 1.2.0 | Localisation des échelons isolés, appariement | Testé (enregistrements synthétiques) ; 1 refus sur données réelles (TCLab) |
| `model_comparison.py` 1.1.0 | FOPDT contre SOPDT sur l'essai de validation, test F | Testé |
| `validation_gate.py` 1.1.0 | Porte d'acceptation unique et proposition de critères | Testé |
| `closed_loop.py` 0.1.0 | Méthode à deux étages (FIR consigne→MV, puis FOPDT) avec bootstrap | Testé sur simulations du dépôt ; **expérimental, non exposé** ; non qualifié |
| `mkr_engine.py` 0.10.0 | Contrat IPC JSON stdin/stdout | Testé (contrat) |
| `trial_preview.py` 1.2.0 | Décimation d'affichage qui conserve les extrema | Testé |

**Aucun module n'est « Qualifié dans un domaine ».** La seule confrontation à des mesures réelles (TCLab, `docs/validation/donnees-reelles-tclab.md`) est un **refus**, pas une acceptation validée.

Constats majeurs, établis par sonde (les détails sont au §12) :
1. **Erreur-type du gain FOPDT sous-estimée d'un facteur 2.** Sur 56 tirages Monte Carlo, l'erreur-type publiée vaut 0,49 fois la dispersion réelle. L'incertitude du plateau initial n'est pas propagée (`fopdt.py:198-232`), alors qu'elle l'est pour SOPDT et intégrateur (`model_drift.py:89-93`). Ces erreurs-types alimentent la boîte d'incertitude du réglage guidé (`PythonDiagnostics.cs:326-333`).
2. **L'ARX accepte un procédé intégrateur comme premier ordre.** Résultat : pôle 0,9999975, τ = 809 549 s, K = 20 306, décision `accepted` (sonde p2). Dans ce cas, seul le refus en aval par `recommend` (signe du gain non établi à ±2 σ, sonde p3) empêche un réglage.
3. **La détection de rétroaction annoncée par l'ARX n'existe pas.** La docstring promet « no feedback » (`arx.py:17-18`). Or l'intercorrélation ne teste que les entrées passées (`arx.py:148-153`, `186`), et des données de PI en boucle fermée déclarées « open » sont acceptées (sonde p2).
4. **Broïda est exposé comme famille** `BroidaFOPDT` (`Identification.cs:3`, `validation_gate.py:55`, `ID-07A-broida.md` : « Nouvelle option BroidaFOPDT dans la liste de familles »), et non comme une méthode d'estimation du FOPDT.
5. **Aucun critère AIC, AICc ou BIC** n'existe dans le dépôt (recherche sur `*.py`, `*.md` et `*.cs` : aucune occurrence).
6. **Plusieurs documents ne correspondent plus au code** : versions, seuils 0,3 et 0,5, « pas d'erreurs-types » pour SOPDT et intégrateur, ARX et réglage guidé (§13).

---

## 1. `engine/fopdt.py` (VERSION `1.4.0`, ligne 12)

### 1.1 Ce que fait le code [V]
- **Modèle :** `PV(t) = PV0 + K·ΔMV·(1 − exp(−max(t−θ,0)/τ))`. La base est calculée par `basis` (l. 132-133), avec `expm1`.
- **Lecture d'un essai** (`read_step`, l. 37-93) :
  - l'origine est le premier échantillon où |MV − MV[0]| > 2·tolMV (l. 70) ;
  - `PV0` est la moyenne du plateau avant échelon (l. 82) et `ΔMV` la différence des moyennes des plateaux de MV (l. 76-77) ;
  - les horodatages sont relus en secondes réelles (l. 89).
- **Estimateur** (`estimate`, l. 136-169) : `scipy.optimize.least_squares`, méthode TRF par défaut avec bornes.
  - Paramètres : `log τ` et la fraction de retard `θ/horizon`. Le gain est **profilé analytiquement** (l. 149, projection de moindres carrés).
  - 12 départs déterministes (l. 153-154). On retient le coût minimal (l. 162).
- **Diagnostics** (l. 172-195) : RMSE, MAE, R², RMSE normalisée par l'étendue, autocorrélation des résidus centrés au retard d'un échantillon (l. 192), 500 points de tracé au plus.
- **Erreurs-types** (`conditional_errors`, l. 198-232) :
  - jacobien analytique dans les coordonnées (A normalisé, log τ, θ/τ), puis `s²(JᵀJ)⁻¹` par SVD avec `s² = SSE/(n−3)` (l. 229-230) ;
  - les dérivées ont été contrôlées par l'auditeur [V] : la colonne 2 est ∂/∂log τ et la colonne 3 est ∂/∂(θ/τ). La reconversion d'unités (l. 231-232) est cohérente ;
  - **l'incertitude du plateau PV0 n'est pas incluse.**
- **Validation :** le deuxième essai est prédit avec K, τ et θ estimés, en utilisant son propre `PV0` et son propre `ΔMV` mesurés (l. 264). Il ne contribue à aucune estimation.

### 1.2 Préconditions vérifiées dans le code [V]
- Au moins 4 points avant l'échelon et 12 après (l. 74).
- MV constante à ±tolMV sur chaque plateau. L'amplitude doit dépasser 10·tolMV (l. 78-81). Conséquence : **un seul échelon par fenêtre**.
- PV stable avant l'échelon : max|PV − moyenne| ≤ tolPV (l. 83-84).
- Réponse supérieure à 4·tolPV (l. 85-86).
- Temps strictement croissants (l. 66, 90). Valeurs finies et |x| ≤ 1e100 (l. 32).
- Intervalles de temps disjoints entre les deux essais (l. 243-244). Rapport des amplitudes compris entre 0,5 et 2 (l. 245-246). Tolérances comprises entre 1e-100 et 1e100 (l. 239).
- **La colonne MANUAL n'est pas lue** (contrairement à `step_finder` et `closed_loop`). Si l'utilisateur saisit lui-même les plages, des lignes en automatique peuvent entrer dans l'essai.

### 1.3 Constantes codées en dur
| Ligne | Valeur | Rôle | Justification |
|---|---|---|---|
| 70 | 2·tolMV | détection de l'échelon | arbitraire (doc fopdt-echelon) |
| 74 | 4 / 12 échantillons | longueur minimale | « choix conservateurs documentés pour revue » (fopdt-echelon.md) |
| 78 | 10·tolMV ; 1e-14 relatif | excitation minimale | idem ; le 1e-14 est une garde numérique |
| 85 | 4·tolPV ; 1e-14 ; 1e-100 | réponse minimale | idem |
| 98 | `ROUND_OFF_RELATIVE = 1e-9` | résidu considéré comme arrondi | commentaire l. 96-97 (NC-10) |
| 103 | `LJUNG_Z_99 = 2.576` | borne de blancheur ±2,576/√N | **référencé** : Ljung 1999 (l. 100-102) |
| 116 | `ONSET_UNCERTAINTY_RATIO = 0.1` | avertissement sur le retard | audit NC-03, valeur arbitraire |
| 139-140 | τ ∈ [Ts_méd/4, horizon] ; θ ≤ horizon/2 | bornes | documentées, arbitraires |
| 153-154 | τ0/horizon ∈ {0,05 ; 0,2 ; 0,6} × θ0/horizon ∈ {0 ; 0,08 ; 0,24 ; 0,45} | départs | arbitraires, non testés pour la sensibilité |
| 157 | `max_nfev=150` ; ftol, xtol et gtol = 1e-10 | solveur | arbitraires |
| 164 | 1e-9·horizon, 1e-12 | retard ramené à 0 | garde numérique |
| 185 | 500 | points de tracé | affichage |
| 210 | 1e-12 | résidu nul : pas d'erreur-type publiée | garde numérique |
| 213 | 1e-8·τ | échantillon confondu avec le retard | garde numérique |
| 225 | 1e-6 | conditionnement de la SVD | « garde-fou révisable » (doc) |
| 245 | 0,5 à 2 | rapport des amplitudes | arbitraire (doc) |
| 254 | 1,02 / 0,98 | `parameter_bound` | arbitraire |
| 257 | 4τ | `short_horizon` | règle usuelle, non citée |

### 1.4 Références citées
- « Ljung, System Identification, 2nd ed., 1999, residual analysis » (`fopdt.py:100`).
- `docs/science/fopdt-echelon.md` renvoie à MKR-SPEC-001 §6 et à « SciPy least_squares » (lien vers la documentation SciPy).

### 1.5 Tests (`test_fopdt.py`, `test_windowed_identification.py`, `test_process_diversity.py`)
- **Oracle analytique indépendant [V]** : la réponse exacte est écrite dans le test (`test_fopdt.py:17`). La récupération est exacte à 1e-5 près (l. 34-40). L'étude couvre 3 échelles de temps × 3 rapports θ/τ × 2 signes de gain, avec un bruit de graine 3 (`test_process_diversity.py:43, 53-64`).
- **Erreurs-types** (`test_fopdt.py:69-89`) : l'oracle est une différence finie écrite dans le test. Il **reprend la même hypothèse** (plateau supposé exact). Il vérifie donc l'algèbre, pas la calibration. Graine `random.Random(8321)` (l. 51).
- **Le test l. 124 inscrit le biais du retard** (« the bias the audit demonstrated ») : l'intervalle `delayIntervalSeconds` le couvre.
- **Il n'existe aucun test Monte Carlo de couverture des erreurs-types FOPDT** : `test_standard_errors.py` ne couvre que SOPDT et intégrateur. C'est ce qui a laissé passer le constat 1.
- Classement : **vérifié indépendamment** pour la récupération des paramètres ; **testé, non calibré** pour les erreurs-types.

### 1.6 Refus corrects [V]
`fit_step`, `fit_baseline`, `fit_response`, `fit_time`, `fit_data`, `fit_independence`, `fit_amplitude`, `fit_tolerance`, `fit_mapping`, `fit_window`, `fit_solver` (tests l. 139-153). Côté avertissements : la valeur nulle d'erreur-type est retirée si le conditionnement est mauvais (`ill_conditioned`) ou si les résidus sont corrélés (test l. 94-100).

---

## 2. `engine/sopdt.py` (version littérale `"1.1.0"`, ligne 80)

- **Modèle [V] :** `1 − τ1/(τ1−τ2)·e^{−x/τ1} + τ2/(τ1−τ2)·e^{−x/τ2}`, écrit sous une forme stable `φ(w) = (1−e^{−w})/w` (l. 10-14). La limite du pôle double est exacte. L'auditeur a redérivé l'identité algébrique.
- **Pôles :** réels positifs seulement, donc **aucune réponse oscillante n'est représentable**.
- **Estimation :** `least_squares` borné, gain profilé, 27 départs (l. 37-42), `max_nfev=200`. Paramétrage : τlent ∈ [Ts/4, horizon], τrapide = bas + (lent − bas)·r avec r ∈ [0,1], θ ≤ horizon/2 (l. 26-28, 35).
- **Avertissements :**

  | Avertissement | Ligne | Condition |
  |---|---|---|
  | `parameter_bound` | 60 | 1,02 / 0,98 / 0,49 |
  | `short_horizon` | 63 | moins de 5·τlent |
  | `fast_pole_resolution` | 65 | τrapide < Ts |
  | `close_poles` | 67 | rapport ≥ 0,95 |
  | `correlated_residuals` | 70-71 | — |

- **Erreurs-types :** `model_drift.numerical_errors` (jacobien par différences finies, pas de 0,05·Ts pour le retard et de 1e-4 relatif pour les autres paramètres, `model_drift.py:72`), **avec** le terme de plateau (l. 89-93).
- **Tests :** oracle analytique indépendant sans bruit (`test_sopdt.py:23-27`) et limite du pôle double (l. 46-54). La couverture Monte Carlo (`test_standard_errors.py:42-45`, graines 200 à 211) porte **sur le gain seulement**, avec un seuil de 0,8 sur 12 tirages, alors que 98,8 % sont attendus. τlent, τrapide et θ ne sont pas contrôlés.
- `recommend` signale `second_pole_uncertainty_not_propagated`.
- **Classement :** vérifié indépendamment sans bruit ; incertitude partiellement testée.

## 3. `engine/integrator.py` (version `"1.1.0"`, ligne 72)

- **Modèle [V] :** `PV0 + Ki·ΔMV·max(t−θ,0)`. Le procédé est purement intégrateur, sans constante de temps.
- **Estimation du retard :** profilage exact. Sur chaque intervalle entre instants actifs, on compare les deux bornes et le point stationnaire (l. 9-27). Retard ≤ 0,5·horizon (l. 15).
- **Précondition :** PV stable avant l'échelon (lecteur FOPDT), donc un procédé **à l'équilibre**.
- **Contrôle de la fin de réponse :** sur deux fenêtres de n/8 points (au moins 2, l. 48-53), la pente doit dépasser 4·tolPV dans le sens prédit. Sinon, refus `fit_integrator_tail` pour l'essai d'estimation (l. 56-57), ou avertissement `integrator_validation_tail` pour l'essai de validation (l. 62-63). Ce dernier est bloquant dans la porte (`validation_gate.py:20`).
- **Erreurs-types :** `numerical_errors` sur (gain, retard) avec le terme de plateau. La couverture est testée **sur le gain seulement** (`test_standard_errors.py:38-40`, graines 100 à 124, seuil 0,85).
- **Tests :** oracle analytique et **balayage `lstsq` indépendant** sur 1001 retards (`test_integrator.py:45-53`), un vrai oracle indépendant.
- **Classement :** vérifié indépendamment.

## 4. `engine/broida.py` (VERSION `1.1.0`, ligne 13)

- **Méthode [V] :**
  - `τ = 5,5·(t40 − t28)` et `θ = 2,8·t28 − 1,8·t40` (l. 60-61) ;
  - `K = (PVfinal − PV0)/ΔMV`, où PVfinal est la moyenne des **6 derniers points** (l. 15, 40-41) ;
  - les passages sont interpolés linéairement entre échantillons réels (l. 18-33) ;
  - aucune optimisation. C'est bien une **méthode d'estimation des paramètres d'un FOPDT**.
- **Contrôle analytique de l'auditeur :** sur un FOPDT exact, t28 = θ + 0,3285·τ et t40 = θ + 0,5108·τ, d'où τ = 5,485·Δ et θ = 2,802·t28 − 1,802·t40. Les constantes publiées sont donc arrondies. Mesure (sonde p1, Ts = 0,1 s, τ = 10 s) : **biais de +0,27 % sur τ et de +0,0034 s sur θ**, indépendant de θ. Ce biais est conforme à la doc (« version classique arrondie »).
- **Refus [V] :**

  | Code | Ligne | Motif |
  |---|---|---|
  | `broida_horizon` | 38, 69 | moins de 18 points, ou fin de réponse avant θ + 4τ |
  | `broida_plateau` | 42 | dispersion de la fin de réponse > 2·tolPV |
  | `broida_response` | 45, 67 | variation de PV trop faible |
  | `broida_shape` | 51-55 | réponse non monotone ou dépassement au-delà de 2·tolPV |
  | `broida_crossing` | 22-33, 58 | passages absents ou mal ordonnés |
  | `broida_domain` | 64 | θ < 0, jamais ramené de force à 0 |

  Un procédé oscillant est correctement refusé (`broida_shape`, sonde p1).
- **Pas d'erreurs-types, par choix documenté** (`ID-07A-broida.md`). Le champ `delayUpperSeconds` contient l'horizon de l'essai (l. 87), ce qui est une **surcharge de sens** (documentée).
- **Références citées (copie exacte, `docs/science/ID-07A-broida.md`) :**
  - « Étude académique « Teaching experience for process identification using first-order-plus-time-delay models », tableau des méthodes à deux points (Broïda : seuils 28 %/40 %, temps et retard) : https://onlinelibrary.wiley.com/doi/full/10.1002/cae.22794 »
  - « L. Kamenova, publication universitaire présentant la relation de Broïda et ses approximations : https://ecad.tu-sofia.bg/et/2004/Papers/Circuits%20and%20Systems/Paper-L_Kamenova.pdf »
  - « Sujet de BTS CIRA, épreuve d'instrumentation, rappels de la méthode (28 % et 40 %) : https://sites.ac-nancy-metz.fr/physique/SiteAcademiqueOld/ancien_site/PHYS/Bts-Cira/annales/2006/U41-2006-instrum.pdf »

  Aucune référence n'est citée dans le code lui-même.
- **Tests (`test_broida.py`) :** oracle analytique FOPDT (repris de `test_fopdt.source`), mais les tolérances sont **lâches** : τ ± 0,25 sur 4 (6 %), θ ± 0,15 (l. 33-34). Elles ne détecteraient pas une erreur de constante de l'ordre de 2 %, alors que le biais attendu est connu (0,27 %). L'étanchéité de la validation est vérifiée (l. 44-49). Les refus sont testés.
- **Présentation :** l'enum C# `ModelFamily { FOPDT, SOPDT, Integrateur, BroidaFOPDT }` (`src/Mkr.Core/Identification.cs:3`), la liste `FAMILIES` (`validation_gate.py:55`) et le libellé affiché `study.Family.ToString()` (`IdentificationView.xaml.cs:462`) **font de Broïda une famille de modèles**. C'est **non conforme** à l'exigence : Broïda doit être une méthode d'estimation du modèle FOPDT. Le contrat C# code aussi en dur `TerminalSamples == 6` et les fractions 0,28/0,40 (`PythonDiagnostics.cs:250-256`), ce qui crée un couplage fragile.
- **Classement :** testé ; formules contrôlées par l'auditeur ; non qualifié.

## 5. `engine/arx.py` (VERSION `1.0.0`, ligne 35)

### 5.1 Ce que fait le code [V]
- **Modèle :** `y(k) = −Σaᵢ y(k−i) + Σbⱼ u(k−nk−j+1) + d + e(k)` (l. 3-4). Le terme d absorbe le point de fonctionnement : **aucun régime permanent initial n'est exigé.**
- **ARX :** moindres carrés par `lstsq` sur des colonnes normalisées. Refus si le rapport des valeurs singulières est ≤ 1e-9 (l. 89-104). Covariance `σ²(ΦᵀΦ)⁻¹` avec `σ² = RSS/(N−p)`.
- **OE :** `least_squares(method="lm")` sur l'erreur de simulation libre, initialisée par l'ARX (l. 127-145). Covariance `σ²(JᵀJ)⁻¹`. La simulation passe par `lfilter`/`lfiltic`, amorcée sur les premières mesures et centrée sur l'équilibre `d/(1+Σa)` (l. 107-124). Pénalité de 1e6 si la simulation diverge.
- **Recherche :** ordres (1,1) et (2,2) seulement (l. 37). nk de 1 à 60 (l. 36). On garde les 3 meilleurs nk de l'apprentissage (une étape) et leurs voisins (l. 234-242).
- **Acceptation sur la série de validation** (`evaluate`, l. 160-191) :
  1. pôles de module < 1 ;
  2. RMSE de simulation ≤ `maximumRmse` déclarée ;
  3. |r1| ≤ 2,576/√N ;
  4. max|corr(e(k+ℓ), u(k))| pour ℓ = 0…20 ≤ z(0,005/21)/√N, avec correction de Bonferroni.
- **Sélection :** le moins de paramètres l'emporte, puis la RMSE de validation la plus faible (l. 265-268). Un OE qui passe la porte est préféré à l'ARX (l. 257-258).
- **Équivalent du premier ordre (1,1)** (l. 194-213) : p = −a1, τ = −Ts/ln p, K = b1/(1−p), θ = (nk−1)·Ts, erreurs-types par la méthode delta.
  - L'auditeur a refait la dérivation [V] : dτ/da1 = −Ts/(p ln²p), dK/da1 = −b1/(1+a1)², dK/db1 = 1/(1+a1). Elle est **correcte**.
  - Les lignes 203 et 205 sont du **code mort** : des affectations écrasées aussitôt (l. 204 et 207). C'est un risque de régression, sans effet sur le résultat actuel.
  - **Le moteur publie `delaySeconds: 0.0` comme erreur-type du retard** (l. 213). Le pont C# la remplace par Ts/2 et utilise nk·Ts (ADR 0008, `PythonDiagnostics.cs:392`). Tout autre consommateur du moteur hérite d'une certitude fausse.

### 5.2 Constantes
| Ligne | Valeur | Rôle | Justification |
|---|---|---|---|
| 36 | `MAXIMUM_DELAY_SAMPLES = 60` | retard maximal | doc arx.md (« jusqu'à 60 pas »), arbitraire |
| 37 | ordres (1,1), (2,2) | structures | documenté |
| 38 | `UNIFORM_SAMPLING_TOLERANCE = 1e-3` | gigue relative tolérée | commentaire, arbitraire |
| 39 | `MINIMUM_SAMPLES_PER_PARAMETER = 10` | données minimales | arbitraire |
| 67 | 40 échantillons | longueur minimale | arbitraire |
| 74 | 10·tolMV | excitation minimale | arbitraire |
| 95 | 1e-9 | conditionnement | garde numérique |
| 113 ; 122 | 1e-12 ; 1e150 | divergence | garde numérique |
| 131 | 1e6 | pénalité OE | arbitraire |
| 133 | xtol et ftol 1e-12 ; `max_nfev` = 400·p | solveur | arbitraire |
| 178 | 2,576/√N | blancheur | Ljung 1999 |
| 185-188 | 21 retards, Bonferroni 0,005/21 | intercorrélation | justifié en commentaire (l. 187) |
| 236 | 20 + nk + nb | longueur minimale de validation | arbitraire |
| 271 | 80 | candidats publiés | affichage |

### 5.3 Références citées
« Ljung, System Identification: Theory for the User, 2nd ed., 1999, ch. 4 and 10 » ; « (Ljung 1999, ch. 7) » ; « (Ljung 1999, ch. 7 and 16) » (`arx.py:3, 6, 9`).

### 5.4 Tests (`test_arx.py`)
- L'oracle est un **simulateur ZOH écrit dans le test** (l. 25-42). Il est indépendant du module, mais **structurellement identique** au modèle estimé. Le bruit est un bruit de sortie blanc. Les graines sont documentées (0 à 5, 11, 5, 7, 4, 9).
- La récupération est vérifiée depuis un départ hors équilibre (y0 = 5, point de fonctionnement 50) : gain à 1 %, τ à 3 %, retard exact (l. 50-72). La couverture à 3σ est exigée sur au moins 5 tirages sur 6.
- Un second ordre est accepté en (2,2) et refusé en (1,1) (l. 74-84).
- Faiblesse : `test_an_uninformative_validation_series_does_not_validate_anything` (l. 184-192) obtient le refus par la **seule RMSE** (bruit 0,5 pour une limite de 0,05). Il ne prouve pas qu'une validation peu informative mais propre serait refusée.

### 5.5 Écarts trouvés par sonde
- **Intégrateur accepté** (sonde p2) : rampe Ki = 0,05, retard de 3 pas, bruit 0,02. Décision `accepted`, OE (1,1), pôle 0,999997529, **τ = 809 549 s** (environ 500 fois l'horizon), **K = 20 306 ± 55 636**. Aucune règle ne refuse un pôle quasi unitaire, un τ très supérieur à l'horizon ou une erreur-type relative supérieure à 1. La doc dit seulement « Pas d'intégrateur » (arx.md, Limites). Le réglage guidé (ADR 0008) prend une étude ARX acceptée comme porte. Dans ce cas, `recommend` refuse (`parameter_uncertainty_excludes_the_model`, sonde p3) **grâce au signe** du gain ; un cas moins bruité pourrait passer [H].
- **Boucle fermée déclarée ouverte, acceptée** (sonde p2) : PI sur FOPDT, perturbation colorée, `loop="open"`. Résultat `accepted`, ARX (2,2), nk = 2 (vrai nk = 3), pôle parasite −0,44. La détection de rétroaction promise par la docstring (« no unmodelled dynamics, no feedback », l. 17-18) **n'est pas réalisée** : `lag_correlation(e, u, ℓ)` (l. 148-153) corrèle e(k+ℓ) avec u(k) pour ℓ ≥ 0, donc avec les entrées **passées** seulement. La rétroaction se voit sur les retards négatifs, où u suit e. Le seul garde-fou est la déclaration de l'utilisateur. L'ARX **ignore aussi la colonne MANUAL**.
- **Retard de 80 pas** : refusé (correct), mais avec les motifs `validation_residuals_not_white` et `validation_simulation_rmse`. Aucun motif explicite ne dit « retard hors du domaine de recherche ».
- **Fuite légère de la validation vers la sélection [V] :** la série de validation choisit l'estimateur (OE ou ARX), nk parmi les candidats retenus et l'ordre (à égalité de paramètres). La RMSE de validation publiée pour le modèle retenu est donc **optimiste**, et il n'existe pas de troisième jeu de test.
- **Classement :** testé sur données synthétiques structurellement exactes ; **non qualifié** ; refus incomplets (intégrateur, rétroaction).

## 6. `engine/step_finder.py` (VERSION `1.2.0`, ligne 23)

- **Fonctionnement [V] :** un événement est un saut de MV supérieur à 2·tolMV entre deux lignes valides (l. 90).
  - Une rampe ou un changement sur plusieurs échantillons est rejeté (l. 102).
  - La ligne de base est la plus longue au plus près de l'échelon dont la PV tient dans tolPV, à 60 points au plus (l. 110-116). Elle doit compter au moins 4 points (l. 118). La réponse doit compter au moins 12 points (l. 121).
  - Chaque fenêtre est revalidée par `fopdt.read_step` (l. 136).
  - Si la colonne MANUAL existe, une ligne où MANUAL ≠ 1 est invalide (l. 64-67, 86-87).
  - Le plateau partagé entre deux échelons est coupé à 8 points au plus (l. 29, 162-164).
  - Les essais sont appariés avec un rapport d'amplitudes de 0,5 à 2 (l. 28), le sens opposé et les réponses stabilisées en premier.
- **Indicateurs :** `settled` si la dispersion de max(6, n/8) points de fin ≤ 2·tolPV (l. 137-139) ; `low_signal_to_noise` si la réponse < 5·σ du plateau (l. 141).
- **Constantes :** l. 24-29 ainsi que 90, 118, 121, 137, 139 et 141. Elles sont **arbitraires**, avec une justification partielle dans l'ADR 0004.
- **Tests (`test_step_finder.py`) :** oracle de superposition d'échelons FOPDT écrit dans le test (l. 14-30, graine 1 et paramètres). Rampe, double échelon, dérive, trous, mode non manuel et transfert automatique→manuel sont couverts (l. 46-178). Réel : les 4 échelons TCLab sont rejetés `baseline_too_short_or_unstable` (`test_real_tclab_dynamic.py:37`).
- **Écart :** la recherche n'exploite que des échelons isolés depuis un plateau stable. Un historique **à plusieurs variations sans plateau** ne donne aucun essai. C'est le comportement attendu, par refus explicite.
- **Classement :** testé.

## 7. `engine/model_comparison.py` (version `"1.1.0"`, ligne 32) et `engine/validation_gate.py` (VERSION `1.1.0`, ligne 12)

- **`compare` [V] :** il ne compare que **FOPDT et SOPDT** (l. 16). L'intégrateur et Broïda sont exclus : la doc conseille de comparer avec l'intégrateur pour une dynamique lente, mais rien ne l'automatise. On retient l'ordre minimal accepté, sauf si le SOPDT passe un test F à 99 % sur le rapport des MSE de validation avec F(n,n) (`validation_gate.py:91-103`).
  - Le test F suppose des résidus **indépendants**.
  - Les deux erreurs portent sur les **mêmes échantillons**. Leur corrélation positive rend le test conservateur. Mais l'autocorrélation des résidus réduit les degrés de liberté effectifs, ce qui le rend **anti-conservateur**.
  - Le qualificatif « approximation prudente » (fopdt-echelon.md) n'est donc **pas démontré** [H].
- **`reasons` [V] :** RMSE de validation supérieure au maximum ; |r1| de validation supérieur au maximum, sauf résidu d'arrondi ; avertissements bloquants `{parameter_bound, short_horizon, fast_pole_resolution, ill_conditioned, integrator_validation_tail}` (l. 18-21). `correlated_residuals` (apprentissage), `close_poles`, `delay_resolution` et `delay_onset_uncertainty` **ne sont pas bloquants**.
- **`suggest` [V] :** σ est la moyenne quadratique des écarts-types des plateaux des deux essais. `maximumRmse = max(2σ, tolPV/2)` (l. 80-82) et `maximumResidualLagOne = 2,576/√N` (l. 84). Les critères sont fixés **avant l'ajustement**, ce qui est sain.
- **Constantes :** `NOISE_MULTIPLE = 2.0` (l. 15, signalé ouvert par l'audit F-02) ; `SIGNIFICANCE = 0.99` (l. 16) ; limites 0 < x ≤ 1e100 et lag ≤ 1 (l. 28-31).
- **Tests (`test_validation_gate.py`, `test_model_comparison.py`) :** oracles analytiques (FOPDT et SOPDT écrits dans les tests) ; graines `seed + day` (l. 87-89) et 0 à 9 (l. 134). Le cas NC-04 est couvert : un SOPDT vrai et des seuils lâches mènent au SOPDT retenu. Le cas inverse (10 tirages) retient le FOPDT.
- **Classement :** testé. Aucun taux de fausses acceptations n'est mesuré sur données réelles.

## 8. `engine/closed_loop.py` (VERSION `0.1.0`, ligne 22) — expérimental (ADR 0006)

### 8.1 Ce que fait le code [V]
- **Étage 1 :** régression FIR de MV sur la consigne, avec `taps = min(N/3, 150)` (l. 160) et une régularisation de 1e-9·trace (l. 166). La consigne est prise depuis le début du fichier, avec répétition de la première valeur (l. 115-118).
- **Étage 2 :** FOPDT discret `y(k) = a·y(k−1) + (1−a)·K·u_θ(k−1)`. Le retard fractionnaire est obtenu par interpolation linéaire de u (l. 88-112). `least_squares` est borné : K ∈ ±1e6, τ ∈ [Ts/2, 100·durée], θ ≤ durée/2. Six départs.
- **Conditions initiales :** les écarts sont pris par rapport à la moyenne des **8 premières lignes** de chaque fenêtre (l. 155-157), et le filtre part d'un état nul. **Le régime permanent est donc supposé au début de la fenêtre** [V], contrairement à l'ARX.
- **Dispersion :** bootstrap par blocs de 30 tirages, blocs de max(10, N/20), **graine fixe 12345** (l. 178-192). On prend ensuite le maximum avec |p_estimation − p_validation|/√2 (l. 208-209). La dispersion obtenue est **sous-estimée d'environ 30 %**, selon la mesure documentée dans l'ADR 0006, et le test n'exige qu'un rapport entre 0,5 et 2 (`test_closed_loop.py:102-112`).
- **Acceptation :** RMSE de validation ≤ max(2σ, tolPV/2), calculée en interne (l. 217) ; dispersion relative du gain ≤ 30 % et de τ ≤ 50 % (l. 30, 222-225). L'autocorrélation |r1| > 0,5 donne un **simple avertissement** (l. 31, 227-229). Ce seuil 0,5 est **fixe**, alors que l'audit F-02 l'a remplacé ailleurs par la borne de Ljung.

### 8.2 Écarts avec l'ADR 0006 [V]
- L'ADR annonce la « même porte de validation que la boucle ouverte ». Le code ne passe pas par `validation_gate` et n'a pas de critère de blancheur.
- Il annonce aussi un modèle « FOPDT, SOPDT ou intégrateur ». Le code ne traite que le **FOPDT**.
- Il annonce que les « ajustements manuels de MV » sont refusés. Seule la colonne MANUAL est contrôlée (l. 144) ; rien ne détecte une MV modifiée par une anticipation, une cascade ou un opérateur.
- La fenêtre de validation sert **aussi à une seconde estimation** (critère de dispersion). Il n'y a pas de fuite sur les paramètres retenus, mais cette fenêtre joue deux rôles.

### 8.3 Constantes
L. 23-31 : `BASELINE_ROWS = 8`, `MAXIMUM_TAPS = 150`, `EXCITATION_NOISE_RATIO = 20` (ouvert selon F-02), `SATURATION_MARGIN = 0.005`, `UNIFORMITY = 0.01`, `BOOTSTRAP = 30`, `BLOCK_FRACTION = 20`, dispersion maximale 0,3 et 0,5, `RESIDUAL_LAG_ONE = 0.5`. S'y ajoutent les départs (l. 101-102), les bornes (l. 105), la régularisation 1e-9 (l. 166), le seuil `taps < 5` (l. 161), l'estimateur du bruit `std(diff)/√2` (l. 128) et `short_horizon` si τ > durée/4 (l. 230). **Toutes sont arbitraires** ; seule l'ADR en discute quelques-unes.

### 8.4 Référence citée
« Van den Hof and Schrama, Automatica 1993 » (l. 4) ; « Van den Hof & Schrama, 1993 » (ADR 0006).

### 8.5 Tests et statut
- **Tests :** le simulateur de boucle fermée est **écrit dans le test** (PI idéal, FOPDT ZOH, perturbation AR(1) de coefficient 0,98, `test_closed_loop.py:11-40`). Graines 1, 0 à 5 et 0 à 9.
- Faiblesses : `test_a_strong_unmeasured_disturbance_is_not_accepted_as_a_good_model` (l. 94-100) **passe par un `return`** dès qu'une `ValueError` est levée, quelle qu'en soit la cause. `test_without_the_mode_column_nothing_is_assumed` (l. 136-138) ne vérifie que la présence d'une clé.
- **Statut :** non branché à l'interface (aucun appel C# à `identify_closed_loop`). **Classement :** testé sur simulations du dépôt, expérimental, non qualifié. Aucune donnée réelle (exigence 4 de l'ADR 0006 non remplie).

## 9. `engine/mkr_engine.py` (`ENGINE_VERSION = "0.10.0"`, ligne 10)

- **Contrat [V] :**
  - JSON UTF-8 sans clé dupliquée (l. 14-20). Taille ≤ `MAX_REQUEST_BYTES = 2_900_000` (l. 11), une limite **non justifiée** qui borne aussi la longueur des historiques importés.
  - `requestId` conforme à `[A-Za-z0-9_-]{1,64}` (l. 33). Clés exactes `{version, requestId, operation, payload}`, version 1.
  - Une exception non prévue devient `engine_failure`, une valeur non finie devient `engine_non_finite` (l. 201-212).
- **Correspondance des codes d'erreur :**
  - `identify_*`, `compare_models`, `assess_model`, `suggest_criteria` et `find_steps` : une erreur sans préfixe `fit_`, `csv_` ou `broida_` devient `fit_data` (l. 137). C'est sain.
  - `identify_arx` : sans préfixe `arx_`, l'erreur devient `fit_data` (l. 71).
  - **`identify_closed_loop` renvoie `str(error)` brut** (l. 89). Toute `ValueError` interne non prévue (numpy, scipy) part comme « code » avec un texte libre. C'est un écart de contrat, sans effet tant que le module n'est pas exposé.
- **Tests (`test_mkr_engine.py`) :** aller-retour en sous-processus, JSON invalide, version, opération inconnue, exception, valeur non finie. **Testé.**

## 10. `engine/trial_preview.py` (VERSION `1.2.0`, ligne 8)

Décimation pour l'affichage seulement : 60 paquets de 10 indices au plus, `MAX_PLOT_POINTS = 600` (l. 9, 25). Elle conserve les bornes, les extrema de MV et de PV et le plus grand saut (l. 13-41). Aucune donnée n'est interpolée. Refus : `preview_mapping`, `preview_time`, `preview_data`, `csv_encoding_or_syntax`. Tests déterministes ; l'oracle est construit à la main (pic isolé, bornes d'échelon). **Testé.** Ce module n'intervient pas dans l'estimation.

---

## 11. Écarts par exigence du cahier des charges

| Exigence | État [V], sauf mention [H] |
|---|---|
| Hors régime permanent, CI non stabilisées | **ARX seulement** (terme d, simulation amorcée par les mesures, test l. 50-72). FOPDT, SOPDT, intégrateur et Broïda exigent un plateau PV dans `tolPV`, une tolérance **déclarée par l'utilisateur**, sans test de pente. Sonde p1 : une relaxation avant l'échelon, admise par une tolérance de 0,2, donne **K = 1,333 pour 1,5 (−11 %) et θ = 3,7 s pour 3 s**. Le seul signal est `correlated_residuals`, avec erreurs-types retirées. La boucle fermée suppose aussi le régime permanent (8 premières lignes). |
| Procédés intégrateurs | Échelon : intégrateur pur avec retard, à l'équilibre avant l'échelon ; pas de modèle intégrateur avec constante de temps. ARX : **accepté à tort comme premier ordre** (§5.5). Boucle fermée : absent. |
| Procédés oscillants | Aucun modèle sous-amorti continu. SOPDT à pôles réels. L'ARX (2,2) admet des pôles complexes, mais sans équivalent continu ni réglage. Sonde p1 : FOPDT et SOPDT sont refusés par la porte (`validation_rmse`, `validation_residuals`), Broïda par `broida_shape`. Le refus est correct, mais **aucun motif ne nomme l'oscillation**. |
| Grand temps mort | FOPDT correct jusqu'à θ/τ = 7 (sonde p1 : θ = 70 s, τ = 10 s retrouvés) à condition que θ ≤ horizon/2. ARX limité à 60 pas, refus sans motif explicite. |
| Historiques à plusieurs variations | Échelons : une variation par fenêtre (`fopdt.py:78-81`). ARX : oui, avec excitation quelconque. Réel TCLab : refus justifié. |
| Fonctionnement normal (données d'exploitation) | Non couvert. Le guide le dit (« données d'exploitation « sales » », guide l. 62). |
| Boucle fermée (biais de rétroaction, ADR 0006) | Module expérimental non exposé : dispersion sous-estimée, porte différente de l'ADR, FOPDT seul. Ailleurs, la boucle fermée n'est exclue que **par déclaration** (ARX) ou par la colonne MANUAL (`step_finder`). L'ARX **ne détecte pas** la rétroaction (sonde p2). FOPDT et ARX ignorent MANUAL. |
| Incertitudes (erreurs-types) | FOPDT : **gain sous-estimé ×0,49** (sonde p4), τ correct (×1,16). SOPDT et intégrateur : calibrés sur le gain seulement. Broïda : aucune (documenté). ARX : méthode delta, retard à 0 dans le moteur. Boucle fermée : sous-estimée de 30 %. |
| Identifiabilité | Seuil de conditionnement SVD de 1e-6 (FOPDT et numérique), 1e-9 (ARX) : l'erreur-type est retirée, mais **aucune matrice de corrélation des paramètres** n'est publiée. ARX : pas de détection d'un pôle quasi unitaire ni d'un τ supérieur à l'horizon. |
| Sensibilité aux valeurs initiales | Grilles fixes de 12, 27 et 6 départs. Seul le meilleur coût est gardé ; ni l'écart entre optima locaux ni leur nombre n'est publié. Aucun test de robustesse à la grille. |
| Séparation estimation / validation sans fuite | Échelons : étanche, invariance testée (FOPDT l. 42-47, SOPDT l. 37-40, Broïda l. 44-49). L'essai de validation fournit son PV0 et son ΔMV, ce qui est légitime et documenté. ARX : la validation sert à la sélection, donc la RMSE publiée est optimiste. Boucle fermée : double rôle de la fenêtre de validation. Même enregistrement : limite reconnue dans l'ADR 0004. |
| Refus de données inadéquates | Riche et explicite pour les échelons, Broïda et la boucle fermée. Lacunes : intégrateur et rétroaction en ARX, plateau non stabilisé admis par une tolérance lâche. |
| AIC / AICc / BIC | **Absents.** La sélection repose sur la porte de validation et le test F. Aucun ADR ne justifie ce choix contre l'exigence. |
| Analyse des résidus | Autocorrélation au retard 1 seulement (pas de test portmanteau ou Ljung–Box multi-retard). ARX : intercorrélation aux retards ≥ 0 seulement. Le test F suppose l'indépendance. |
| Broïda comme méthode d'estimation du FOPDT | Le code calcule bien des paramètres FOPDT, mais le **contrat, l'interface et la doc en font une famille** (`BroidaFOPDT`). **Non conforme.** |

## 12. Sondes de l'auditeur (scripts hors dépôt, reproductibles)

- **p1** :
  - biais de Broïda (+0,27 % sur τ, +0,0034 s sur θ) ;
  - FOPDT avec θ/τ de 3 à 7 retrouvé à 0,3 % près ;
  - sous-amorti refusé par les trois méthodes ;
  - plateau non stabilisé : biais de −11 % sur K, avec avertissement seulement.
- **p2** : ARX avec retard de 80 pas refusé ; intégrateur accepté (τ = 8,1e5 s) ; boucle fermée déclarée ouverte acceptée en ARX (2,2) avec nk décalé.
- **p3** : `recommend` refuse l'équivalent intégrateur de p2 (signe du gain non établi).
- **p4** : Monte Carlo FOPDT sur 60 graines (300 à 359), 56 avec erreurs-types publiées, K = 1,5, τ = 20 s, θ = 4 s, σ = 0,03, 10 points de plateau.

  | Grandeur | Erreur-type publiée | Dispersion réelle | Rapport |
  |---|---|---|---|
  | Gain | 0,00240 | 0,00493 | **0,49** |
  | τ | — | — | 1,16 |
  | Gain, avec le terme de plateau de `model_drift.numerical_errors` | — | — | **1,06** |

  La correction existe donc déjà dans le dépôt.

## 13. Documentation non alignée sur le code [V]

| Document | Ce qu'il dit | Ce que fait le code |
|---|---|---|
| `docs/science/fopdt-echelon.md` | « Méthode 1.1.0 » | `VERSION = "1.4.0"` |
| `docs/science/fopdt-echelon.md` | erreurs-types omises si |r1| > 0,3 | borne de Ljung (`fopdt.py:100-108, 201`) |
| `docs/science/fopdt-echelon.md` | `suggest_criteria` propose |r1| ≤ 0,5 | 2,576/√N (`validation_gate.py:84`) |
| `docs/science/sopdt-echelon.md` | « Méthode 1.0.0 », « ne fournit pas d'erreurs-types SOPDT » | 1.1.0, erreurs-types publiées |
| `docs/science/integrateur-echelon.md` | « Méthode 1.0.0 », « Les erreurs-types de l'intégrateur ne sont pas estimées » | 1.1.0, erreurs-types publiées |
| `docs/science/ID-07A-broida.md` | « algorithme identifié 1.0.0 » | `1.1.0` |
| `docs/science/arx.md` | « Elle n'alimente pas encore le réglage guidé », puis « Ces valeurs alimentent le réglage guidé » (contradiction interne) | ADR 0008 : réglage guidé avec nk·Ts, et non (nk−1)·Ts |
| `docs/validation/etat-de-maturite.md` | ARX : il manque « l'alimentation du réglage guidé » | réalisée par l'ADR 0008 |
| `docs/adr/0004…md` et `guide-utilisateur.md:23` | « ne détecte pas le mode » | `step_finder` exploite la colonne MANUAL |
| `guide-utilisateur.md:77` | erreurs-types « des trois familles » augmentées de l'incertitude du plateau | vrai pour le contrôle de dérive, **faux pour l'identification FOPDT** |
| `docs/adr/0006…md` | porte de validation, familles, refus des ajustements de MV | voir §8.2 |
| `docs/science/audit-moteur-scientifique.md` F-02 | `RESIDUAL_LAG_ONE = 0.5` de la boucle fermée non mentionné | seuil fixe toujours présent |

## 14. Risques scientifiques et de régression

1. Les incertitudes FOPDT trop étroites rendent le réglage guidé trop optimiste. Le FOPDT est la famille la plus utilisée [H].
2. Une étude ARX « acceptée » à tort et enregistrée (ADR 0007) peut tromper le relecteur. Elle ne serait bloquée que par `recommend`.
3. Une déclaration « open » erronée conduit à une estimation ARX biaisée, sans aucune alerte.
4. Les seuils sont dupliqués entre modules (multiples de tolPV, 0,5 et 2, 4 et 12, 0,49 et 0,98), avec des versions littérales (`sopdt.py:80`, `integrator.py:72`, `model_comparison.py:32`). Les avertissements bloquants sont couplés par des chaînes (`validation_gate.py:18-21`). Le contrat C# code en dur la preuve Broïda (6 points, 0,28/0,40).
5. Le code mort de `arx.py:203-205` invite à une mauvaise correction.
6. Des tests passent à vide ou ne testent pas ce que leur nom annonce : `test_closed_loop.py:94-100` et `136-138`, `test_standard_errors.py:53-57` (« withheld », mais l'assertion accepte None ou une valeur > 0), `test_arx.py:184-192`. Les tolérances de Broïda sont lâches.
7. Le test F « prudent » n'est pas démontré sous résidus corrélés [H].
8. La limite IPC de 2,9 Mo n'est pas documentée et pèse sur les historiques longs.

---

## 15. Consolidations et développements, par priorité

### P0 — bloquant
1. **Erreurs-types FOPDT :** propager l'incertitude du plateau dans `fopdt.conditional_errors`. On peut réutiliser `model_drift.numerical_errors(include_baseline=True)`, déjà calibré. Ajouter un test Monte Carlo de couverture FOPDT (gain, τ, θ) et durcir les tests SOPDT et intégrateur : tous les paramètres, au moins 50 graines, seuil justifié. Corriger `guide-utilisateur.md:77` et `fopdt-echelon.md`.
2. **ARX et intégrateur ou modèle non identifiable :** refuser par un motif explicite un (1,1) dont le pôle est ≥ 1 − ε, ou dont τ dépasse l'horizon de l'essai, ou dont l'erreur-type relative de K ou de τ est trop grande. Ces seuils doivent être fixés et justifiés **avant** les résultats, et testés sur la sonde p2. Raison : l'acceptation ARX est la porte du réglage guidé (ADR 0008).

### P1
3. **Broïda :** en faire une **méthode d'estimation** de la famille FOPDT (contrat de la forme famille = FOPDT, méthode = broida), avec migration de l'enum `BroidaFOPDT` et reprise des libellés et de la doc ID-07A. Le point est bloquant pour la conformité si le cahier des charges l'impose formellement.
4. **ARX et boucle fermée :** retirer la promesse « no feedback » de la docstring, ou ajouter l'intercorrélation aux **retards négatifs**, testée sur la sonde p2. Faire lire la colonne MANUAL par `arx.read_series` et `fopdt.read_step`, avec la même convention que `step_finder`.
5. **ARX, fuite vers la sélection :** sélectionner l'ordre, nk et l'estimateur sur l'apprentissage, avec un critère d'information (voir 8), et réserver la série de validation à la seule acceptation. À défaut, documenter le caractère optimiste de la RMSE publiée. Publier une erreur-type du retard égale à Ts/2 dans le moteur, et non 0.
6. **Plateau non stabilisé (méthodes par échelon) :** ajouter un test de pente ou de dérive du plateau, indépendant de la tolérance déclarée (avertissement bloquant ou refus), et un test reprenant la sonde p1.
7. **Boucle fermée (si elle doit sortir de l'expérimental) :** respecter l'ADR (porte commune, borne de Ljung au lieu de 0,5) ; vérifier la couverture du FIR (taps comparés au temps d'établissement) ; vérifier la blancheur ; fournir des codes d'erreur préfixés dans `mkr_engine.py:89` ; obtenir des données réelles. Sinon, la laisser non exposée, comme aujourd'hui.
8. **AIC, AICc et BIC :** soit les calculer sur les résidus d'apprentissage comme **information de sélection** (ARX, et FOPDT contre SOPDT), à côté de la porte de validation ; soit rédiger un ADR qui justifie leur absence au regard du cahier des charges. Ne pas les substituer à la porte de validation.
9. **Mise à jour documentaire** de tout le §13 (versions, seuils, erreurs-types, ARX et réglage guidé, MANUAL).
10. **Tests à vide** du §14.6 : les rendre effectifs, et resserrer les tolérances de Broïda sur le biais connu (0,27 %).

### P2
11. Analyse des résidus multi-retard (Ljung–Box) en complément du retard 1 ; justifier ou corriger le test F sous autocorrélation.
12. Publier, à partir des candidats déjà calculés, l'écart entre optima locaux (sensibilité aux départs) et la matrice de corrélation des paramètres (identifiabilité).
13. Motifs de refus explicites : réponse oscillante (dépassement ou changement de signe de la dérivée) ; retard ARX au-delà de 60 pas.
14. Hygiène : retirer le code mort de `arx.py:203-205` ; regrouper les constantes et les versions ; documenter `MAX_REQUEST_BYTES`.
15. Qualification : une paire d'échelons réelle acceptée (protocole du kit, TCLab sur H1 seul) ; un enregistrement réel en boucle fermée comparé à la boucle ouverte (ADR 0006, exigence 4).

Hors priorité, à n'entreprendre que sur besoin avéré : modèles sous-amortis continus, intégrateur avec constante de temps, identification sur données d'exploitation. Ce sont des lacunes au regard du cahier des charges, pas des consolidations ; elles n'appellent pas d'algorithme supplémentaire tant que les P0 et P1 ne sont pas soldés.
