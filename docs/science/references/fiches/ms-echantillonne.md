# Fiche — Ms, marges et boucle échantillonnée (retard h/2)

**État de vérification :**
- **V** pour les définitions de Ms et de Mt et les bornes sur les marges, d'après Grimholt (thèse 2018, éq. 3.9 à 3.11).
- **V** pour les valeurs de Skogestad 2003 et le terme h/2 de Skogestad 2003 (p. 293).
- **NV** pour Åström & Hägglund (2006), Åström & Wittenmark (*Computer-Controlled Systems*) et Franklin, Powell & Workman : ouvrages non accessibles légalement depuis la session, aucun passage lu.

## Définitions (lues)
- S = 1/(1 + GK), T = 1 − S, Ms = ‖S‖∞, Mt = ‖T‖∞, Mst = max(Ms, Mt). Source : Grimholt, thèse NTNU 2018, éq. 3.9 et 3.10.
- Ms est l'inverse de la plus courte distance entre le point critique −1 et le lieu de Nyquist de GK.
- Bornes garanties (éq. 3.11, attribuées à Rivera et al. 1986) : **GM ≥ Ms/(Ms − 1)** et **PM ≥ 2 arcsin(1/(2Ms)) ≥ 1/Ms** (en radians). Exemple donné : Ms = 1,6 garantit GM ≥ 2,67 et PM ≥ 36,4°.
- Contrôle croisé lu dans Skogestad 2003 (p. 297) : Ms < 1,7 garantit GM > 2,43 et PM > 34,2°. C'est cohérent : 1,7/0,7 = 2,43 et 2 arcsin(1/3,4) = 34,2°.
- Dans Rivera et al. 1986, « M » désigne le pic de la sensibilité complémentaire, et non Ms (§ II, lu).

## Plages usuelles (lues)
- « Ms typiquement entre 1,4 (réglage robuste) et 2 (réglage plus agressif) » (Skogestad 2003, p. 301).
- « Généralement, Ms ne devrait pas dépasser 2 » (Grimholt 2018, § 3.2).
- SIMC avec τc = θ : Ms = 1,59 (τ1 ≤ 8θ) à 1,70 (intégrateur).
- La plage « 1,2 à 2,0 » que `recommend.py` attribue à Åström & Hägglund : **non vérifiée**.

## Retard équivalent dû à l'échantillonnage (h/2)
- **Lu** : « pour une implantation numérique de période h, la contribution au retard effectif est approximativement h/2 (le temps moyen que met le correcteur à réagir à un changement) » (Skogestad 2003, p. 293). Le terme est inclus dans les éq. 10 et 11.
- **Non lu** : le développement classique selon lequel le bloqueur d'ordre zéro (BOZ) se comporte, aux fréquences basses devant π/h, comme un retard de h/2, puisque (1 − e^(−sh))/(sh) a pour phase −ωh/2. Il est attribué à Åström & Wittenmark (chapitre à identifier). Il n'est pas cité ici faute d'avoir lu la source.
- **Calcul (non cité)** : la phase du BOZ vaut exactement −ωh/2 et son gain |sinc(ωh/2)| est inférieur à 1. Au voisinage de la pulsation de coupure ωc, l'approximation par un retard pur h/2 est donc **légèrement pessimiste en gain** et exacte en phase, pour la seule réponse fréquentielle continue équivalente.
  - Elle ignore le repliement et la nature discrète du correcteur (intégration discrète, dérivée filtrée discrète).
  - Domaine raisonnable présumé : ωc·h petit, par exemple h ≤ θ_eff/2 ou ωc·h ≤ 0,5. Ce seuil est **à justifier**, aucune source lue ne le fixe.

## Rapport au code (lecture seule)
- `recommend.py` ajoute h/2 au retard effectif pour le réglage **et** pour l'évaluation de Ms sur les coins d'incertitude. C'est conforme à Skogestad 2003 pour le réglage.
- Employer le même retard approché pour **juger** Ms revient à évaluer la robustesse sur un modèle continu approché, et non sur la boucle échantillonnée réelle.
- `noise_to_control_gain` déclare ignorer le repliement et le BOZ, ce qui est honnête.
- `pi_tuning.py` et `stability_margins.analyze` n'ont pas de paramètre h.

## Critères de validation proposés
1. **Oracle discret indépendant** :
   - discrétiser exactement le procédé avec BOZ (retard fractionnaire compris) ;
   - discrétiser le PI ou le PID selon la loi réellement implantée (par exemple Euler arrière pour l'intégrale) ;
   - calculer Ms = max |1/(1 + L(e^(jωh)))| pour ω ∈ (0, π/h] ;
   - comparer au Ms continu avec θ + h/2.
2. Fixer a priori l'écart toléré, par exemple |ΔMs| ≤ 0,05. Publier le domaine (θ/h, ωc·h) où cet écart est respecté ; en dehors, refuser ou avertir.
3. Retrouver le tableau 2 de Skogestad (Ms 1,59 et 1,70) quand h → 0.
