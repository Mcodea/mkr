# P0-3 — Robustesse du réglage guidé non tenue

Trois défauts distincts ; les deux premiers sont corrigés dans le moteur, le troisième est traité côté moteur et reste à relier à l'interface.

## P0-3a — Robustesse jugée sur le modèle réduit (règle de la moitié)
**Anomalie.** Pour un SOPDT avec τ₂ ≤ θ, `recommend.py` calculait le PI par la règle de la moitié (Skogestad 2003) **puis jugeait aussi la robustesse sur ce modèle réduit**. **Preuve :** `engine/test_recommend_half_rule_robustness.py` (oracle fréquentiel écrit dans le test) : Ms annoncé 1,3691 contre 1,4387 réel sur le SOPDT identifié (limite « robuste » 1,4), 18 sous-tests sur 18 en échec avant correction. **Impact :** la promesse « Ms ≤ limite sur toutes les configurations » était fausse pour le procédé identifié. **Référence :** Skogestad 2003, éq. 10-11 (règle de la moitié) — **lue** (voir `references/fiches/regle-de-la-moitie.md`) ; la règle sert à calculer le régulateur, pas à juger sa robustesse. **Correction :** les coins d'incertitude et l'évaluation sont faits sur le SOPDT identifié (τ, τ₂, θ) ; le régulateur reste calculé sur le modèle réduit. **Après :** 19/19 sous-tests, Ms réel ≤ limite et annoncé ≥ réel − 10⁻³.

## P0-3b — L'échantillonnage représenté par un retard h/2
**Anomalie.** La boucle jugée est continue avec un retard supplémentaire h/2. Cette approximation est optimiste quand h n'est pas petit devant la dynamique en boucle fermée, et le résultat dépend du schéma d'intégration du bloc.
**Preuve (oracle indépendant : procédé discrétisé par `scipy.signal.cont2discrete`, ZOH ; PI en Euler avant et arrière) :** θ = 0, τ = 10 s, objectif « équilibré » (limite 1,7) :
| h | Ms Euler avant (avant → après) | Ms Euler arrière (avant → après) |
|---|---|---|
| 1 s | 1,805 → **1,467** | 2,363 → **1,612** |
| 2 s | 1,801 → **1,456** | 2,355 → **1,621** |
| 3 s | 1,760 → **1,418** | 2,405 → **1,663** |

Objectif « robuste » (limite 1,4) : Ms 1,402 (Euler arrière, K=2, τ=25, θ=3, h=3) → 1,333. Quand θ est assez grand devant h, rien ne change (aucune recommandation modifiée dans ces cas).
**Impact.** Un réglage « équilibré » pouvait dépasser l'objectif de 6 à 40 % selon le bloc réellement utilisé.
**Référence.** Skogestad 2003 confirme l'ajout de h/2 comme approximation (lu) ; la fonction de transfert impulsionnelle exacte (ZOH, retard fractionnaire) est **dérivée dans `engine/sampled_loop.py`** et vérifiée à 10⁻⁶ contre la réponse indicielle analytique (`test_sampled_loop.py`) — les ouvrages de commande échantillonnée (Åström & Wittenmark, Franklin-Powell) n'ont pas pu être lus : **non cités**.
**Correction.** Nouveau module `sampled_loop.py` ; `recommend.py` 1.2.0 exige que le Ms de la **boucle échantillonnée**, sur tous les coins et pour le pire des deux schémas d'intégrale (Euler avant, Euler arrière), respecte l'objectif ; les PI/PID « optimisés » sont recherchés sous une limite de conception resserrée par pas de 3 % (huit au plus, `designPeakSensitivityLimit` publié) jusqu'à ce que la boucle échantillonnée respecte l'objectif, car l'optimum continu se place exactement sur sa limite ; à défaut, ils sont retirés avec un avertissement (`optimized_pi_unavailable_on_the_sampled_loop`). Cette contrainte supplémentaire a d'abord fait échouer 4 tests existants de `test_recommend.py` (optimum retiré) ; ils passent maintenant **sans modification**.
**Limites.** Procédés à pôles réels, PI/PID idéal discret ; le schéma réel du bloc constructeur n'est pas connu (pire des deux retenu) ; la stabilité du système échantillonné n'est pas démontrée par Ms borné (les marges continues restent nécessaires) ; le chemin « intégrateur » (`_integrator`) n'est pas encore vérifié sur la boucle échantillonnée.

## P0-3c — Période du régulateur ≠ cadence des données
**Anomalie.** Le C# transmet la cadence des données enregistrées (`PythonDiagnostics.cs:322`) comme période d'échantillonnage du régulateur. **Correction moteur :** champ facultatif `controllerPeriodSeconds` ; s'il est absent, l'avertissement `controller_period_assumed_equal_to_data_cadence` est émis ; s'il est présent, la boucle est jugée à cette période (`samplePeriodSeconds` en sortie). **Reste à faire (LOT 1) :** saisir la période d'exécution du bloc dans la fiche de boucle (interface, contrat C#, SQLite) et la transmettre.
