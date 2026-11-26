# Fiche — IMC-PID de Rivera, Morari et Skogestad (1986)

**État de vérification : V.** J'ai lu le texte de l'article (copie scannée diffusée par S. Skogestad, rendue en image) pour les § II, IV.2, V.1, V.2 et V.3, le tableau I et le tableau II, soit les pages 252-261. Je n'ai pas relu le reste de l'article.

## Référence exacte
D. E. Rivera, M. Morari, S. Skogestad, « Internal Model Control. 4. PID Controller Design », *Industrial & Engineering Chemistry Process Design and Development* **25**(1), 252–265, janvier 1986. DOI 10.1021/i200032a041 (Crossref, V). Le titre Crossref ne comporte pas « 4. », le titre imprimé si.

URL lue : https://skoge.folk.ntnu.no/publications/1986/Rivera86/Rivera86.pdf

## Ce qui est publié pour g(s) = k e^(−θs)/(τs + 1)
Notations : ε est la constante de temps du filtre IMC, c'est-à-dire de la boucle fermée (le λ de MKR). La forme du correcteur est **idéale (parallèle)**, éventuellement complétée d'un filtre : c = kc[1 + 1/(τI s) + τD s] · 1/(τF s + 1) (note du tableau I).

**1. PI par approximation de Padé d'ordre zéro** (e^(−θs) ≈ 1). C'est le cas A du tableau I et l'éq. 75 :
- c = (1 + τs)/(k ε s)
- soit **kc = τ/(kε), τI = τ**
- ε/θ recommandé > 1,7 (tableau II)

La ligne « PI » du tableau II est mal imprimée dans le scan (« θ/τ = 0,1 ; 1,54 »). L'expression ci-dessus est lue dans l'éq. 75 et dans le cas A du tableau I.

**2. PID par approximation de Padé du premier ordre** (éq. 73 et 74, tableau II, ligne PID) :
- c = (1 + τs)(1 + θs/2) / (k(θ/2 + ε)s)
- k·kc = (2τ + θ)/(2ε + θ) ; τI = τ + θ/2 ; τD = τθ/(2τ + θ)
- ε/θ recommandé > 0,8

**3. PI « amélioré »** (§ V.3, éq. 78 à 80, tableau II). Il ne vient **pas** de l'approximation de Padé : le retard est absorbé dans une constante de temps majorée, g̃ = k/((τλ)s + 1) avec λ = 1 + 0,5(θ/τ) (éq. 79, attribuée à Rivera 1984) :
- c = (1 + (τ + θ/2)s)/(k ε s)
- soit **k·kc = (2τ + θ)/(2ε)**, autrement dit kc = (τ + θ/2)/(kε), et **τI = τ + θ/2**
- ε/θ recommandé > 1,7

Contrainte générale (en-tête du tableau II) : ε > 0,1τ toujours, car la bande passante en boucle fermée excède rarement dix fois celle de la boucle ouverte (p. 260).

Remarque de Skogestad (2003, éq. 33, V) : avec ε = 1,7θ, ce PI amélioré s'écrit Kc = 0,588(τ1 + θ/2)/(kθ), τI = τ1 + θ/2. C'est la forme qu'il cite comme « improved IMC PI-settings ». Les réglages IMC donnent τI ≥ τ1, d'où un rejet lent des perturbations de charge quand τ1 ≫ θ (Skogestad 2003, p. 300).

## Robustesse : définitions de l'article
- M y désigne le pic de la **sensibilité complémentaire** |H| = |gc/(1 + gc)| (§ II), et non Ms.
- Pour M = 1 : GM ≥ 2 et PM ≥ 60° (éq. 14 et 15).
- Erreur de retard admissible δ < PM/ωc (éq. 17).

## Écart majeur avec le code et la documentation (lecture seule)
`engine/pi_tuning.py` (version d'algorithme 1.2.0) calcule `imcPublished` : Kc = (τ + θ/2)/(K(λ + θ/2)), Ti = τ + θ/2. Son commentaire le présente comme le « Published IMC-PI (Rivera, Morari and Skogestad 1986) … first-order Pade ». `docs/science/pi-fopdt.md` et `audit-moteur-scientifique.md` (F-01) disent la même chose.

**Ce n'est pas un PI publié par l'article.** Kc = (2τ + θ)/(K(2λ + θ)) avec Ti = τ + θ/2 est le gain et le temps intégral du **PID** de Padé du premier ordre (forme idéale), auquel on a retiré τD = τθ/(2τ + θ). Le PI publié qui porte Ti = τ + θ/2 est le PI amélioré, dont le gain vaut kc = (τ + θ/2)/(Kλ) (λ seul au dénominateur).

Pour un même λ, le gain du code vaut λ/(λ + θ/2) fois le gain publié. Avec λ = 1,7θ, c'est **0,773** fois le gain publié, donc un réglage moins agressif.

Ce réglage n'est pas faux en soi, mais il est **mal nommé** : c'est une variante dérivée, pas une méthode publiée sous ce nom (AGENTS.md § 6.1). La dérivation « indépendante » invoquée dans F-01 vérifie l'algèbre du PID de Padé, pas l'existence de ce PI dans la publication.

**Autres points à noter :**
- La clé `imc` (Kc = τ/(K(λ + θ)), Ti = τ) correspond, selon la note historique de Skogestad (révision d'août 2026, lue), à la règle PI de Chien et Fruehauf (1990) et au « Lambda tuning » (approximation de Taylor). Le PI IMC de Rivera 1986 (cas A) est kc = τ/(kε), **sans θ** au dénominateur. Le libellé « Lambda (Dahlin) » adopté le 25/11 n'est vérifié qu'en partie : Dahlin (1968) n'a pas été lu, et selon Skogestad, Dahlin n'approxime pas le retard.
- Aucune des deux clés n'applique les bornes recommandées de l'article (ε/θ > 1,7 pour le PI, ε > 0,1τ) ; `pi_tuning` n'avertit que si λ < θ.

## Critères de validation proposés
1. Implanter, sous leurs vrais noms :
   - « IMC-PI (Rivera 1986, cas A) » : kc = τ/(Kε), τI = τ ;
   - « IMC-PI amélioré (Rivera 1986, éq. 80) » : kc = (τ + θ/2)/(Kε), τI = τ + θ/2 ;
   - « IMC-PID (Rivera 1986, éq. 74) » : forme idéale, kc = (2τ + θ)/(K(2ε + θ)), τI = τ + θ/2, τD = τθ/(2τ + θ).
2. Comme oracle, vérifier que la boucle fermée nominale du PI cas A suit l'éq. 77 et celle du PID suit l'éq. 76 (réponse indépendante de τ, à l'échelle θ).
3. Pour le PID, retrouver le minimum de J/J_opt = 1,092 à ε/θ = 0,68 et M ≈ 1,3 (p. 260, figure 4). C'est une lecture de figure, avec une tolérance graphique.
4. Renommer ou retirer `imcPublished` tant qu'aucune source ne publie ce PI. Cette décision relève de l'architecte (ADR).
