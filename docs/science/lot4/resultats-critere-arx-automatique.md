# Critère d'erreur ARX automatique — résultats (pré-enregistrement : `protocole-critere-arx-automatique.md`)

**Verdict : NON ADOPTÉ dans le moteur.** Le critère d'adoption fixé d'avance (i : borne supérieure unilatérale à 95 % du taux d'acceptation des populations inadéquates ≤ 5 % à **chaque** niveau de bruit) n'est satisfait ni par la règle automatique ni par aucune limite fixe. Aucun critère n'a été modifié après coup. Données : synthétiques à vérité de **structure** connue (200 cas par population et par bruit, graines 1 000–1 199), sortie complète : `resultats-critere-arx-automatique.json`, script `tests/reference/real_data/arx_criteria_study.py`, moteur `arx 1.0.0`.

## Faux positifs de structure (acceptation de modèles inadéquates : P3 Hammerstein + P4 gain non invariant ; 400 cas par ligne)
| Règle | σ = 0,01 | σ = 0,04 | σ = 0,10 |
|---|---|---|---|
| **Automatique 3 σ̂** | **0,0 % (borne 0,7 %)** | **4,5 % (borne 6,6 %)** | 21,0 % (borne 24,6 %) |
| Limite fixe 5 % de l'étendue | 16,3 % (19,6 %) | 19,3 % (22,8 %) | 13,3 % (16,4 %) |
| Limite fixe 10 % | 16,8 % (20,1 %) | 22,5 % (26,2 %) | 21,0 % (24,6 %) |
| Limite fixe 20 % | 16,8 % (20,1 %) | 22,5 % (26,2 %) | 21,2 % (24,9 %) |

## Puissance (acceptation des populations adéquates : P1 FOPDT + P2 SOPDT)
Automatique : 75,5 % · 84,8 % · 91,0 % ; limite fixe 10 % : 77,0 % · 86,3 % · 90,7 %. **Équivalente.**

## Lecture honnête
1. **La règle automatique domine les limites fixes à bruit faible ou modéré** (0 % contre 16–17 % de faux positifs à σ = 0,01 pour une puissance équivalente ; 4,5 % contre 19–22 % à σ = 0,04) et ne dépend d'aucune limite saisie. L'estimation du bruit est correcte (σ̂/σ médian de 1,00 à 1,09).
2. **À bruit élevé (σ = 0,10, soit 5 % de l'étendue) aucune règle ne contrôle les faux positifs de structure** : l'erreur de structure du cas Hammerstein devient indiscernable du bruit (limite d'information, pas défaut de la règle). Le critère (i) pré-enregistré exigeait la maîtrise à tous les bruits : **non satisfait**, donc non adopté. Je ne l'ai pas assoupli.
3. **Faiblesse nouvelle F-ARX-2 : abstention excessive de la porte à fort rapport signal/bruit.** Pour la population adéquate P2 (SOPDT, structure exactement représentable), la porte refuse 25 cas sur 60 à σ = 0,01 (plus tard, 45 % de refus pour les deux règles : puissance 55 %), le meilleur candidat échouant aux tests de blancheur et de corrélation résidus/entrée (17 cas sur 25 pour les deux à la fois). Les tests statistiques détectent de très petites erreurs d'estimation lorsque le bruit est faible. **La cause (erreur d'optimisation de l'estimation de sortie, délai, ou excès de sensibilité des tests) n'est pas établie.**
4. **Limites** : bruit blanc gaussien ; populations synthétiques ; la vérité est la structure, pas l'aptitude à la régulation ; une seule configuration d'échantillonnage (Ts = 1 s, 600 échantillons).

## Décision
- La voie ARX **conserve** sa limite saisie par l'utilisateur ; sa documentation doit **déclarer** que la décision en dépend et que l'acceptation d'une erreur élevée n'est pas une preuve d'adéquation (S04 h2).
- **Travaux justifiés par les constats** : (a) étudier l'abstention F-ARX-2 (cause, puis remède sur A/B, jamais sur C) ; (b) examiner une règle automatique **combinée** (3 σ̂ plus une borne sur l'étendue) dont le pré-enregistrement doit être **nouveau** (critère d'adoption adapté au bruit : un contrôle de risque conditionnel au rapport signal/bruit) ; (c) joindre au diagnostic le **rapport signal/bruit**, afin d'avertir qu'à fort bruit la structure ne peut pas être jugée.
