# Pré-enregistrement de l'évaluation sur données expérimentales (protocole directeur §6, §8, §10, étape 4)

**Statut : écrit AVANT toute exploitation des jeux S01, S03, S04. Aucun résultat n'existe.** Toute modification sera datée et signalée comme telle.

## 1. Ce qui est évalué et ce qui ne l'est pas
- **Évalué** : (a) le **diagnostic des données** (détection de saturations, pertes de capteur, échantillonnage, dérives, perturbations, mode) ; (b) l'**identification** et la **prédiction sur données indépendantes** ; (c) les **décisions de la porte de validation** et du moteur (accepter / refuser / demander un essai complémentaire) ; (d) pour les essais **en boucle fermée**, la capacité du moteur à **refuser** ou à identifier correctement.
- **Non évalué ici** : la performance d'un nouveau réglage sur le procédé (situation C du protocole §15 : il n'a jamais été appliqué). Les performances d'un réglage recommandé ne seront présentées que comme **simulation**, jamais comme preuve expérimentale. Le régulateur réellement appliqué (S01) n'est évalué qu'à titre **descriptif** (situation B).

## 2. Répartition des données (déterministe, figée ici, indépendante des résultats)
- **S01 (25 essais, banc unique)** : l'ensemble d'un fichier est `A`, `B` ou `C` selon SHA-256(nom du fichier sans dossier) mod 3 (0 → A développement, 1 → B calibration, 2 → C validation intermédiaire). Table : `repartition-S01.tsv` (A 6, B 11, C 8 fichiers). **Limite déclarée** : un seul banc et des essais de mêmes semaines ; l'indépendance est par essai, pas par installation, donc elle est **plus faible** que l'exige l'idéal du §6.
- **S03 (benchmark)** : ensembles `estimation` (uEst, yEst) et `test` (uVal, yVal) **des auteurs** ; le test est un ensemble `C` et n'est utilisé ni pour estimer, ni pour choisir un modèle, ni pour calibrer.
- **S04 (10 répétitions de la même séquence)** : répétitions 1–3 → A, 4–6 → B, 7–10 → C (ordre d'acquisition ; la séquence d'entrée est identique : seules les réalisations de bruit et de perturbation diffèrent, donc l'indépendance porte sur le bruit, **pas sur l'excitation**).
- **Aucun jeu ne sert de `D`** : publics, publications accessibles et structure déjà lue. Aucune validation à l'aveugle n'est revendiquée.

## 3. Ce que reçoit ORIAS
Uniquement les signaux (entrée, sortie, consigne si présente, unités et période d'échantillonnage tels que **documentés par les auteurs**) et ce qu'un automaticien connaîtrait : type de boucle **lorsqu'il est documenté** (S01 fermée ; S03 et S04 ouvertes). ORIAS ne reçoit ni paramètres de référence, ni modèles publiés, ni résultats d'identification des articles. La boucle n'est jamais supposée ouverte lorsqu'elle est inconnue.

## 4. Indicateurs et règles de lecture (fixés avant les essais)
1. **Prédiction indépendante** : erreur quadratique normalisée (NRMSE, par rapport à la variation de la sortie) sur l'ensemble C, avec la méthode `fit` de la sortie de l'engine ; **jamais** comparée à un seuil choisi après coup : on publie la valeur avec l'intervalle (par répétition/essai) et on compare des **versions du moteur** entre elles.
2. **Décision du moteur** : pour chaque essai, accepté / refusé / essai complémentaire, avec la **raison**. Un refus justifié n'est pas un échec (protocole §19) ; un refus excessif est mesuré (taux d'abstention par jeu).
3. **Diagnostic** : anomalies annoncées par les auteurs (pertes de capteur de S01 via `Sensor_Dropout`, saturations observées) → détectées / non détectées / fausses alertes, **par type**.
4. **Aptitude à la régulation** : n'est évaluée que par la **simulation** du réglage recommandé sur les **modèles compatibles** (pas seulement le modèle nominal) ; résultat présenté comme estimation.
5. **Familles** : résultats **par famille de procédé** (niveau mono-réservoir S01 ; cascade non linéaire S03 ; réservoirs couplés S04) pour que la moyenne ne masque pas une faiblesse.

## 5. Garde-fous
- Les ensembles `A` servent seuls à corriger un défaut ; `B` à calibrer ; `C` à comparer des versions. Toute donnée ayant motivé une correction devient `A` pour la suite.
- Les incohérences connues (S04 : unités mm/cm, période 4 s vs ~3,85 s) sont **levées et documentées avant** exploitation ; sinon S04 est écarté et le motif consigné.
- Un résultat défavorable est conservé tel quel ; aucun cas difficile n'est retiré.
- Le moteur actuel est le commit `5202e25` (puis suivants) : toute évaluation consignera le commit, les graines éventuelles et les empreintes des fichiers (registre).

## 6. Mode opératoire de l'évaluation d'identification sur S03 et S04 (ajouté le 2026-11-28 AVANT toute exécution)
Chemin évalué : `engine/arx.py` (`identify`, boucle ouverte déclarée par la documentation des auteurs), seule voie du moteur acceptant une excitation arbitraire ; le chemin FOPDT exige des échelons et n'est pas évalué ici.
- **Horodatages** : S03 et S04 n'ont que la période (4 s) : des horodatages synthétiques `2000-01-01T00:00:00Z + k·Ts` sont générés (transformation documentée, valeurs inchangées).
- **S03** : apprentissage = (uEst, yEst) ; validation = (uVal, yVal), ensembles des auteurs. **S04** : entrée `u` (pompe) → `h1` puis `h2` séparément ; paires apprentissage A / validation C : répétition 1 → 7, 2 → 8, 3 → 9 (fenêtres de 81 échantillons de `complete_data.csv`, début aux lignes 476 + 425·(k−1)).
- **Seuil `maximumRmse`** (que la voie ARX exige de l'utilisateur) : **balayé** à 2 %, 5 %, 10 % et 20 % de l'étendue de la sortie de validation ; on publie la décision du moteur à **chaque** niveau, sans en choisir un a posteriori. `mvTolerance` = 1e-3 (résolution déclarée de l'entrée).
- **Indicateurs** : décision, structure retenue, erreur de simulation libre sur la validation (valeur absolue et normalisée par l'étendue), raisons de refus, et, pour situer, l'erreur d'un prédicteur constant (écart type de la sortie de validation).
- **Rien n'est conclu au-delà** de ce que mesure l'expérience : un refus n'est pas un échec ; une acceptation n'est pas une preuve d'aptitude à la régulation.
