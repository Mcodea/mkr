# Rapport de l'étude du critère de validation — tranche 1 (données synthétiques)

**Statut : TESTÉ AUTOMATIQUEMENT sur données SYNTHÉTIQUES uniquement. Conclusion sur l'adoption d'un nouveau critère : NON CONCLUSIF. La porte de validation actuelle est conservée.** Aucune donnée réelle avec vérité de commande n'a été utilisée (règle §7 iv non satisfaite). Ce rapport ne constitue **pas** une qualification industrielle.

Documents liés : protocole et amendements v1.1 à v1.5 (`protocole-validation-critere.md`), seuils gelés (`seuils-calibration-T1.json`, SHA-256 `57982b228f47bb18fbdd3041fddf26f2f5b5a7c6832b7f5fb272aea4296fa888`, commités **avant** la phase finale), données complètes (`rapport-etude-validation-T1.json`), outils et code (`tests/reference/validation_study/`), préparation des données réelles (`donnees-reelles-preparation.md`).

## 1. Ce qui a été fait
- 6 000 cas tirés par graines fixes (P1 FOPDT, P2 SOPDT, P3 chaînes de retards, P4 FOPDT + bruit coloré et dérive, P6 réponse inverse ; SNR 20/50/200/1000). Indices < 1000 : calibration (1 856 cas avec modèle) ; indices ≥ 1000 : évaluation (3 718 cas avec modèle) ; 426 cas sans modèle identifié (7,1 %) : le moteur ne produit alors aucune recommandation.
- **Vérité** : le réglage recommandé à partir du modèle est appliqué au **procédé vrai** ; il est « apte » s'il est stable par la simulation temporelle ET par le critère de Nyquist (deux méthodes qui doivent s'accorder) et si Ms vrai ≤ M_lim. Un faux positif est un modèle dont le réglage est inapte mais qui est accepté par le critère ; le dénominateur est l'ensemble des inaptes.
- Calibration des seuils sur les seuls indices < 1000, figée avant l'évaluation. Intervalles : Clopper-Pearson exacts ; décisions d'adoption : bornes appariées de l'amendement v1.4.

## 2. Résultat principal : la tranche 1 ne peut pas démontrer l'objectif de 5 %
Effectifs d'inaptes dans l'évaluation : **M ≤ 1,7 → 180 ; M ≤ 2,0 (primaire) → 18 ; M ≤ 2,5 → 8 ; M ≤ 3,0 → 6.** Démontrer 5 % exige au moins 59 inaptes (zéro faux positif), et 1 % au moins 299 (528 avec la correction de Bonferroni). Pour la définition primaire, même **zéro** faux positif donnerait une borne supérieure de 15,3 %. Aucun critère n'est donc « démontré » à 5 % ni à 1 % sur la définition primaire, et la règle d'adoption (primaire + deux définitions alternatives) ne peut pas être satisfaite. **Ce n'est pas un résultat sur le mérite des critères** : c'est un manque de puissance du jeu de cas (le réglage du moteur est prudent, donc presque toujours apte sur ces populations).

## 3. Taux observés, avec effectifs et incertitude (M ≤ 2,0, 3 718 cas ; 3 700 aptes, 18 inaptes)
| Critère | FP (k/n) | FP taux [IC 95 % bilatéral Clopper-Pearson] | borne sup. unilatérale 95 % | FN (k/n) | FN taux | instables ou douteux acceptés / acceptés (borne sup. 95 %) |
|---|---|---|---|---|---|---|
| A | 1/18 | 5.6 % [0.1 % ; 27.3 %] | 23.8 % | 2255/3700 | 60.9 % | 0/1446 (0.2 %) |
| A1 | 14/18 | 77.8 % [52.4 % ; 93.6 %] | 92.0 % | 1228/3700 | 33.2 % | 0/2486 (0.1 %) |
| A2 | 1/18 | 5.6 % [0.1 % ; 27.3 %] | 23.8 % | 2255/3700 | 60.9 % | 0/1446 (0.2 %) |
| B | 1/18 | 5.6 % [0.1 % ; 27.3 %] | 23.8 % | 2387/3700 | 64.5 % | 0/1314 (0.2 %) |
| C | 4/18 | 22.2 % [6.4 % ; 47.6 %] | 43.9 % | 2033/3700 | 54.9 % | 0/1671 (0.2 %) |
| D | 0/18 | 0.0 % [0.0 % ; 18.5 %] | 15.3 % | 3700/3700 | 100.0 % | 0/0 (—) |
| E | 0/18 | 0.0 % [0.0 % ; 18.5 %] | 15.3 % | 3700/3700 | 100.0 % | 0/0 (—) |
| C7 | 0/18 | 0.0 % [0.0 % ; 18.5 %] | 15.3 % | 3700/3700 | 100.0 % | 0/0 (—) |
| A_or_D | 1/18 | 5.6 % [0.1 % ; 27.3 %] | 23.8 % | 2255/3700 | 60.9 % | 0/1446 (0.2 %) |
| A_or_E | 1/18 | 5.6 % [0.1 % ; 27.3 %] | 23.8 % | 2255/3700 | 60.9 % | 0/1446 (0.2 %) |
| A_and_E | 0/18 | 0.0 % [0.0 % ; 18.5 %] | 15.3 % | 3700/3700 | 100.0 % | 0/0 (—) |
| A_or_C7 | 1/18 | 5.6 % [0.1 % ; 27.3 %] | 23.8 % | 2255/3700 | 60.9 % | 0/1446 (0.2 %) |

**Lecture honnête.**
- Les critères **D, E, C7** (seuils calibrés) n'ont **pas pu être calibrés** (15 inaptes < 20 requis) : ils refusent tout. Leur « 0 faux positif » et leur « 100 % de faux négatifs » reflètent cette absence de calibration, **pas** une performance.
- **A1 (erreur quadratique seule)** accepte 14 inaptes sur 18 (77,8 %, borne 92 %) : l'erreur d'ajustement seule ne protège pas, ce qui est cohérent avec l'avertissement « ne confonds jamais qualité d'ajustement et aptitude à la régulation ». **A (critère actuel)** : 1 inapte accepté sur 18 (5,6 %), IC très large [0,1 ; 27,3 %].
- **Réglages instables acceptés** : 0 sur 1 446 acceptés par A (borne supérieure 0,2 %, soit 3/n) ; seuls 2 réglages instables existent dans toute l'évaluation (ils sont comptés dans les 18 inaptes). L'absence d'événement n'est jamais présentée comme « 0 % » certain.
- **Le critère actuel refuse 60,9 % des modèles dont le réglage aurait été apte** (2 255/3 700) : coût en disponibilité, pas en sécurité.

## 4. Autres définitions d'aptitude (faux positifs k/n ; borne supérieure unilatérale 95 %)
| Critère | M ≤ 1.7 : FP k/n ; borne sup. | M ≤ 2.0 : FP k/n ; borne sup. | M ≤ 2.5 : FP k/n ; borne sup. | M ≤ 3.0 : FP k/n ; borne sup. |
|---|---|---|---|---|
| A | 12/180 ; 10.6 % | 1/18 ; 23.8 % | 1/8 ; 47.1 % | 1/6 ; 58.2 % |
| A1 | 89/180 ; 55.8 % | 14/18 ; 92.0 % | 6/8 ; 95.4 % | 4/6 ; 93.7 % |
| A2 | 12/180 ; 10.6 % | 1/18 ; 23.8 % | 1/8 ; 47.1 % | 1/6 ; 58.2 % |
| B | 10/180 ; 9.2 % | 1/18 ; 23.8 % | 1/8 ; 47.1 % | 1/6 ; 58.2 % |
| C | 21/180 ; 16.4 % | 4/18 ; 43.9 % | 2/8 ; 60.0 % | 2/6 ; 72.9 % |
| D | 0/180 ; 1.7 % | 0/18 ; 15.3 % | 0/8 ; 31.2 % | 0/6 ; 39.3 % |
| E | 0/180 ; 1.7 % | 0/18 ; 15.3 % | 0/8 ; 31.2 % | 0/6 ; 39.3 % |
| C7 | 0/180 ; 1.7 % | 0/18 ; 15.3 % | 0/8 ; 31.2 % | 0/6 ; 39.3 % |
| A_or_D | 12/180 ; 10.6 % | 1/18 ; 23.8 % | 1/8 ; 47.1 % | 1/6 ; 58.2 % |
| A_or_E | 12/180 ; 10.6 % | 1/18 ; 23.8 % | 1/8 ; 47.1 % | 1/6 ; 58.2 % |
| A_and_E | 0/180 ; 1.7 % | 0/18 ; 15.3 % | 0/8 ; 31.2 % | 0/6 ; 39.3 % |
| A_or_C7 | 12/180 ; 10.6 % | 1/18 ; 23.8 % | 1/8 ; 47.1 % | 1/6 ; 58.2 % |

À M ≤ 1,7 (180 inaptes) l'objectif de 5 % est mesurable mais aucun critère n'y est démontré : A à 12/180 (6,7 %), borne 10,6 %.

## 5. Qui est inapte ? (résultat inattendu)
Parmi les 18 réglages inaptes (M ≤ 2,0), **16 viennent de populations dont le modèle est exact (P1, P4) et 2 seulement de populations à erreur de structure** (P6 : 2 ; P2 : 0). Le danger observé vient donc surtout de l'**incertitude paramétrique due au bruit** (retard et gain mal estimés), pas d'une erreur de structure : A en accepte 1 sur 16 (exacts) et 0 sur 2 (imparfaits). Accepter un modèle **imparfait** dont le réglage est apte est fréquent (724 acceptés sur 2 227 : voir la table croisée du JSON) ; ce n'est pas une erreur de sécurité.

## 6. Pouvoir discriminant (AUC ; 50 = aucun)
| Statistique | AUC M ≤ 1,7 (180 inaptes) | AUC M ≤ 2,0 (18 inaptes) | n |
|---|---|---|---|
| erreur_quadratique_validation | 63.6 | 61.1 | 3718 |
| autocorrelation_lag1_absolue | 69.3 | 63.4 | 3718 |
| erreur_de_structure_relative_nse | 85.2 | 89.0 | 3718 |
| ljung_box_(1-p) | 69.6 | 68.9 | 3718 |
| correlation_croisee_residus_entree | 81.2 | 75.1 | 3718 |
| E_pire_Ms_ensemble | 53.1 | 61.3 | 3718 |
| C7_pire_Ms_coins | 80.9 | — | 1489 |

L'**erreur de structure relative au bruit** (NSE) discrimine le mieux l'aptitude (AUC ≈ 89 % à M ≤ 2,0 et 85 % à M ≤ 1,7), devant la corrélation croisée résidus–entrée (75–81 %) ; l'**erreur quadratique** (critère actuel) est la plus faible (61–64 %). Ces AUC portent sur 18 inaptes pour la définition primaire : intervalle large, **non confirmatoire**. E (pire Ms de l'ensemble) discrimine peu (53–61 %) ; C7 n'est calculable que sur 1 489 des 3 718 cas (aucun cas P4) et ne contient que 2 inaptes à M ≤ 1,7 : non évaluable.

## 7. Échelle de recours (que faire d'un réglage inapte ou d'un modèle refusé ?)
- Sur les **18 réglages inaptes**, le **réglage plus robuste** du même modèle est apte dans **12** cas (66,7 %, IC 95 % [41,0 ; 86,7]) ; le **modèle alternatif SOPDT** dans **0** (18 disponibles). Les 6 restants relèvent d'un essai complémentaire ou d'un refus.
- Sur les **2 255** modèles refusés par A dont le réglage initial était apte, le réglage plus robuste est apte dans **2 255** (IC 95 % [99,8 ; 100]), le SOPDT dans 1 747 sur 1 847 disponibles.
Ces résultats soutiennent l'ordre de recours demandé (réglage plus robuste avant refus définitif) ; ils ne valent que pour ces populations synthétiques.

## 8. Couverture et biais de sélection
| Population | Cas d'évaluation | Sans modèle identifié | Avec porte | Couverture |
|---|---|---|---|---|
| P1 | 800 | 61 | 739 | 92.4 % |
| P2 | 800 | 65 | 735 | 91.9 % |
| P3 | 800 | 48 | 752 | 94.0 % |
| P4 | 800 | 50 | 750 | 93.8 % |
| P6 | 800 | 58 | 742 | 92.8 % |

Les cas sans modèle (≈ 7 %) ne génèrent aucune recommandation, donc aucun faux positif ; leur taux est comparable entre populations (92–94 % de couverture). Les cas sans statistique E ou C7 ne sont plus exclus (amendement v1.5). Borne de sensibilité de pire cas publiée dans le JSON (`selection_worst_case_fp_upper_adoption`).

## 9. Limites déclarées
Synthétique uniquement ; P5 (Hammerstein) et P7 (dérive de gain) non encore produites ; peu d'inaptes à M ≤ 2,0 (18) ; E et C7 sont des heuristiques sans garantie de pire cas ; D, E, C7 non calibrés ; vérité fondée sur un réglage unique par cas et un procédé vrai simulé ; seuils et définitions fixés par le protocole sans les ajuster aux résultats ; la définition d'aptitude (Ms) est un critère de robustesse parmi d'autres (marges de gain/phase, bruit de commande, perturbations, saturations non évaluées ici).

## 10. Conclusion et suite
1. **Aucune modification de la porte de validation** : la supériorité ou la complémentarité d'un nouveau critère n'est pas démontrée (condition posée par le client).
2. **Constats utiles malgré tout** : l'erreur quadratique seule est un mauvais garde-fou (A1) ; la blancheur et la corrélation résidus–entrée font déjà la différence (A, A2) ; l'erreur de structure relative au bruit est le meilleur discriminant observé et mérite une tranche 2 **dimensionnée à l'avance** ; le réglage plus robuste récupère la grande majorité des cas refusés ou inaptes.
3. **Tranche 2 (à déclarer par amendement avant production)** : ≥ 528 inaptes par définition retenue, populations plus dangereuses (P5, P7, bruit plus élevé, incertitude de retard forte), objectif de 1 % mesurable, et — indispensable pour la règle iv — **au moins un jeu réel avec boucle fermée et réglages documentés** (non trouvé dans les jeux publics inspectés).
