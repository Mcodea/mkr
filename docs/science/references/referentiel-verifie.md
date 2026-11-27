# Référentiel bibliographique vérifié (complément du LOT 0, annexe E)

Rédigé le 26/11/2026 par le documentaliste scientifique, en lecture seule du code. Ce document complète `docs/science/lot0/annexes/E-referentiel.md` (§ 8) ; il ne le remplace pas.

## Niveaux

| Niveau | Signification |
|---|---|
| **V-texte** | Texte primaire lu (URL indiquée). |
| **V-notice** | Notice confirmée à la source d'enregistrement (Crossref `api.crossref.org/works/<doi>` ou métadonnées de l'éditeur), sans lecture du texte. |
| **NV-c** | Notices secondaires concordantes, non confirmées à la source. |
| **NV** | Non vérifiée. |

Une notice V ne vaut **pas** validation du contenu.

## Méthode et accès réseau (26/11/2026)
- **Accessibles** : api.crossref.org (avec limitation de débit HTTP 429 par moments), skoge.folk.ntnu.no, api.openalex.org, arxiv.org (page de recherche).
- **Refusés ou inaccessibles** :
  - www.sciencedirect.com : HTTP 403 (page anti-robot) avec curl, résolution DNS refusée avec WebFetch ;
  - ntnuopen.ntnu.no : connexion refusée ;
  - export.arxiv.org ;
  - liu.diva-portal.org, hal.science, tandfonline.com : hors des hôtes autorisés.
- **Aucune restriction n'a été contournée.**
- Les PDF de skoge.folk.ntnu.no sont des copies diffusées par l'auteur. Les symboles grecs n'étant pas extraits par `pdftotext`, j'ai lu les équations sur les pages rendues en image.
- Les fichiers téléchargés sont restés dans le répertoire temporaire de la session ; aucun extrait long n'est reproduit.

## 1. Références prioritaires (contenu lu)

| Réf. | Notice exacte | Niveau | URL consultée |
|---|---|---|---|
| Skogestad 2003 | « Simple analytic rules for model reduction and PID controller tuning », *J. Process Control* 13(4), 291–309, juin 2003, DOI 10.1016/S0959-1524(02)00062-8 | **V-texte** | https://skoge.folk.ntnu.no/publications/2003/tuningPID/finalpaper.pdf ; Crossref |
| Erratum Skogestad | *J. Process Control* 14(4), 465, 2004, DOI 10.1016/j.jprocont.2003.09.001. Titre Crossref : « Corrections and comments from the author on the paper… » ; titre du PDF : « Erratum to… ». **Quatre corrections** : éq. 9 (s manquant), éq. 12 règle T3 (τ0 → T0), tableau 6 (en-têtes des colonnes 2 et 3). Aucune ne touche les règles SIMC ni la règle de la moitié employées par MKR | **V-texte** | https://skoge.folk.ntnu.no/publications/2003/tuningPID/correction.pdf |
| Réimpression MIC | *Modeling, Identification and Control* 25(2), 85–120, 2004, DOI 10.4173/mic.2004.2.2. L'auteur signale une coquille dans les notes c et d du tableau 1 | V-notice. PDF sans couche texte, non relu | Crossref ; https://skoge.folk.ntnu.no/publications/2003/tuningPID/mic-version/README.txt |
| Rivera, Morari, Skogestad 1986 | « Internal Model Control. 4. PID Controller Design », *Ind. Eng. Chem. Process Des. Dev.* 25(1), 252–265, janv. 1986, DOI 10.1021/i200032a041 | **V-texte** (p. 252-261) | https://skoge.folk.ntnu.no/publications/1986/Rivera86/Rivera86.pdf ; Crossref |
| Broïda 1969 | V. Broida, « The determination of large time-constants by step-response extrapolation », *Automatica* 5(5), 677–683, sept. 1969, **DOI 10.1016/0005-1098(69)90033-8** (confirmé, n'est plus « déduit ») | V-notice ; **contenu NV** (accès fermé, HTTP 403) | Crossref ; OpenAlex (« closed ») |
| Broïda (version française) | « L'extrapolation des réponses indicielles apériodiques », *Automatisme* XIV(3), 1969, p. 105–114 | **NV** | aucune source primaire trouvée |
| Grimholt, thèse 2018 | *Optimal tuning of PID controllers and the verification of the SIMC rules*, NTNU, 2018 | **V-texte** (§ 1.5, chapitres 2 et 3) | https://skoge.folk.ntnu.no/publications/thesis/2018_grimholt/phd-thesis-grimholt-2018.pdf |
| G3 | Grimholt, Skogestad, « Optimal PI and PID control of first-order plus delay processes and evaluation of the original and improved SIMC rules », *JPC* 70, 36–46, oct. 2018, DOI 10.1016/j.jprocont.2018.06.011 | V-notice ; contenu lu dans la thèse (chapitre 3, « published as » G3) | Crossref |
| G4 | Grimholt, Skogestad, « Optimization of fixed-order controllers using exact gradients », *JPC* 71, 130–138, nov. 2018, DOI 10.1016/j.jprocont.2018.09.001 | V-notice ; contenu lu dans la thèse (chapitre 2) | Crossref |
| G1 | Grimholt, Skogestad, « Optimal PI-Control and Verification of the SIMC Tuning Rule », *IFAC Proceedings Volumes* 45(3), 11–22, 2012, DOI 10.3182/20120328-3-IT-3014.00003 | **V-texte** (résumé et page de titre) et V-notice | https://skoge.folk.ntnu.no/prost/proceedings/PID-12/papers/0126.pdf ; Crossref |
| G2 | « Optimal PID-Control on First Order Plus Time Delay Systems & Verification of the SIMC Rules », *IFAC Proc. Vol.* 46(32), 265–270, déc. 2013, DOI 10.3182/20131218-3-IN-2045.00122 | V-notice | Crossref |
| G5 | « Should we forget the Smith Predictor? », *IFAC-PapersOnLine* 51(4), 769–774, 2018, DOI 10.1016/j.ifacol.2018.06.203 | V-notice | Crossref |
| Chapitre SIMC | Skogestad, Grimholt, « The SIMC Method for Smooth PID Controller Tuning », dans *PID Control in the Third Millennium* (dir. R. Vilanova, A. Visioli, selon la thèse), Springer, série *Advances in Industrial Control*, 2012, p. 147–175, DOI 10.1007/978-1-4471-2425-2_5 | V-notice | Crossref |
| (nouveau) | Grimholt, Skogestad, « Optimal PID control of double integrating processes », *IFAC-PapersOnLine* 49(7), 127–132, 2016, DOI 10.1016/j.ifacol.2016.07.228 | V-notice | Crossref |
| Note historique | S. Skogestad, « Some remarks on the History of PID control for first-order with delay process (Dahlin, IMC, SIMC, Lambda-tuning) », note non publiée, révision d'août 2026 | V-texte. Contient une coquille (« τI = max(τ, 4τc+θ) » au lieu du min{τ1, 4(τc+θ)} de l'article) : à ne pas citer pour les formules | https://skoge.folk.ntnu.no/publications/2003/tuningPID/ |

## 2. Ouvrages (notices non confirmées à la source ; contenu non lu)

| Réf. | Niveau | Remarque |
|---|---|---|
| Ljung, *System Identification: Theory for the User*, 2ᵉ éd., Prentice Hall, 1999 | NV-c | Aucun texte libre légitime trouvé. Le compte rendu de la 2ᵉ éd. dans *Automatica* (DOI 10.1016/S0005-1098(01)00214-X, selon OpenAlex) n'a pas été lu |
| Söderström & Stoica, *System Identification*, Prentice Hall, 1989 | NV-c | |
| Åström & Hägglund, *Advanced PID Control*, ISA, 2006 | NV-c | La plage « Ms 1,2–2,0 » et « N 2–20 » citées par le code sont **non vérifiées** |
| Åström & Wittenmark, *Computer-Controlled Systems*, 3ᵉ éd., 1997 | NV-c | L'approximation BOZ ≈ retard h/2 n'est pas lue dans cette source |
| Franklin, Powell & Workman, *Digital Control of Dynamic Systems* | NV | Non recherché à la source |
| Åström & Murray, *Feedback Systems*, 2ᵉ éd., 2021 | NV-c | Le wiki de Caltech est inaccessible |
| Burnham & Anderson, *Model Selection and Multimodel Inference*, DOI 10.1007/b97636 | V-notice | Crossref donne **2004** comme date (2ᵉ éd. imprimée en 2002 selon l'annexe E) : écart à noter |
| Jelali & Huang (dir.), *Detection and Diagnosis of Stiction in Control Loops*, 2010, DOI 10.1007/978-1-84882-775-2 | V-notice | |
| Jelali, *Control Performance Management in Industrial Automation*, 2013, DOI 10.1007/978-1-4471-4546-2 | V-notice | |

## 3. Articles : notices confirmées par Crossref le 26/11/2026 (V-notice)

| Réf. | DOI | Correction apportée à l'annexe E |
|---|---|---|
| Åström & Hägglund, « Revisiting the Ziegler–Nichols step response method for PID control », *JPC* 14(6), 635–650, 2004 | 10.1016/j.jprocont.2004.01.002 | — |
| Åström, Panagopoulos, Hägglund, « Design of PI Controllers based on Non-Convex Optimization », *Automatica* 34(5), 585–601, 1998 | **10.1016/S0005-1098(98)00011-9** | DOI trouvé ; ordre des auteurs selon Crossref : Åström, Panagopoulos, Hägglund |
| Panagopoulos, Åström, Hägglund, « Design of PID controllers based on constrained optimisation », *IEE Proc. Control Theory Appl.* 149(1), 32–40, 2002 | 10.1049/ip-cta:20020102 | Nouvelle référence (PID) |
| Akaike 1974, *IEEE TAC* 19(6), 716–723 | 10.1109/TAC.1974.1100705 | — |
| Schwarz 1978, *Ann. Stat.* 6(2), 461–464 | 10.1214/aos/1176344136 | — |
| Hurvich & Tsai 1989, *Biometrika* 76(2), 297–307 | 10.1093/biomet/76.2.297 | Une seule source auparavant : confirmé |
| Ljung & Box 1978, *Biometrika* 65(2), 297–303 | 10.1093/biomet/65.2.297 | — |
| Gustavsson, Ljung, Söderström 1977, *Automatica* 13(1), 59–75 | 10.1016/0005-1098(77)90009-7 | — |
| Forssell & Ljung 1999, *Automatica* 35(7), 1215–1241 | 10.1016/S0005-1098(99)00022-9 | — |
| Van den Hof & Schrama 1993, *Automatica* 29(6), 1523–1527 | 10.1016/0005-1098(93)90015-L | — |
| Van den Hof 1998, « Closed-loop issues in system identification », *Annual Reviews in Control* 22, 173–186 | 10.1016/S1367-5788(98)00016-9 | Jugé « source douteuse » auparavant : confirmé |
| Horch 1999, *Control Eng. Practice* **7(10)**, **1221–1231** | 10.1016/S0967-0661(99)00100-8 | Volume et pages confirmés |
| Choudhury, Thornhill, Shah 2005, « Modelling valve stiction », *CEP* 13(5), 641–658 | **10.1016/j.conengprac.2004.05.005** | DOI complété |
| Choudhury, Shah, Thornhill 2004, *Automatica* 40(10), 1719–1728 | 10.1016/j.automatica.2004.03.022 | — |
| Thornhill & Hägglund 1997, *CEP* 5(10), 1343–1354 | **10.1016/S0967-0661(97)00131-7** | DOI trouvé |
| Hägglund 2007, *JPC* 17(6), 489–499 | 10.1016/j.jprocont.2007.01.002 | — |
| Jelali 2006, *CEP* 14(5), 441–466 | 10.1016/j.conengprac.2005.11.005 | — |
| Park, Martin, Kelly, Hedengren 2020 (TCLab), *Comput. Chem. Eng.* 135, 106736 | 10.1016/j.compchemeng.2020.106736 | — |
| Lacerda et al. 2020 (SysIdentPy), *JOSS* 5(54), 2384 | 10.21105/joss.02384 | — |
| Fuller et al., « The Python Control Systems Library (python-control) », 60ᵗʰ IEEE CDC, 2021, p. 4875–4881 | **10.1109/CDC45484.2021.9683368** | DOI trouvé |
| Ljung 1985, « Asymptotic variance expressions for identified black-box transfer function models », *IEEE TAC* | 10.1109/TAC.1985.1104093 | Notice OpenAlex seulement (NV-c) ; non lu |

**Restent NV** : Harris 1989 (requête Crossref limitée par HTTP 429, non refaite) ; Basseville & Nikiforov 1993 ; Isermann 2006 ; ISO 9241-11 ; Brooke 1996 ; licences des données (DaISy, Nonlinear Benchmark, TCLab) ; brevet Control Station ; documentation des logiciels industriels (§ 4 de l'annexe E) ; normes IEC.

## 4. Fiches de méthode
Répertoire `fiches/` :
- `regle-de-la-moitie.md` (V)
- `simc.md` (V)
- `imc-pid-rivera-1986.md` (V)
- `broida.md` (notice V, contenu NV)
- `erreur-type-fopdt.md` (formules NV à la source, dérivation fournie)
- `identifiabilite-arx-integrateur.md` (V pour l'énoncé de Skogestad, le reste par calcul)
- `ms-echantillonne.md` (V pour Ms et h/2 selon Skogestad ; NV pour Åström & Wittenmark)

## 5. Écarts entre le code (lecture seule) et les sources lues

Versions lues : `pi_tuning` 1.2.0, `recommend` 1.1.0, `broida` 1.1.0, `fopdt` 1.4.0, `arx` 1.0.0. `fopdt.py` a été **modifié pendant cette lecture** : il faut le relire avant toute revue.

| # | Gravité | Code | Source lue | Constat |
|---|---|---|---|---|
| E-1 | **Élevée** | `pi_tuning.py` `imcPublished` : Kc = (τ+θ/2)/(K(λ+θ/2)), Ti = τ+θ/2, présenté comme « Published IMC-PI (Rivera et al. 1986) … first-order Padé ». Même affirmation dans `docs/science/pi-fopdt.md` et `audit-moteur-scientifique.md` (F-01) | Rivera 1986, tableau II et éq. 74, 75, 80 | **Aucun PI de cette forme n'est publié.** Ce Kc et ce Ti sont ceux du **PID** de Padé du premier ordre (forme idéale) dont on a retiré τD = τθ/(2τ+θ). Les PI publiés sont : kc = τ/(kε), τI = τ (éq. 75) ; et le PI amélioré kc = (τ+θ/2)/(kε), τI = τ+θ/2 (éq. 80, sans θ/2 au dénominateur). À même λ, le gain du code vaut λ/(λ+θ/2) fois celui du PI amélioré, soit 0,77 à λ = 1,7θ |
| E-2 | Moyenne | `pi_tuning.py` : aucune borne sur λ, sauf l'avertissement `tight_response` si λ < θ | Rivera 1986, tableau II : ε/θ > 1,7 (PI), > 0,8 (PID), et ε > 0,1τ toujours | Les bornes publiées ne sont pas appliquées |
| E-3 | Moyenne | Clé `imc` = τ/(K(λ+θ)), Ti = τ, libellée « Lambda (Dahlin) » depuis le 25/11 | Note historique de Skogestad (V) : c'est la règle PI de Chien et Fruehauf (1990) et du Lambda tuning (approximation de Taylor). Dahlin (1968) n'approxime pas le retard ; Dahlin n'a pas été lu | Attribution « Dahlin » non vérifiée. Le PI IMC de Rivera (cas A, sans θ) n'est calculé nulle part |
| E-4 | Moyenne | Docstring de `recommend.py` : « Ms 1,2–2,0 usual (Åström & Hägglund 2006) » ; règle de l'intégrateur rangée sous Åström & Hägglund | Skogestad 2003 p. 301 : Ms 1,4–2 ; tableau 1 : règle de l'intégrateur | La plage 1,2–2,0 n'est pas vérifiée. La règle de l'intégrateur est mal attribuée : elle vient de Skogestad 2003. Les objectifs {1,4 ; 1,7 ; 2,0} sont conformes à la plage lue |
| E-5 | Faible | `recommend.py` : « N 2–20 (Åström & Hägglund) », N = 10 par défaut | Skogestad 2003 p. 292 : τF = ατD, α = 0,1–0,2 en pratique (N = 5–10) | La plage attribuée n'est pas vérifiée ; la valeur par défaut est compatible avec la source lue |
| E-6 | Faible | `pi_tuning.py` n'inclut pas h/2 ; `recommend.py` l'inclut | Skogestad 2003, éq. 10 et 11 | Incohérence entre les deux modules pour un régulateur numérique |
| E-7 | Faible | `recommend.py` : PID si `second > delay` (retard brut) ; grille de τc à partir de 0,25θ_eff | Skogestad 2003 : « τ2 > θ, approximately » ; τc = θ recommandé ; Grimholt : τc = θ/2 pour l'iSIMC, de 0,5θ à 1,5θ étudiés | Critères conformes en esprit. Les valeurs τc < 0,5θ sortent du domaine commenté par les sources |
| E-8 | Moyenne | `broida.py` : 28 %/40 %, coefficients 5,5, 2,8 et 1,8, nommés « Broïda » | Broïda 1969 : **texte non lu** | Coefficients cohérents avec le calcul exact (biais d'arrondi : +0,28 % sur τ). L'attribution à Broïda reste NV |
| E-9 | Information | `fopdt.py` : erreurs-types σ²(JᵀJ)⁻¹ + propagation du plateau (σ_b²/n_b)ssᵀ | Ljung et Söderström & Stoica **non lus** | Conforme à ma dérivation de premier ordre. Formule NV à la source ; validation Monte-Carlo de la couverture à faire (fiche) |
| E-10 | Information | `arx.py` : refus de l'équivalent du premier ordre quand p + 2,576σ_p ≥ 1 ou τ > horizon | Skogestad 2003 p. 292 (τ1 > 8θ : k et τ1 mal déterminés, l'intégrateur k' suffit) | Cohérent. Seuil à 99 % non sourcé (choix de conception) |
| E-11 | Information | Le Ms de `recommend.py` est évalué sur un modèle continu avec θ + h/2 | Skogestad 2003 : h/2 est une approximation pour le **réglage** | Le jugement de robustesse n'est pas fait sur la boucle échantillonnée : un oracle discret est à ajouter (fiche) |

Conformités vérifiées :
- SIMC PI et PID : Kc, τI = min, τD = τ2 ;
- conversion série vers idéal (éq. 36) ;
- règle de la moitié (éq. 10) ;
- SIMC de l'intégrateur (tableau 1) ;
- ajout de h/2 dans `recommend.py`.

## 6. Ce qui reste à faire par un humain
1. Obtenir Broïda 1969 (*Automatica*) et la version d'*Automatisme* par un accès institutionnel ou un prêt entre bibliothèques : confirmer les seuils et les coefficients.
2. Consulter Ljung (1999, chapitre 9) et Söderström & Stoica (1989) pour la covariance asymptotique ; Åström & Wittenmark pour le BOZ et h/2 ; Åström & Hägglund (2006) pour les plages de Ms et de N. Relever les pages exactes.
3. Arbitrer E-1 à E-3 (nommage ou retrait de `imcPublished`, ajout des PI publiés, bornes ε/θ) par une ADR. Aucun code n'a été modifié.
