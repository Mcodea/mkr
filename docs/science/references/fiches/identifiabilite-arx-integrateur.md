# Fiche — Identifiabilité premier ordre / intégrateur (pôle discret proche de 1)

**État de vérification :**
- **V** pour l'énoncé pratique de Skogestad (2003, p. 292).
- **Calcul** pour l'équivalent bloqueur d'ordre zéro (BOZ) et pour la limite p → 1.
- **NV** pour tout énoncé statistique formel : Ljung et Söderström & Stoica n'ont pas été lus.

## Références
- Skogestad 2003, *JPC* 13, 291–309, DOI 10.1016/S0959-1524(02)00062-8, p. 292, éq. 5 (V). « Si la réponse est à constante de temps dominante, c'est-à-dire si τ1 > 8θ environ, les valeurs individuelles de τ1 et de k peuvent être difficiles à obtenir, mais elles importent peu pour le réglage. » Le procédé peut être approché par un intégrateur k/(τ1 s + 1) ≈ k'/s avec k' = k/τ1, ce qui est exact quand τ1 → ∞.
- Söderström & Stoica 1989 et Ljung 1999 (excitation persistante, identifiabilité) : NV-c pour les notices, contenu non lu.

## Équations (calcul)
L'équivalent BOZ exact de K e^(−θs)/(τs + 1), échantillonné à Ts avec θ = (nk − 1)Ts, est :

  y(k) = p y(k−1) + b u(k−nk), avec p = e^(−Ts/τ) et b = K(1 − p).

D'où τ = −Ts/ln p et K = b/(1 − p).

Quand τ ≫ Ts·N (N = nombre d'échantillons), p → 1. Le développement 1 − p ≈ Ts/τ donne b ≈ K Ts/τ = k' Ts, qui reste bien déterminé. En revanche τ et K pris séparément divergent avec 1/(1 − p) : seule leur combinaison k' = K/τ (la pente) est identifiable. La propagation par la méthode delta, dτ/dp = Ts/(p ln²p), devient non bornée.

## Hypothèses
- Boucle ouverte, entrée suffisamment excitante, bruit d'équation (ARX) ou de sortie (OE). Sous bruit de sortie, l'ARX simple est biaisé : c'est un principe standard non relu ici.

## Rapport au code (lecture seule)
`engine/arx.py` 1.0.0, `first_order_equivalent` :
- refuse de publier l'équivalent du premier ordre si p + 2,576·σ_p ≥ 1, ou si τ dépasse la durée d'enregistrement ;
- dans ces cas, l'ARX reste un prédicteur valide.

Cette logique est **cohérente** avec l'énoncé de Skogestad et avec le calcul ci-dessus. Le seuil à 99 % est un choix de conception, non sourcé.

Une remarque de code, sans incidence numérique : `d_tau_d_a1` est affecté deux fois (lignes 203-204). La valeur finale −Ts/(p ln²p) est correcte, puisque dp/da1 = −1, et elle n'entre que par son carré.

Pour `fopdt.py`, l'avertissement `parameter_bound` quand τ atteint la borne haute (l'horizon) joue un rôle analogue. Le critère τ1 > 8θ de Skogestad n'est utilisé nulle part comme alerte « préférer un modèle intégrateur » : c'est une **proposition**.

## Critères de validation proposés
1. Sur un intégrateur pur simulé, l'ARX(1,1) doit refuser l'équivalent du premier ordre dans 99 % des tirages au moins.
2. Sur un FOPDT avec τ = 10 × l'horizon, même refus. La pente b/Ts identifiée doit couvrir k' (intervalle à 95 %).
3. Sur un FOPDT avec τ < horizon/4, l'équivalent doit être publié et ses intervalles doivent couvrir (K, τ) à leur niveau nominal (Monte-Carlo).
