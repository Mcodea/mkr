# Audit du moteur scientifique — relevé du 25/11/2026

Périmètre : `engine/*.py` (6 600 lignes, 262 tests exécutés le 25/11/2026, tous réussis) et la chaîne acquisition → identification → validation → réglage → simulation → conversion de bloc. Priorité : exactitude scientifique, pas l'interface.

## 1. Échelle de preuve (pour chaque fonction)
0 non implémentée · 1 implémentée · 2 vérifiée mathématiquement (cas analytique ou implémentation indépendante) · 3 testée sur données synthétiques à paramètres connus · 4 testée sur données expérimentales · 5 validée sur banc · 6 qualifiée pour une configuration industrielle identifiée.
Un test automatisé réussi ne vaut jamais le niveau 5 ou 6. **Aucune fonction n'est au niveau 5 ou 6** : ni banc ni automate réel n'étaient disponibles.

## 2. État par fonction
| Fonction | Niveau | Preuve | Lacune |
|---|---|---|---|
| Qualité des signaux, import CSV | 3 | `test_csv_*`, fixtures, DST | fichiers d'historiens réels |
| Identification FOPDT (moindres carrés à retard profilé) | 3 | récupération des paramètres de 3 échelles de temps × 3 ratios de retard × 2 signes de gain (`test_process_diversity`) ; campagne ci-dessous | données expérimentales industrielles ; 1 identification TCLab réelle (niveau 4, très limité) |
| SOPDT, intégrateur, Broïda | 3 | idem | idem |
| Identification en boucle fermée | 3 (expérimentale) | simulations ; dispersion publiée sous-estimée d'environ 30 % | comparaison à un essai en boucle ouverte réel |
| Porte de validation et sélection de modèle | 3 | critères fixés avant ajustement ; test F sur essai indépendant ; refus testés | seuils : voir §3 |
| Marges de stabilité (gain, phase, retard, Ms) | 2 | formules fermées indépendantes (`test_stability_margins`) | modèle nominal seulement |
| Règles SIMC PI / PID, intégrateur | 2 | formules de Skogestad 2003 recopiées dans les tests, conversion série→idéale vérifiée en fréquence | — |
| Règle « IMC/Lambda » | 2 | formule vérifiée, **mal nommée** (voir F-01) | renommage |
| **Réglage guidé avec incertitude et refus** (`recommend.py`, nouveau) | 2–3 | tests avec simulation fréquentielle et temporelle **écrites dans le test**, pas par le moteur ; campagne de 8 procédés | pas d'interface, pas de banc |
| Simulation PI/PID (saturation, anti-windup, bruit, perturbation, transfert manuel/auto) | 1–2 | cas analytiques partiels | validation contre un simulateur indépendant (voir F-10) |
| Conversions Schneider PIDFF, PID_INT, Siemens PID_Compact | 1 (forme) | équations lues dans les documents officiels, équivalence de forme à un oracle | **essai sur le bloc réel**, jamais qualifié |
| Optimisation numérique sous contraintes | 0 | — | F-05 |
| ARX / identification sans régime permanent initial | 0 | — | F-08 |

## 3. Constats
| # | Gravité | Constat | Décision |
|---|---|---|---|
| F-01 | **élevée** | La règle livrée sous la clé `imc` et le libellé « IMC/Lambda » est Kp = τ/[K(λ+θ)], Ti = τ. C'est la règle **Lambda (Dahlin)** des industriels, pas le PI IMC publié de Rivera, Morari et Skogestad (1986, avec Padé : Kc = (τ+θ/2)/[K(λ+θ/2)], Ti = τ+θ/2). Le libellé attribue à IMC une règle qui n'est pas la sienne. | **Libellé corrigé le 25/11** (interface, fiche PDF, tests) en « Lambda (Dahlin) ». Reste ouvert : ajouter le PI IMC publié comme candidat distinct ; la clé de contrat `imc` est conservée. |
| F-02 | moyenne (**corrigé en partie**) | Seuils sans justification complète : résidu retard 1 maximal par défaut **0,5** (`DEFAULT_MAXIMUM_LAG`) et avertissement **0,3** (`correlated_residuals`) fixes alors que le seuil statistique dépend du nombre d'échantillons (Ljung 1999 : bande de confiance ±2,58/√N à 99 %) ; `NOISE_MULTIPLE = 2` (heuristique, 2σ ≈ 95 % pour un bruit gaussien, acceptable mais non démontré) ; excitation en boucle fermée `EXCITATION_NOISE_RATIO = 20`. | **Corrigé le 25/11** pour le résidu retard 1 : borne de blanchité de Ljung à 99 % (2,576/√N) pour la proposition de critères, `correlated_residuals` (FOPDT, SOPDT, intégrateur) et le contrôle de dérive ; elle remplace 0,5 et 0,3. Effet constaté : un modèle du premier ordre appliqué à un procédé du second ordre est maintenant refusé aussi sous bruit de 1 % avec 250 échantillons (avant : accepté). Limite : la borne suppose un bruit blanc ; un bruit coloré peut faire refuser un modèle adéquat, l'utilisateur peut alors relâcher le critère en connaissance de cause. **Restent ouverts** : `NOISE_MULTIPLE`, `EXCITATION_NOISE_RATIO`. |
| F-03 | **élevée** | L'utilisateur devait fournir le temps de réponse cible. | **Corrigé** : `recommend.py`. L'utilisateur choisit un objectif de robustesse ; MKR cherche le réglage le plus rapide qui le respecte sur le modèle nominal et sur l'ensemble d'incertitude. |
| F-04 | moyenne | Les marges ne portaient que sur le modèle nominal. | **Atténué** : la recommandation vérifie les coins de l'incertitude (±2 erreurs-types : 8 coins pour FOPDT et SOPDT, 4 pour l'intégrateur). **Le 25/11**, les ajustements SOPDT et intégrateur publient eux aussi leurs erreurs-types (jacobien numérique, conditionnelles aux mesures de base et d'entrée) ; leur calibration est vérifiée par Monte Carlo (`test_standard_errors.py`). Limites : erreurs locales, valables pour un bruit à peu près blanc ; pour SOPDT, l'incertitude du second pôle n'est pas propagée (`second_pole_uncertainty_not_propagated`). |
| F-05 | moyenne | Pas d'optimisation numérique : recherche en grille dans la famille SIMC. | Reconnu. L'optimisation sous contraintes (IAE minimal avec Ms ≤ limite) reste à faire. **Ouvert.** |
| F-06 | moyenne | Le choix P / PI / PID suit la règle SIMC (PID seulement si le second pôle dépasse le retard) ; pas d'évaluation de la commande intégrale seule pour un retard dominant. | Documenté ; **ouvert**. |
| F-07 | moyenne | Le filtre de la dérivée du PID n'est pas dimensionné (`derivative_filter_unspecified`). | **Ouvert.** |
| F-08 | élevée | Seuls des échelons isolés avec régime permanent initial sont acceptés. | **Ouvert** (ARX, état initial estimé). |
| F-09 | moyenne | Dispersion de l'identification en boucle fermée sous-estimée d'environ 30 %. | Publiée ; reste expérimentale. |
| F-10 | élevée | Les simulateurs PI/PID de MKR sont validés surtout par leurs propres tests. | **Atténué** pour le réglage guidé (oracles écrits dans les tests, voir §4) ; reste à faire pour la simulation complète. |
| F-11 | faible | `pi_tuning_integrator.loop_margins` n'utilise que le premier passage à 0 dB et le premier croisement de phase ; correct pour PI sur intégrateur, non général. | Noté. |
| F-12 | faible | Les variations de l'analyse de sensibilité (en %) sont saisies par l'utilisateur, sans lien avec les erreurs-types. | Le réglage guidé relie l'incertitude aux erreurs-types. |

## 4. Campagne de validation (synthétique, paramètres connus)
`engine/test_reference_campaign.py` : bruit initial → `suggest_criteria` → `assess_model` (essai de validation indépendant) → `recommend_tuning`. Le réglage est jugé sur le **procédé vrai**, par balayage fréquentiel dense et par simulation temporelle écrits dans `test_recommend.py`, jamais par le simulateur de MKR.

Critères d'acceptation fixés avant l'exécution (objectif « équilibré », Ms ≤ 1,7) : C1 modèle accepté, gain à 3 %, constante de temps lente à 10 % ; C2 une recommandation est rendue ; C3 la boucle vraie se stabilise (erreur finale < 2 % de l'échelon) ; C4 Ms vrai < 2,0 et ne dépasse pas de plus de 0,05 le pire cas annoncé par MKR.

Résultats (8 procédés, tous conformes) : Ms vrai entre 1,33 et 1,58, toujours inférieur au pire cas annoncé (1,61 à 1,67).

| Procédé | Famille | Réponse choisie | Pire Ms annoncé | Ms vrai |
|---|---|---|---|---|
| Débit | FOPDT | 1,26 s | 1,65 | 1,49 |
| Pression, gain négatif | FOPDT | 5,8 s | 1,67 | 1,43 |
| Température lente | FOPDT | 62 s | 1,67 | 1,41 |
| Dosage chimique | FOPDT | 29 s | 1,64 | 1,53 |
| Chloration, retard long | FOPDT | 241 s | 1,62 | 1,58 |
| Débit bruité | FOPDT | 1,5 s | 1,62 | 1,44 |
| Niveau | Intégrateur | 7,7 s | 1,61 | 1,48 |
| Température 2ᵉ ordre | SOPDT | 20 s | 1,61 | 1,33 |

**Limites de cette preuve** : données synthétiques, modèle structurellement exact, bruit gaussien blanc, procédé linéaire. Elle ne dit rien d'un procédé réel non linéaire, saturé ou perturbé. Données expérimentales publiques : les essais d'osmose inverse examinés (NAWI) ne contiennent pas la paire d'échelons en boucle ouverte requise ; un essai TCLab réel existe (niveau 4 limité). Procédés à dynamique variable et non-linéarités : non couverts.

## 5. Consolidation des branches
Notre branche (`claude/peaceful-cray-phk6q4`) contient toutes les branches `chatgpt/*` et `codex/*` sauf quatre contenant des commits absents, **tous obsolètes** : `chatgpt/sim-02-pi-manual-auto` (le transfert manuel/auto existe déjà dans `pi_mode_transition.py`), `chatgpt/mkr-ui-03b-raw-trials` (correctif WPF de 4 lignes, interface depuis refondue), `codex/mkr-audit-20261121` (documents d'audit antérieurs), `codex/mkr-strategy-evidence` (une ligne de fixture). `main` est 459 commits en arrière. La consolidation consiste à fusionner notre branche dans `main` ; ce n'est pas fait sans l'accord de l'utilisateur.

## 6. Références
Skogestad, J. Process Control 13 (2003) 291–309 · Skogestad, Ind. Eng. Chem. Res. (2006) « Tuning for smooth PID control with acceptable disturbance rejection » · Åström & Hägglund, *Advanced PID Control* (ISA, 2006) · Rivera, Morari & Skogestad, Ind. Eng. Chem. Process Des. Dev. 25 (1986) 252–265 · Ljung, *System Identification: Theory for the User* (2ᵉ éd., 1999) · Morari & Zafiriou, *Robust Process Control* (1989) · documentation officielle Schneider 33002536 (PIDFF) et 33002520 (PID_INT), manuel Siemens PID_Compact.
