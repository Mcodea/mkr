# Fiche — Règles SIMC PI/PID (Skogestad 2003) et variantes de Grimholt et Skogestad

**État de vérification : V** pour Skogestad 2003 et son erratum (texte lu). **V** pour le chapitre 3 de la thèse de Grimholt (2018), lu : il est déclaré « publié sous » l'article de *JPC* 2018 (G3). L'article de *JPC* 70 lui-même n'a **pas** été lu (ntnuopen.ntnu.no était inaccessible) : son contenu exact est NV-c.

## Références
- Skogestad 2003, *J. Process Control* 13(4), 291–309, DOI 10.1016/S0959-1524(02)00062-8, et erratum *JPC* 14(4), 465, DOI 10.1016/j.jprocont.2003.09.001. Voir la fiche `regle-de-la-moitie.md`.
- C. Grimholt, *Optimal tuning of PID controllers and the verification of the SIMC rules*, thèse NTNU, 2018 : https://skoge.folk.ntnu.no/publications/thesis/2018_grimholt/phd-thesis-grimholt-2018.pdf (lue, chapitres 1.5, 2 et 3).
- G3 : C. Grimholt, S. Skogestad, « Optimal PI and PID control of first-order plus delay processes and evaluation of the original and improved SIMC rules », *JPC* **70**, 36–46, 2018, DOI 10.1016/j.jprocont.2018.06.011 (Crossref, V pour la notice).

## Équations (Skogestad 2003, p. 296, éq. 23 à 25, forme **série** de l'éq. 1)
Modèle : g(s) = k e^(−θs) / ((τ1 s + 1)(τ2 s + 1)), avec k' = k/τ1.

- Kc = (1/k) · τ1/(τc + θ) = (1/k') · 1/(τc + θ)
- τI = min{τ1, 4(τc + θ)}
- τD = τ2 (PID seulement)

Le PI s'obtient en partant d'un modèle du premier ordre (τ2 = 0).

Cas particuliers (tableau 1, p. 296) :
- **Intégrateur** k' e^(−θs)/s : Kc = 1/(k'(τc + θ)), τI = 4(τc + θ).
- **Retard pur** : contrôleur purement intégral, KI = 1/(k(τc + θ)).
- **Double intégrateur** : PID, avec τD = 4(τc + θ).

Note : la version MIC (2004) signale une coquille dans les notes c et d du tableau 1, où les renvois aux éq. (1) et (27) doivent être intervertis (README de l'auteur, V).

**Conversion vers la forme idéale** (§ 6.3, éq. 36) :
- K'c = Kc(1 + τD/τI)
- τ'I = τI(1 + τD/τI)
- τ'D = τD/(1 + τD/τI)

**Choix de τc** (§ 3.4) :
- Il faut −θ < τc < ∞ pour un gain positif et fini.
- Réglage « rapide avec bonne robustesse » : **τc = θ** (éq. 28), soit Kc = 0,5τ1/(kθ) et τI = min{τ1, 8θ} (éq. 29 et 30).
- Un τc plus grand « désaccorde » le réglage pour le rendre plus doux (§ 6.1).

**Robustesse obtenue avec τc = θ** (tableau 2, p. 297) :

| Procédé | GM | PM | Ms | Mt | Δθ/θ admissible |
|---|---|---|---|---|---|
| τ1 ≤ 8θ | 3,14 | 61,4° | 1,59 | 1,00 | 2,14 |
| Intégrateur | 2,96 | 46,9° | 1,70 | 1,30 | 1,59 |

Les mêmes marges s'appliquent à un second ordre avec τD = τ2.

**Plage de Ms** (§ 5.2, p. 301) : « typiquement entre 1,4 (réglage robuste) et 2 (réglage plus agressif) ». Ms < 1,7 garantit GM > 2,43 et PM > 34,2° (p. 297).

**Filtre de dérivée** (p. 292) : τF = ατD. Les simulations emploient α = 0,01 ; « en pratique, α de 0,1 à 0,2 est normalement employé ».

**Variantes de Grimholt** (thèse, § 3.4, lues) :
- iSIMC (PID série) : mêmes Kc et τI, avec **τD = θ/3** ; τc recommandé ≥ θ/2 (≈ θ/2).
- iSIMC-PI : Kc = (τ + θ/3)/(k(τc + θ)), τI = min{τ + θ/3, 4(τc + θ)}.

Le critère de la thèse est J = 0,5(IAE_dy/IAE°_dy + IAE_du/IAE°_du), où les références IAE° sont celles de PI optimaux à Ms = 1,59. La robustesse y est mesurée par Mst = max(Ms, Mt).

## Hypothèses et domaine
- Modèle linéaire du premier ou du second ordre à retard, obtenu par la règle de la moitié si nécessaire.
- Forme série. τc est le seul paramètre de réglage.
- Pas de saturation, pas de bruit modélisé : la sensibilité au bruit est discutée qualitativement au § 6.2.

## Rapport au code (lecture seule, 26/11/2026)

**Conforme :**
- `pi_tuning.py` (clé `simc`) : Kc = τ/(K(τc + θ)), Ti = min(τ, 4(τc + θ)).
- `recommend.py._simc` : mêmes règles, plus la conversion série vers idéal (éq. 36).
- `recommend.py._integrator` : Kc = 1/(k'(τc + θ)), Ti = 4(τc + θ) (tableau 1).

**Écarts :**
1. La docstring de `recommend.py` attribue la règle de l'intégrateur et la plage « Ms 1,2–2,0 » à Åström et Hägglund (2006). La règle de l'intégrateur est dans Skogestad 2003, tableau 1 (V). La plage 1,2–2,0 n'a **pas** été lue dans Åström et Hägglund (NV). La plage lue est 1,4–2 (Skogestad 2003, p. 301). Les objectifs {1,4 ; 1,7 ; 2,0} sont cohérents avec cette plage lue.
2. « N entre 2 et 20 » est attribué à Åström et Hägglund : NV. La source lue indique α de 0,1 à 0,2, soit N de 5 à 10.
3. `pi_tuning.py` n'ajoute pas h/2 au retard, contrairement à `recommend.py`. Ce n'est conforme aux éq. 10 et 11 que pour une régulation continue.
4. La grille de `recommend.py` descend à τc = 0,25 θ_eff. La source recommande τc = θ ; Grimholt étudie 0,5θ à 1,5θ pour le PI et θ/2 pour l'iSIMC. Le filtrage par Ms limite le risque, mais les valeurs τc < 0,5θ sortent du domaine commenté par les sources.

## Critères de validation proposés
- Retrouver exactement le tableau 2 (GM, PM, Ms, Mt, Δθ/θ) pour τ1 ≤ 8θ et pour l'intégrateur, avec τc = θ, en calcul fréquentiel indépendant (par exemple python-control ou un calcul analytique).
- Retrouver le tableau 3.4 de la thèse (Ms de 1,59 et 1,70 ; Kc et τI) pour e^(−s), e^(−s)/(s+1), e^(−s)/(8s+1) et e^(−s)/s.
