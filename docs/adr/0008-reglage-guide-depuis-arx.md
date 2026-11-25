# ADR 0008 — Réglage guidé depuis une étude ARX acceptée

Statut : implémenté, à valider (revue humaine puis recette Windows). Prolonge l'ADR 0007 (étude ARX enregistrée). Décision de conception de l'architecte dans le cadre de la délégation « développe ce qui reste à développer » du 25/11/2026 ; aucun contrat de stockage n'est modifié.

## Contexte
L'étude ARX enregistrée affichait un équivalent du premier ordre sans pouvoir le régler : le réglage guidé exige un modèle d'identification validé (`IdentificationStudy`). L'ARX a pourtant sa propre porte de validation, sur une série **indépendante** : stabilité, erreur de simulation libre bornée, résidus blancs, résidus non corrélés aux entrées (`docs/science/arx.md`).

## Décision
1. **La porte est l'acceptation ARX.** Une étude refusée, ou dont le modèle retenu est du second ordre, ne produit aucun réglage : le message dit pourquoi (refus à la validation indépendante ; pas de réduction documentée du second ordre).
2. **Modèle réglé** = équivalent du premier ordre (ZOH exact) du modèle (1,1) retenu : K, τ et leurs erreurs-types par la méthode delta, publiées par le moteur.
3. **Retard quantifié au pas.** Un modèle (1,1) à nk échantillons représente un retard continu compris entre (nk − 1)·Ts et nk·Ts. Le réglage utilise le retard **prudent nk·Ts** et une erreur-type de retard **Ts/2** : la boîte d'incertitude (±2 erreurs-types) couvre [(nk − 1)·Ts, (nk + 1)·Ts]. Vérifié sur les procédés vrais (test `QuantisedDelayConvention`) : sensibilité maximale ≤ 1,7 + 0,05 pour des retards vrais de (nk−1)·Ts à nk·Ts.
4. **Aucun nouveau contrat moteur** : l'opération `recommend_tuning` est inchangée ; le pont C# construit la charge utile et applique les mêmes contrôles de cohérence que pour les études d'identification. Le réglage reste « théorique, non qualifié ».

## Alternatives écartées
- **Réduire un second ordre à un premier ordre** (règle de la moitié ou ajustement de réponse) : aucune réduction n'a été vérifiée ni documentée pour les modèles discrets ARX ; refusée plutôt qu'inventée.
- **Utiliser le retard (nk − 1)·Ts du moteur** avec une erreur-type nulle : annonce une certitude que la grille ne donne pas.

## Conséquences et risques
- Un modèle ARX second ordre accepté ne peut pas être réglé dans cette version (l'utilisateur est informé).
- Le réglage hérite des limites de l'ARX : bruit à peu près blanc, pas régulier, procédé linéaire.

## Retour arrière
Retirer le bouton « Réglage guidé depuis l'ARX » et l'opération C# associée ; aucune donnée stockée n'est concernée.
