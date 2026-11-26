# Fiche — Règle de la moitié (half rule) de Skogestad

**État de vérification : V** (source primaire lue le 26/11/2026, pages rendues en image pour lire les symboles grecs).

## Référence exacte
- S. Skogestad, « Simple analytic rules for model reduction and PID controller tuning », *Journal of Process Control* **13**(4), 291–309, juin 2003. DOI 10.1016/S0959-1524(02)00062-8 (Crossref, V).
- Erratum : *Journal of Process Control* **14**(4), 465, 2004. DOI 10.1016/j.jprocont.2003.09.001. Le PDF de l'auteur l'intitule « Erratum to “Simple analytic rules…” » ; Crossref l'intitule « Corrections and comments from the author on the paper… ». Les deux concernent le même document.
- Texte lu : https://skoge.folk.ntnu.no/publications/2003/tuningPID/finalpaper.pdf et https://skoge.folk.ntnu.no/publications/2003/tuningPID/correction.pdf (copies diffusées par l'auteur).
- Réimpression : *Modeling, Identification and Control* **25**(2), 85–120, 2004, DOI 10.4173/mic.2004.2.2 (Crossref, V). L'auteur précise qu'elle contient des commentaires supplémentaires. Son PDF (mic-version/mic.pdf) est une image sans texte et n'a pas été relu.

## Équations (p. 293, éq. 9 à 11, avec la correction de l'erratum sur l'éq. 9)
Modèle d'origine :

  g0(s) = Πj(−T_j0^inv s + 1) / Πi(τ_i0 s + 1) · e^(−θ0 s)

Les constantes τ_i0 sont rangées par ordre décroissant ; T_j0^inv > 0 sont les constantes de temps de réponse inverse (zéros instables). L'erratum ajoute le « s » oublié devant le « + » du numérateur.

**Réduction en premier ordre** e^(−θs)/(τ1 s + 1) (éq. 10) :
- τ1 = τ10 + τ20/2
- θ = θ0 + τ20/2 + Σ_{i≥3} τi0 + Σj T_j0^inv + h/2

**Réduction en second ordre** (éq. 11) :
- τ1 = τ10 ; τ2 = τ20 + τ30/2
- θ = θ0 + τ30/2 + Σ_{i≥4} τi0 + Σj T_j0^inv + h/2

Ici h est la période d'échantillonnage, dans le cas d'une implantation numérique.

Énoncé : la plus grande constante de temps négligée est répartie par moitié entre le retard effectif et la plus petite constante de temps conservée.

Exemple E1 (p. 293), utile comme oracle : g0 = 1/((s+1)(0,2s+1)) donne k = 1, θ = 0,1 et τ1 = 1,1.

## Hypothèses et domaine
- Approximation **empirique** : l'auteur précise que « la base principale de la règle est de maintenir la robustesse des réglages PI et PID proposés », justifiée par des exemples (E1 à E15, § 4.2). Elle ne garantit **pas** la meilleure approximation de la réponse indicielle au sens des moindres carrés.
- Unités : constantes de temps et retards dans la même unité de temps (en secondes dans MKR).
- Les zéros positifs (T0 > 0) se traitent auparavant par les règles T1 à T3 (éq. 12). L'erratum corrige le dénominateur de la règle T3 en (τ̃0 − T0)s + 1. MKR n'emploie pas ces règles.
- Pour un procédé à constante de temps dominante (τ1 > 8θ environ, p. 292), k et τ1 sont difficiles à identifier séparément. On peut alors approcher le procédé par un intégrateur k' = k/τ1 (éq. 5).

## Rapport au code (lecture seule, 26/11/2026)
- `engine/recommend.py` (famille SOPDT, τ2 ≤ θ) : `tau1, theta = tau + second/2, delay + second/2`. C'est **conforme** à l'éq. 10 appliquée à un SOPDT, où τ10 = τ et τ20 = τ2.
- Le terme h/2 est ajouté ensuite (`effective = theta + sample/2`), ce qui est **conforme** aux éq. 10 et 11.
- Le choix entre PID et demi-règle se fait par le test `second > delay`. Ce test compare τ2 au retard **brut** (sans h/2). La source formule ce critère comme approximatif (« τ2 > θ, approximately », p. 292 et 296) : l'écart est mineur, mais le critère retenu devrait être documenté.

## Critères de validation proposés
1. Reproduire exactement l'exemple E1 (k = 1, θ = 0,1, τ1 = 1,1) et les réductions des cas E2 à E15 du tableau 4, après relecture de ce tableau.
2. Oracle indépendant : comparer les Ms obtenus sur le **modèle d'origine** g0 avec le réglage SIMC issu du modèle réduit à ceux du tableau 4 (Ms de 1,59 à 1,70 ; jusqu'à 2 pour E14 et E15, p. 300).
3. Ne pas présenter le modèle réduit comme un modèle identifié : c'est une approximation orientée réglage.
