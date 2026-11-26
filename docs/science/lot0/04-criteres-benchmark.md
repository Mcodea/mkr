# LOT 0 — Critères de benchmark, fixés AVANT toute comparaison

**Statut :** cadre méthodologique. **Aucune comparaison n'a été réalisée ; ORIAS n'est déclaré ni équivalent, ni supérieur, ni inférieur à un logiciel de référence.** Les logiciels visés (LOOP-PRO Tuner, ExperTune, MATLAB PID Tuner / System Identification Toolbox, Siemens PID Tuner) ne sont ni disponibles ni sous licence ici ; leurs seules sources sont leurs documentations publiques (non relues sur les sites officiels : accès bloqué). L'algorithme NSS de LOOP-PRO est propriétaire et n'est pas reproduit.

## Règles

1. Critères, tolérances et corpus **publiés avant exécution** (pré-enregistrement) ; aucune tolérance ajustée après observation.
2. Mêmes données, hypothèses et contraintes pour tous les candidats ; découpage apprentissage / validation / qualification disjoint ; graines déclarées.
3. **Ne pas sélectionner les situations favorables** : le corpus inclut cas nominaux, limites, dégradés, hors domaine et cas de refus.
4. Chaque cas est classé : **comparable, supérieur, inférieur, non comparable** — chacun avec sa preuve ; à défaut de logiciel de référence, comparer à des résultats analytiques, des publications et des implémentations indépendantes (`scipy`, `python-control`).
5. Les tolérances sont à **justifier** (incertitude de mesure, exigence d'usage, littérature) ; aucune valeur n'est posée sans fondement.

## Critères (détail, sources et fondements des tolérances : `annexes/E-referentiel.md` §5)

| Critère | Métrique candidate | Référence indépendante envisagée |
|---|---|---|
| Précision d'identification | erreur relative sur K, τ, θ (Monte-Carlo, biais et dispersion) | simulations à vérité connue ; Ljung, Söderström & Stoica |
| Qualité de prédiction | NRMSE/FIT en simulation libre sur validation disjointe | validation croisée de Ljung |
| Incertitudes | taux de couverture empirique des intervalles annoncés | covariance asymptotique ; bootstrap |
| Robustesse des modèles | blancheur des résidus, corrélation résidus–entrée, stabilité des paramètres | Ljung & Box 1978 |
| Stabilité des régulations | Ms, marges, marge de retard | `python-control` (calcul croisé) ; Åström & Murray |
| Performance des réglages | IAE/ISE, dépassement, établissement **à robustesse égale** | Grimholt & Skogestad (non vérifiée) ; optimum indépendant |
| Rejet de perturbations | IAE sur perturbation de charge et de sortie | idem |
| Effort de commande | variation totale de u, pic de u, ‖KS‖∞ | littérature SIMC (source précise à relever) |
| Temps de calcul | temps médian et 95ᵉ centile, matériel déclaré | mesure instrumentée |
| Refus de données inadéquates | matrice de confusion sur corpus **étiqueté a priori** | excitation persistante (Söderström & Stoica) ; boucle fermée (Forssell & Ljung) |
| Simplicité d'utilisation | taux de réussite des tâches, temps, erreurs | protocole d'essai utilisateur (références d'utilisabilité non vérifiées) |

## Diagnostic industriel approfondi (cahier §41)

Cibles : mauvais réglage PID, défaut de mesure, actionneur saturé, frottement ou hystérésis de vanne, perturbation externe, variation de dynamique, problème d'échantillonnage, modèle inadéquat. **Principe :** ne jamais recommander un nouveau réglage quand une autre cause peut expliquer le comportement ; ne jamais attribuer une cause certaine sans preuve dans les données. **Aucune de ces détections n'existe aujourd'hui** (voir `01`, lignes §41). Sources scientifiques candidates : `annexes/E-referentiel.md` §6 (non vérifiées).
