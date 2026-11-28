# Pré-enregistrement — critère d'erreur automatique pour l'identification ARX (écrit le 2026-11-28 AVANT toute exécution de l'évaluation)

**Faiblesse visée** : F-ARX-1 (la décision ARX dépend d'une limite `maximumRmse` saisie ; voir `resultats-identification-arx-S03-S04.md`).
**Règle candidate** (`engine/arx_criteria.py` v1.0.0) : limite = 3 σ̂, σ̂ = bruit estimé **sans modèle** (pseudo-résidus de Gasser, Sroka & Jennen-Steinmetz 1986, MAD robuste, Hampel 1974), poolé sur apprentissage et validation. Le multiple 3 est celui de la voie par échelons ; il n'est **pas** ajusté ici.
**Données déjà vues et donc exclues de la validation indépendante de cette règle** : S03 et S04 (leurs décisions à 2/5/10/20 % ont été lues). Elles sont reclassées **A (développement)** pour cette règle et seront seulement rapportées.
**Évaluation (synthétique, vérité de structure connue ; graines fixes 1 000 à 1 199, disjointes de toute étude antérieure)** — Ts = 1 s, 600 échantillons d'apprentissage et 600 de validation (séquences pseudo-aléatoires indépendantes, bascule de ±1 avec probabilité 0,05 par échantillon), bruit blanc σ ∈ {0,01 ; 0,04 ; 0,10} (sortie d'étendue ≈ 2) :
| Population | Procédé | Vérité de structure |
|---|---|---|
| P1 | FOPDT, K ∈ [0,5 ; 2], τ ∈ [5 ; 30] s (log-uniforme), retard entier de 1 à 8 échantillons | **adéquat** (représentable exactement par ARX(1,1)) |
| P2 | SOPDT, τ1 log-uniforme [5 ; 30], τ2 = τ1·U(0,1 ; 0,5), retard entier | **adéquat** (ARX(2,2)) |
| P3 | Hammerstein : entrée multi-niveaux {−1 ; −0,5 ; 0,5 ; 1}, non-linéarité statique u + 0,6 u³ puis FOPDT | **inadéquat** (non-linéarité) |
| P4 | FOPDT dont le gain est multiplié par 1,25 entre apprentissage et validation | **inadéquat** (modèle non invariant) |
| P5 | chaîne de 4 retards égaux (τ = 10 s) | **sans étiquette** : erreur de structure modérée, rapportée seule |
**Comparaison** : règle automatique 3 σ̂ contre limites fixes à 5 %, 10 % et 20 % de l'étendue de la sortie de validation (pratique actuelle). Les candidats ne dépendent pas de la limite : le moteur est exécuté une fois par cas avec une limite très large, la décision à chaque limite est dérivée des raisons de refus autres que l'erreur et de l'erreur de simulation libre (équivalent à relancer).
**Indicateurs** : taux d'acceptation par population et par bruit avec intervalles exacts de Clopper-Pearson (95 %) ; « puissance » = acceptation des populations adéquates ; « faux positifs de structure » = acceptation des populations inadéquates. **Limite déclarée** : la vérité est la structure du modèle, pas l'aptitude à la régulation.
**Critère d'adoption fixé d'avance** : la règle automatique est adoptée dans le moteur seulement si (i) sa borne supérieure unilatérale à 95 % du taux d'acceptation des populations inadéquates est ≤ 5 % à chaque bruit, ET (ii) son taux d'acceptation des populations adéquates est au moins égal à celui de la meilleure limite fixe parmi celles qui satisfont (i), ET (iii) elle ne dépend d'aucune limite saisie. Sinon elle est rejetée ou limitée, et le motif est publié.
