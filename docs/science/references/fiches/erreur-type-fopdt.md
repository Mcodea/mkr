# Fiche — Erreur-type des paramètres FOPDT estimés par moindres carrés non linéaires (plateau initial mesuré)

**État de vérification : NV pour les formules à la source.**
- Ljung (1999), Söderström & Stoica (1989), Seber & Wild et Bates & Watts n'ont **pas** été lus : aucun texte libre et légitime n'était accessible depuis les hôtes autorisés.
- Les tentatives sur liu.diva-portal.org, hal.science, tandfonline.com et annualreviews.org ont été refusées par le proxy de session.
- La recherche arXiv (arxiv.org/search) n'a donné aucune source pertinente.
- Les formules ci-dessous sont des **résultats standard de linéarisation** que j'ai **redérivés**. Elles ne sont **pas citées**. Elles restent à confronter à Ljung (1999, chapitre 9, « asymptotic distribution of parameter estimates » : chapitre à confirmer sur un exemplaire).

## Références à lire (notices)
- L. Ljung, *System Identification: Theory for the User*, 2ᵉ éd., Prentice Hall, 1999 : NV-c (notices de catalogues).
- T. Söderström, P. Stoica, *System Identification*, Prentice Hall, 1989 : NV-c.
- L. Ljung, « Asymptotic variance expressions for identified black-box transfer function models », *IEEE TAC* 30(9), 1985, DOI 10.1109/TAC.1985.1104093 (OpenAlex, V pour la notice ; non lu). Il porte sur les modèles boîte noire d'ordre élevé, pas sur le FOPDT.

## Équations (dérivation, non citée)
Modèle :

  y_i = b + K Δu · f(t_i; τ, θ) + e_i, avec f = 1 − e^(−(t−θ)/τ) pour t > θ et e_i ~ iid(0, σ²).

Les paramètres estimés sont p = (K, τ, θ) ; le plateau b est **mesuré** par la moyenne de n_b points avant l'échelon.

1. **Covariance conditionnelle à b** : Cov(p̂) ≈ σ² (JᵀJ)⁻¹, où J = ∂ŷ/∂p est évalué en p̂ et σ² = RSS/(N − 3).
2. **Propagation de l'incertitude du plateau** : une erreur δ sur b décale toutes les observations. Au premier ordre, Δp = −(JᵀJ)⁻¹Jᵀ1·δ, avec Var(δ) = σ_b²/n_b. Si δ est indépendante des e_i : Cov(p̂) ≈ σ²(JᵀJ)⁻¹ + (σ_b²/n_b) s sᵀ, où s = (JᵀJ)⁻¹Jᵀ1.
3. **Variante équivalente** : estimer b comme quatrième paramètre, avec les points du plateau inclus dans la régression. Cette variante sert d'oracle croisé.

## Hypothèses et limites
- Bruit blanc, indépendant, homoscédastique. Sinon (résidus corrélés), l'erreur-type est sous-estimée.
- Linéarisation valide pour un faible rapport bruit sur signal.
- J est non dérivable en θ quand un échantillon tombe exactement sur θ.
- L'instant de l'échelon est supposé connu. L'incertitude sur l'instant de l'échelon (onset) est un biais borné, pas un bruit.
- Le « ±2 erreurs-types ≈ 95 % » de `recommend.py` suppose en outre une loi normale des estimateurs.

## Rapport au code (lecture seule)
`engine/fopdt.py` « 1.4.0 », lu le 26/11/2026. Le fichier a changé pendant la lecture : modification concurrente en cours, à relire avant revue.
- `conditional_errors` calcule σ²(JᵀJ)⁻¹ par décomposition en valeurs singulières, puis ajoute (σ_b²/n_b) s sᵀ avec s = (JᵀJ)⁻¹Jᵀ1. C'est **conforme à la dérivation 2**, dans les unités normalisées par `scale`.
- Refus explicites : résidus corrélés au-delà de ±2,576/√N, mauvais conditionnement, échantillon sur le retard.
- Le seuil de blancheur ±2,576/√N à 99 % est attribué à Ljung (1999). Le **principe** est standard, mais la page n'a pas été lue : NV.
- `broida.py` publie `standardErrors=None`, ce qui est cohérent : aucune incertitude n'est annoncée.

## Critères de validation proposés
1. **Monte-Carlo** sur un FOPDT à vérité connue (au moins 1 000 tirages, bruit gaussien de σ connu, n_b et N variés). Le taux de couverture empirique des intervalles ±1,96·ET doit être compatible avec 95 %, avec une tolérance binomiale de ±1,4 point à 1 000 tirages (≈ 2√(0,95·0,05/1000)). Vérifier en particulier la couverture du **gain** avec et sans propagation du plateau.
2. Comparer les erreurs-types à celles de la variante 3 (b estimé) : l'écart doit tendre vers 0 quand n_b grandit.
3. Avec des résidus corrélés (bruit AR(1)), vérifier que le calcul est refusé et qu'aucune erreur-type n'est publiée.
4. Faire relire la dérivation 2 contre Ljung (1999) ou Seber & Wild (*Nonlinear Regression*, Wiley, 1989 ; notice non vérifiée) avant toute qualification.
