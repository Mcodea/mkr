# Validation de modèles de procédé pour le réglage PID : état de l'art

Rédigé le 27/11/2026 par le documentaliste scientifique, en lecture seule du code. Ce document complète `referentiel-verifie.md`, dont il reprend les niveaux de preuve. Il sert à décider si la porte de validation du moteur (`engine/validation_gate.py` 1.1.0, `engine/model_comparison.py` 1.1.0) doit être complétée. **Il ne prend aucune décision et ne modifie aucun code.**

Conventions :
- **FAIT** : ce que dit une source lue, avec sa page ou sa section.
- **INTERPRÉTATION** : mon raisonnement ou mon calcul, à vérifier.
- **LIMITE** : hypothèse ou domaine de validité.

Une notice V-notice confirme l'existence et les références bibliographiques d'une publication. Elle ne vaut **jamais** lecture de son contenu. La mention « résumé lu » veut dire que seul le résumé (OpenAlex ou arXiv) a été lu.

---

## 0. Le critère actuel, d'après le code

| Élément | Constat dans le code | Conséquence méthodologique |
|---|---|---|
| Nature des résidus | `fopdt.py`, l. 195-196 : `predicted = baseline + gain·amplitude·response`, puis `residual = observations − predicted` | Ce sont des **résidus de simulation** : le modèle est de type « erreur de sortie » (OE), sans modèle de bruit. Ce ne sont pas des erreurs de prédiction à un pas |
| Essai de validation | Un seul échelon indépendant (holdout), en boucle ouverte | L'entrée est un échelon déterministe et non une excitation riche |
| Erreur quadratique | `maximumRmse = max(2σ, tolérance/2)`, où σ est le bruit mesuré sur les plateaux avant l'échelon | C'est déjà, en germe, un test de défaut d'ajustement relatif à une « erreur pure » (voir Q5) |
| Blancheur | \|r̂(1)\| ≤ 2,576/√N (99 %), un seul retard, sans correction du nombre de paramètres | C'est un test à un seul retard, voisin dans son esprit de Durbin-Watson, et non un portemanteau |
| Comparaison FOPDT/SOPDT | Rapport des erreurs quadratiques moyennes comparé à F(n, n) à 99 % | Ce test suppose des résidus indépendants entre instants et entre modèles (voir Q6) |

**FAIT (scénario F-LOT9-1)**, d'après `parcours-moteur.md` et les manifestes de `tests/reference/scenario_pression_pompe/data/` :
- bruit blanc gaussien de 0,003 bar, plus une quantification de 0,001 bar (`generate.py`, l. 191-192) ;
- erreur quadratique de **l'ajustement FOPDT sans bruit** sur le procédé vrai : 0,00272 bar ; pour le SOPDT : 0,00063 bar (`parametres_theoriques.json`) ;
- sur la validation : erreur quadratique de 0,0044 bar ; r̂(1) = 0,42 pour le FOPDT, 0,18 pour le SOPDT ; borne 0,105, soit N ≈ 600.

**INTERPRÉTATION (calcul)**. Écrivons le résidu comme ε = v + y_ε, avec v un bruit blanc de variance σ_v² et y_ε une erreur de structure lisse, d'autocorrélation ρ_ε(1) proche de 1. On a alors :

r(1) ≈ ρ_ε(1) · σ_ε² / (σ_v² + σ_ε²).

Pour le FOPDT : σ_ε ≈ 0,0027 et σ_v ≈ 0,003, donc la part structurelle vaut environ 0,45. La valeur observée r̂(1) = 0,42 correspond alors à ρ_ε(1) ≈ 0,93, ce qui est cohérent.

**Le test à un retard mesure donc, en pratique, la part de variance due à l'erreur de structure.** Sa borne décroît en 1/√N. Pour N donné, il rejette toute erreur de structure dont la part dépasse environ 0,105/ρ_ε, soit ici à peine plus de 10 % de la variance, quel que soit son effet sur la commande.

Le SOPDT ne suit pas ce schéma simple : sa part structurelle sans bruit est d'environ 4 %, alors que r̂(1) vaut 0,18. Une autre contribution lisse intervient probablement : l'erreur d'estimation des paramètres, ou la non-linéarité entre l'essai d'identification (85 → 90 %) et l'essai de validation (85 → 89 %). Ce point n'est pas élucidé.

---

## Méthode et accès réseau (27/11/2026)

- **Hôtes accessibles et utilisés** : api.crossref.org, api.openalex.org, arxiv.org (pages et PDF), skoge.folk.ntnu.no (publications et archive `prost/proceedings/`, répertoires ouverts seulement), www.mathworks.com (documentation), raw.githubusercontent.com (code source de statsmodels), zenodo.org (API des métadonnées), www.nonlinearbenchmark.org.
- **Hôtes refusés par le proxy** (403 ou résolution DNS refusée), **sans aucun contournement** :
  - www.sciencedirect.com (403) et ieeexplore.ieee.org (418) ;
  - link.springer.com, onlinelibrary.wiley.com, tandfonline.com, academic.oup.com, jstor.org, dl.acm.org ;
  - liu.diva-portal.org, diva-portal.org, kth.diva-portal.org, urn.kb.se : ce sont les **copies en accès ouvert de Ljung**, que signale OpenAlex ;
  - users.isy.liu.se, www.control.isy.liu.se ;
  - perso.uclouvain.be et dial.uclouvain.be (Gevers, Bombois), openresearch-repository.anu.edu.au ;
  - www.research-collection.ethz.ch (Smith & Doyle 1992 en accès ouvert), eprints.whiterose.ac.uk (Billings & Voon 1986 en accès ouvert) ;
  - www.itl.nist.gov (NIST/SEMATECH e-Handbook), online.stat.psu.edu, en.wikipedia.org ;
  - www.statsmodels.org, cran.r-project.org, otexts.com, typeset.io, unpaywall.org, semanticscholar.org, core.ac.uk ;
  - hal.science, pure.tue.nl, lup.lub.lu.se, www.control.lth.se, people.kth.se ;
  - homes.esat.kuleuven.be (DaISy).
- Certains répertoires de `skoge.folk.ntnu.no/prost/proceedings/` sont protégés par un mot de passe (README du répertoire) : **ils n'ont pas été ouverts**.
- Les PDF ont été lus avec `pdftotext` et sont restés dans le répertoire temporaire de la session. Les citations sont courtes et en anglais d'origine.
- Les résumés de moteur de recherche n'ont servi qu'à **localiser** des sources. Aucun contenu n'est rapporté sur cette seule base.

---

## (a) Tableau des références

### A.1 Textes lus (V-texte)

| Clé | Notice exacte | Niveau | URL et passage lu |
|---|---|---|---|
| **SL19** | J. Schoukens, L. Ljung, « Nonlinear System Identification: A User-Oriented Road Map », *IEEE Control Systems Magazine* 39(6), 28–99, déc. 2019, DOI 10.1109/MCS.2019.2938121 | **V-texte** (préprint arXiv v1, 2 févr. 2019) ; V-notice (Crossref) | https://arxiv.org/pdf/1902.00683 : p. 13 (modèle linéaire en présence de distorsions non linéaires), p. 30-34 (Model Validation), p. 65-66 (encadré « Simulation Errors and Prediction Errors », éq. S17), p. 85-92 (encadré « Impact of Structural Model Errors », éq. S33-S49). Pages du PDF du préprint |
| **MW-RA** | MathWorks, « What Is Residual Analysis? », System Identification Toolbox, R2026b | **V-texte** (documentation d'éditeur, non revue par des pairs) | https://www.mathworks.com/help/ident/ug/what-is-residual-analysis.html (page entière) |
| **MW-RES** | MathWorks, `resid` (référence de fonction), R2026b. La page cite « Ljung 1999, Section 16.6 » | **V-texte** (documentation) | https://www.mathworks.com/help/ident/ref/resid.html : Description, argument Type (`corr`, `ir`, `fr`), References |
| **MW-EX** | MathWorks, « Examine Model Residuals », R2026b | **V-texte** (documentation) | https://www.mathworks.com/help/ident/ug/examine-model-residuals.html |
| **MW-MQ** | MathWorks, « Loss Function and Model Quality Metrics », R2026b | **V-texte** (documentation) | https://www.mathworks.com/help/ident/ug/model-quality-metrics.html : option Focus, définitions de FPE, AIC, AICc et BIC |
| **MW-LOF** | MathWorks, `anova` (LinearModel), Statistics and Machine Learning Toolbox, R2026b | **V-texte** (documentation) | https://www.mathworks.com/help/stats/linearmodel.anova.html : lignes Lack of fit et Pure error |
| **SM-LB** | statsmodels, `acorr_ljungbox`, docstring (branche `main`, lue le 27/11/2026) | **V-texte** (code source) | https://raw.githubusercontent.com/statsmodels/statsmodels/main/statsmodels/stats/diagnostic.py, l. 797-900 |
| **G04** | M. Gevers, « Identification for Control: Achievements and Open Problems », *IFAC Proceedings Volumes* 37(9), 401–412, 2004 (DYCOPS 7). DOI 10.1016/S1474-6670(17)31843-8 ; Crossref donne aussi un doublon, 10.1016/S1474-6670(17)31908-0, p. 799–810 | **V-texte** ; V-notice | https://skoge.folk.ntnu.no/prost/proceedings/dycops04/pdffiles/papers/213.pdf : §1 (exemple inspiré de Skelton), §6 et §7 |
| **GHEL05** | T. Glad, A. Helmersson, M. Enqvist, L. Ljung, « Controllers for amplitude limited model error models », *IFAC Proc. Vol.* 38(1), 55–60, 2005, DOI 10.3182/20050703-6-CZ-1902.00663 | **V-texte** (§1-2) ; V-notice | https://skoge.folk.ntnu.no/prost/proceedings/ifac2005/Fullpapers/03954.pdf |
| **DKC02** | E. de Klerk, I.K. Craig, « An assignment to teach closed-loop system identification », *IFAC Proc. Vol.* 35(1), 273–278, 2002, DOI 10.3182/20020721-6-ES-1901.01436 | **V-texte** (§ sur l'analyse des résidus) ; V-notice | https://skoge.folk.ntnu.no/prost/proceedings/ifac2002/data/content/01625/1625.pdf |
| **S03** | S. Skogestad, « Simple analytic rules for model reduction and PID controller tuning », *JPC* 13(4), 291–309, 2003, DOI 10.1016/S0959-1524(02)00062-8 | **V-texte** | https://skoge.folk.ntnu.no/publications/2003/tuningPID/finalpaper.pdf : p. 297 (§4.1.1, tableau 2), p. 306 (§6.6), p. 307 (annexe, approximations « sûres ») |
| **RMS86** | D.E. Rivera, M. Morari, S. Skogestad, « Internal Model Control. 4. PID Controller Design », *Ind. Eng. Chem. Process Des. Dev.* 25(1), 252–265, 1986, DOI 10.1021/i200032a041 | **V-texte** | https://skoge.folk.ntnu.no/publications/1986/Rivera86/Rivera86.pdf : p. 253 (éq. 16-19), p. 256 (éq. 59 et texte qui suit) |
| **GR18** | C. Grimholt, *Optimal tuning of PID controllers and the verification of the SIMC rules*, thèse, NTNU, 2018 | **V-texte** (chap. 5, p. 126-129 de la thèse) | https://skoge.folk.ntnu.no/publications/thesis/2018_grimholt/phd-thesis-grimholt-2018.pdf |

### A.2 Notices confirmées, résumé seul lu (V-notice + résumé)

| Clé | Notice | Source du résumé |
|---|---|---|
| **BP70** | G.E.P. Box, D.A. Pierce, « Distribution of Residual Autocorrelations in Autoregressive-Integrated Moving Average Time Series Models », *JASA* 65(332), 1509–1526, 1970, DOI 10.1080/01621459.1970.10481180 | OpenAlex |
| **LB78** | G.M. Ljung, G.E.P. Box, « On a measure of lack of fit in time series models », *Biometrika* 65(2), 297–303, 1978, DOI 10.1093/biomet/65.2.297 | OpenAlex |
| **DW50** | J. Durbin, G.S. Watson, « Testing for serial correlation in least squares regression. I », *Biometrika* 37(3-4), 409–428, 1950, DOI 10.1093/biomet/37.3-4.409 | OpenAlex. La partie II existe : *Biometrika* 38, 159, 1951, DOI 10.2307/2332325 (V-notice) |
| **BV86** | S.A. Billings, W.S.F. Voon, « Correlation based model validity tests for non-linear models », *Int. J. Control* 44(1), 235–244, 1986, DOI 10.1080/00207178608933593 | OpenAlex. La copie ouverte est sur eprints.whiterose.ac.uk, hôte refusé |
| **LG97** | L. Ljung, L. Guo, « The role of model validation for assessing the size of the unmodeled dynamics », *IEEE TAC* 42(9), 1230–1239, 1997, DOI 10.1109/9.623084 | OpenAlex. La copie ouverte est sur urn.kb.se/DiVA, hôte refusé |
| **LG95** | L. Ljung, L. Guo, « Estimating the total model error from a standard validation test », 34th IEEE CDC, 1995, vol. 2, 1651–1656, DOI 10.1109/CDC.1995.480376 | OpenAlex |
| **GGN92** | G.C. Goodwin, M. Gevers, B. Ninness, « Quantifying the error in estimated transfer functions with application to model order selection », *IEEE TAC* 37(7), 913–928, 1992, DOI 10.1109/9.148344 | OpenAlex |
| **HL92** | H. Hjalmarsson, L. Ljung, « Estimating model variance in the case of undermodeling », *IEEE TAC* 37(7), 1004–1008, 1992, DOI 10.1109/9.148358 | OpenAlex |
| **SD92** | R.S. Smith, J.C. Doyle, « Model validation: a connection between robust control and identification », *IEEE TAC* 37(7), 942–952, 1992, DOI 10.1109/9.148346 | OpenAlex. La copie ouverte est sur research-collection.ethz.ch, hôte refusé |
| **GCD99** | M. Gevers, B. Codrons, F. De Bruyne, « Model validation in closed loop », ACC 1999, DOI 10.1109/ACC.1999.782794. Auteurs d'après OpenAlex ; Crossref n'en donne pas | OpenAlex |
| **LH09** | E.K. Larsson, T. Hägglund, « Robustness margins separating process dynamics uncertainties », ECC 2009, 543–548, DOI 10.23919/ECC.2009.7074459. Également cité par GR18 (éq. 5.24-5.25), lu | OpenAlex |
| **GHL04** | T. Glad, A. Helmersson, L. Ljung, « Robustness guarantees for linear control designs with an estimated nonlinear model error model », *Int. J. Robust Nonlinear Control* 14(11), 959–970, 2004, DOI 10.1002/rnc.926 | OpenAlex |
| **LZSS19** | A. Lindholm (anciennement Svensson), D. Zachariah, P. Stoica, T.B. Schön, « Data Consistency Approach to Model Validation », *IEEE Access* 7, 59788–59796, 2019, DOI 10.1109/ACCESS.2019.2915109 | arXiv 1808.05889 (résumé). Crossref donne « Lindholm » comme premier auteur |

### A.3 Notices confirmées, contenu non lu (V-notice)

| Clé | Notice (Crossref) |
|---|---|
| L00 | L. Ljung, « Model Error Modeling and Control Design », *IFAC Proc. Vol.* 33(15), 31–36, 2000 (SYSID 2000), DOI 10.1016/S1474-6670(17)39722-7 |
| RGL02 | W. Reinelt, A. Garulli, L. Ljung, « Comparing different approaches to model error modeling in robust identification », *Automatica* 38(5), 787–803, 2002, DOI 10.1016/S0005-1098(01)00269-2 |
| GBCSA03 | M. Gevers, X. Bombois, B. Codrons, G. Scorletti, B.D.O. Anderson, « Model validation for control and controller validation in a prediction error identification framework — Part I: theory », *Automatica* 39(3), 403–415, 2003, DOI 10.1016/S0005-1098(02)00234-0 ; Part II, 417–427, DOI 10.1016/S0005-1098(02)00235-2 |
| BGSA01 | X. Bombois, M. Gevers, G. Scorletti, B.D.O. Anderson, « Robustness analysis tools for an uncertainty set obtained by prediction error identification », *Automatica* 37(10), 1629–1636, 2001, DOI 10.1016/S0005-1098(01)00104-2 |
| BSGVH06 | X. Bombois, G. Scorletti, M. Gevers, P.M.J. Van den Hof, R. Hildebrand, « Least costly identification experiment for control », *Automatica* 42(10), 1651–1662, 2006, DOI 10.1016/j.automatica.2006.05.016 |
| H05 | H. Hjalmarsson, « From experiment design to closed-loop control », *Automatica* 41(3), 393–438, 2005, DOI 10.1016/j.automatica.2004.11.021 |
| H09 | H. Hjalmarsson, « System Identification of Complex and Structured Systems », *Eur. J. Control* 15(3-4), 275–310, 2009, DOI 10.3166/ejc.15.275-310 |
| HGD96 | H. Hjalmarsson, M. Gevers, F. de Bruyne, « For model-based control design, closed-loop identification gives better performance », *Automatica* 32(12), 1659–1673, 1996, DOI 10.1016/S0005-1098(96)80003-3 |
| G05 | M. Gevers, « Identification for Control: From the Early Achievements to the Revival of Experiment Design », *Eur. J. Control* 11(4-5), 335–352, 2005, DOI 10.3166/ejc.11.335-352 |
| NG95 | B. Ninness, G.C. Goodwin, « Estimation of model quality », *Automatica* 31(12), 1771–1797, 1995, DOI 10.1016/0005-1098(95)00108-7 |
| KLB92 | R.L. Kosut, M.K. Lau, S.P. Boyd, « Set-membership identification of systems with parametric and nonparametric uncertainty », *IEEE TAC* 37(7), 929–941, 1992, DOI 10.1109/9.148345 |
| FL99 | U. Forssell, L. Ljung, « Closed-loop identification revisited », *Automatica* 35(7), 1215–1241, 1999, DOI 10.1016/S0005-1098(99)00022-9 (déjà dans le référentiel) |
| VdH98 | P. Van den Hof, « Closed-loop issues in system identification », *Annu. Rev. Control* 22, 173–186, 1998, DOI 10.1016/S1367-5788(98)00016-9 (déjà dans le référentiel) |
| P71 | D.A. Pierce, « Distribution of residual autocorrelations in the regression model with autoregressive-moving average errors », *JRSS B* 33(1), 140–146, 1971, DOI 10.1111/j.2517-6161.1971.tb00866.x |
| DS98 | N.R. Draper, H. Smith, *Applied Regression Analysis*, Wiley, 1998, DOI 10.1002/9781118625590. Crossref indique 1998 et un champ édition = 1 ; le paquet CRAN `aprean3` la désigne comme 3ᵉ édition de 1998 : **écart à noter** |
| WW40 | A. Wald, J. Wolfowitz, « On a test whether two samples are from the same population », *Ann. Math. Stat.* 11(2), 147–162, 1940, DOI 10.1214/aoms/1177731909. Son emploi comme test des signes ou des suites sur des résidus n'est **pas vérifié** dans cette source |
| A70 | H. Akaike, « Statistical predictor identification », *Ann. Inst. Stat. Math.* 22(1), 203–217, 1970, DOI 10.1007/BF02506337 (FPE) |
| A74, S78, HT89 | Akaike 1974, DOI 10.1109/TAC.1974.1100705 ; Schwarz 1978, DOI 10.1214/aos/1176344136 ; Hurvich & Tsai 1989, DOI 10.1093/biomet/76.2.297 (déjà dans le référentiel) |
| St74 | M. Stone, « Cross-Validatory Choice and Assessment of Statistical Predictions », *JRSS B* 36(2), 111–133, 1974, DOI 10.1111/j.2517-6161.1974.tb00994.x |
| HL99-PA | Huang & Shah, *Performance Assessment of Control Loops*, Springer, 1999, DOI 10.1007/978-1-4471-0415-5 (contexte seulement) |

### A.4 Non vérifiées (NV-c ou NV)

| Réf. | Niveau | Remarque |
|---|---|---|
| **L99-MEM** : L. Ljung, « Model validation and model error modeling », dans B. Wittenmark, A. Rantzer (dir.), *The Åström Symposium on Control*, Studentlitteratur, Lund, 1999, p. 15–42 | **NV-c** | Notice relevée dans la bibliographie de GHEL05 (lue) et dans OpenAlex (rapport DiVA liu-55695). Texte non lu : DiVA refusé ; une copie sur typeset.io est d'origine non établie et n'a pas été consultée |
| Ljung, *System Identification: Theory for the User*, 2ᵉ éd., 1999, notamment §16.6 (cité par MW-RES) | NV-c | Non lu |
| Söderström & Stoica, *System Identification*, 1989 | NV-c | Crossref ne connaît que des comptes rendus (DOI 10.1017/S026646660000880X, 10.1002/acs.4480070305) |
| Morari & Zafiriou, *Robust Process Control*, 1989 ; Lundström, thèse, 1994 (borne exacte de l'erreur multiplicative pour gain et retard) | NV-c | Cités par GR18 (éq. 5.19-5.21), non lus |
| Åström & Hägglund, *Advanced PID Control*, 2006 | NV-c | Non lu |
| Skelton 1989 (exemple repris par G04) | NV | Non lu |

---

## (b) Réponses aux huit questions

### Q1. Bruit, perturbations, erreur de structure, incertitude paramétrique : leurs signatures

**FAIT (SL19, encadré « Impact of Structural Model Errors », p. 85-86, éq. S37).** Pour un modèle de simulation, avec des perturbations additives indépendantes de l'entrée, l'erreur de sortie se décompose en trois termes :
- l'**innovation** ν(t), qu'aucun modèle ne peut prédire ;
- l'**erreur de structure** h₀(u) − h(u, θ*_u), qui reste présente « même avec une quantité infinie de données parfaites » ;
- l'**erreur aléatoire** h(u, θ*_u) − h(u, θ̂_N), due aux perturbations présentes dans les données d'estimation.

Les auteurs soulignent que l'erreur de structure « dépend de l'entrée », et que θ*_u dépend lui-même des propriétés de l'entrée (éq. S36-S38).

**FAIT (SL19, encadré « Simulation Errors and Prediction Errors », p. 65-66, éq. S17).** On note v(t) = y(t) − ŷ₀(t) la perturbation de sortie vraie.
- Si v est blanc, le modèle de simulation donne les prédictions optimales.
- Sinon, v est lisse ou corrélé, et son passé sert à prédire : c'est le rôle du modèle de bruit.

**FAIT (SL19, p. 13).** Avec une excitation aléatoire, les effets non linéaires en sortie « ressemblent beaucoup à du bruit ». On peut en estimer le spectre par un modèle de bruit, de type Box-Jenkins. Mais les bornes d'incertitude qu'on en tire ne sont plus valides, car ces erreurs ne sont pas indépendantes de l'entrée.

**FAIT (SL19, p. 92, éq. S46-S49).** En présence d'erreur de structure, la covariance classique σ²[E K(u)ᵀK(u)]⁻¹ « sous-estime la variabilité ». La solution pragmatique proposée est de répéter les expériences avec des entrées variées. Le résumé de HL92 affirme aussi que, sous sous-modélisation, l'estimation conventionnelle de la variance « could be misleading ».

Synthèse des signatures :

| Source d'écart | Autocorrélation des résidus (modèle OE) | Corrélation résidus/entrée (boucle ouverte) | Effet si N → ∞ |
|---|---|---|---|
| Bruit de mesure blanc | Nulle (hors retard 0) | Nulle | Reste au niveau du bruit |
| Perturbation colorée ou dérive, indépendante de u | **Non nulle**, même avec la structure vraie (SL19, S17) | Nulle en espérance. Mais avec un **échelon unique**, une dérive est corrélée par hasard avec l'échelon (INTERPRÉTATION) | Le test de blancheur rejette sans erreur de structure |
| Dynamique non modélisée (ordre, retard) | Non nulle et lisse | **Non nulle** : c'est la cible du test d'indépendance (MW-RA) | Rejet certain dès que N est assez grand |
| Non-linéarité | Ressemble à du bruit pour une entrée aléatoire (SL19, p. 13) ; dépend de l'amplitude pour un échelon | Non nulle aux moments d'ordre élevé ; partiellement visible à l'ordre 2 (SL19, p. 32 ; BV86, résumé) | Le modèle linéaire tend vers la meilleure approximation linéaire *pour cette entrée* |
| Incertitude paramétrique (structure exacte) | Faible et lisse, décroît en 1/N | Faible, décroît | Tend vers zéro |

**LIMITE.** SL19 traite le cas OE avec des perturbations indépendantes de l'entrée, et vise surtout des excitations riches (multisinus, bruit), non des échelons. La ligne « perturbation colorée » de la colonne « corrélation résidus/entrée » sous un échelon unique est une **INTERPRÉTATION**.

### Q2. Tests de blancheur : hypothèses, puissance, limites

**FAIT (BP70, résumé).** Les autocorrélations des résidus d'un modèle ARMA *estimé* sont approximativement une transformation linéaire **singulière** des autocorrélations des erreurs vraies. Ne pas en tenir compte conduit à « overlook evidence of lack of fit ». Des tests corrigés sont proposés.

**FAIT (LB78, résumé).** Une modification simple du portemanteau de Box-Pierce « substantially improved approximation ». L'article étudie la puissance et la robustesse à la non-normalité. Il propose des modifications analogues pour les modèles **fonction de transfert-bruit**.

**FAIT (SM-LB).**
- Le statsmodels courant calcule Ljung-Box et Box-Pierce avec dof = retards − `model_df`, où `model_df` vaut « usually p+q » pour un ARMA.
- La docstring indique que Ljung-Box « has better finite-sample properties ».
- Le nombre de retards par défaut est min(10, nobs // 5).

**FAIT (DW50, résumé).** Le test de Durbin-Watson porte sur l'indépendance des erreurs d'une régression par moindres carrés, à partir des résidus. Le résumé précise que les tests usuels d'indépendance ne s'appliquent pas directement aux résidus estimés. Les bornes sont tabulées dans la partie II.

**FAIT (MW-RA ; MW-RES).**
- Dans la boîte à outils de Ljung, les résidus sont les **erreurs de prédiction à un pas**.
- La région de confiance du graphe `corr` est calculée en supposant e blanc et indépendant de u, à 99 % (2,58 écarts-types) pour `resid`.
- **Pour les modèles OE et IV**, il faut s'assurer de l'indépendance entre e et u, et « pay less attention to the results of the whiteness of e », car l'objet de la modélisation est G et non H.

**FAIT (SL19, p. 32).** Ces tests sont des tests de moments d'ordre 2 qui « reveal only a subset of the possible problems ». Une autocorrélation détectée indique que les résidus sont colorés : un modèle de bruit linéaire réduit alors les erreurs de prédiction et peut améliorer l'efficacité de l'estimation.

**FAIT (SL19, p. 34, recommandations).**
- « in many problems, structural model errors that are below a user defined level can be tolerated, even if these errors are clearly detected in the model validation step ».
- Même page : « Validating of a model is a rather subjective and pragmatic problem ».

**INTERPRÉTATION, appliquée au moteur.**
1. Le moteur applique un **test de blancheur à des résidus OE**. D'après MW-RA, c'est précisément le cas où ce test compte le moins pour juger G. S'il échoue, cela peut traduire une erreur de structure dans G, ou une perturbation colorée sans erreur sur G (SL19, S17).
2. Le test à un retard ne voit qu'une forme de dépendance. Un portemanteau Ljung-Box à K retards est plus général. Pour un FOPDT OE, la correction des degrés de liberté (`model_df`) n'est pas établie par les sources lues : les sources lues la donnent pour des modèles ARMA ou fonction de transfert-bruit, pas pour un modèle sans modèle de bruit.
3. La « faiblesse » observée dans F-LOT9-1 n'est **pas un défaut de puissance, mais un excès de puissance** vis-à-vis d'une hypothèse sans intérêt pour la commande. L'hypothèse nulle « aucune erreur de structure » est fausse : le procédé est d'ordre 4 et non linéaire. Le test convergent la rejette donc avec une probabilité qui tend vers 1 quand N croît, même si l'écart n'a pas de conséquence sur la commande (voir le calcul du §0).
   - C'est la distinction entre signification statistique et pertinence pratique, que SL19 (p. 34) formule explicitement.
   - Je n'ai trouvé aucune source lue qui chiffre la puissance de ces tests contre une erreur de structure donnée. LB78 en traite, mais seul son résumé a été lu.

**LIMITE.** Aucune page de Ljung (1999, §16.6) ni de Söderström & Stoica n'a été lue. La distribution exacte de r̂(1) pour des résidus OE après ajustement non linéaire de trois paramètres n'est pas établie ici.

### Q3. Corrélation croisée résidus/entrée : ce qu'elle détecte, et pourquoi elle dépend du type de boucle

**FAIT (MW-RA ; MW-EX).**
- Le « test d'indépendance » exige que les résidus soient non corrélés avec les entrées **passées**.
- Un pic hors de l'intervalle au retard k signifie que la part de y(t) issue de u(t−k) « is not properly described by the model ». C'est le détecteur de dynamique non modélisée.
- MW-RES : la région de 99 % suppose e blanc et indépendant de u. Les variantes `ir` et `fr` estiment la réponse de u vers les résidus, par impulsion ou par modèle FIR d'ordre élevé ; « A low magnitude in the frequency range of interest indicates a reliable model ».

**FAIT (MW-RA), boucle fermée.**
- Une corrélation aux **retards négatifs** « is not necessarily an indication of an inaccurate model » : les résidus présents influencent les entrées futures, ce qui signale une rétroaction.
- En présence de rétroaction, il faut regarder les retards **positifs**.
- DKC02 (§ sur les résultats en boucle fermée) constate la même corrélation aux retards négatifs sur des données en boucle fermée, attribuée à la rétroaction (renvoi à la documentation de Ljung de 1995).
- DKC02 rappelle aussi que la plupart des méthodes de boucle ouverte échouent en boucle fermée à cause de la corrélation entre l'entrée et le bruit de sortie, et que la méthode de l'erreur de prédiction fonctionne.

**FAIT (GCD99, résumé).** Une méthode de validation fondée sur un « unbiased model of the model error which relates the inputs to the simulation errors » a été étendue à la validation sur données de **boucle fermée**. Un même modèle peut être refusé sur des données de boucle ouverte et accepté sur des données de boucle fermée. Les ensembles validés en boucle fermée sont « better tuned towards control design ».

**INTERPRÉTATION, appliquée au moteur.**
1. En **boucle ouverte avec une entrée indépendante du bruit**, la corrélation croisée aux retards positifs est le test pertinent pour G (MW-RA). Mais avec un **échelon unique**, l'entrée n'a qu'un seul « événement ». La corrélation croisée se réduit alors, à chaque retard, à une comparaison des résidus avant et après l'instant u(t−k), et apporte peu d'information indépendante.
   - La région de confiance standard suppose de plus des résidus blancs. Si les résidus sont autocorrélés, elle est trop étroite (calcul classique de type Bartlett, **NV à la source**).
   - Une variante plus robuste consiste à estimer la réponse indicielle ou un FIR de u vers les résidus, avec une incertitude tenant compte de la couleur des résidus (`ir` de MW-RES).
2. En **boucle fermée** (données `cl_pi` du scénario, ou futurs essais en régulation), le test n'est valide qu'aux retards positifs (MW-RA). Le modèle OE identifié en boucle fermée est en outre biaisé si le bruit n'est pas modélisé (DKC02 ; FL99, notice seulement). Le moteur ne doit donc **pas** appliquer le même test sans distinguer le type de boucle.

**LIMITE.** Le texte de FL99 et de Söderström & Stoica n'a pas été lu. Les conditions exactes de validité aux retards positifs en boucle fermée ne sont rapportées que par la documentation MW-RA.

### Q4. Validation et erreur de modèle ; validation pertinente pour la commande

**FAIT (GHEL05, §1-2).**
- Le cadre de l'erreur de modèle écrit ε(t) = g_t(u^t) + w(t) : une part provient de l'entrée (l'« erreur de modèle »), une part des perturbations. Dans le cas linéaire, g_t(u) = G̃(q)u(t).
- L'article cite pour séparer ces deux effets Goodwin et al. 1992, Ljung 1999, Smith & Doyle 1992, Hakvoort & Van den Hof 1997 et Bombois et al. 2000.
- Si le modèle d'erreur a un gain β (avec un terme de décalage α) et que le régulateur conçu a un gain βc de ε vers u, la condition βc·β < 1 donne une borne de type **petit gain** (éq. 7-8).
- GHEL05 cite L99-MEM comme « Model validation and model error models », Åström Symposium, p. 15-42.

**FAIT (LG97, résumé ; LG95, résumé).** La procédure de validation classique « gives a direct measure of the model error of the model test without referring to its ensemble properties ». Plusieurs bornes d'erreur sont développées selon les hypothèses faites sur les perturbations.

**FAIT (G04).**
- §1 : quand le modèle sert à concevoir un régulateur, ce qui compte est la performance de ce régulateur sur le système vrai, « not the intrinsic quality of the model ».
  - Exemple inspiré de Skelton : Ĝ = 1/s est « absurde » comme modèle de G₀ = 1/(s+1), mais il est parfaitement acceptable pour un retour statique à grand gain.
- §6 : l'ensemble d'incertitude PE (ellipsoïde dans l'espace des paramètres) n'était facile à calculer que si le système appartient à la classe de modèles. Il a fallu inclure le biais, soit par plongement stochastique (Goodwin et al. 1992 ; Hakvoort & Van den Hof 1997), soit par validation avec un modèle d'ordre complet (Ljung 2000 ; Gevers et al. 2003).
  - BGSA01 a développé la théorie de stabilité et de performance robustes pour ces ellipsoïdes.
- §7 : il n'y a pas de consensus sur ce qu'est un ensemble « control-oriented ». Deux définitions sont proposées :
  - l'ensemble des régulateurs admissibles est grand (avec le ν-gap du pire cas de Gevers et al. 2003) ;
  - la marge et la performance du pire cas sur l'ensemble D restent proches des valeurs nominales.
- Exemple de pensée de G04 (100 essais de Monte-Carlo) : l'identification en boucle fermée donne un gain stabilisant maximal robuste de 2,04, contre 1,36 en boucle ouverte, pour un vrai maximum de 2,2.

**FAIT (SD92, résumé).** La validation est posée comme une question de **non-invalidation** : un modèle avec bruit additif et perturbations bornées en norme peut-il avoir produit les données ? La condition nécessaire se calcule par un problème d'optimisation quadratique.

**FAIT (GGN92, résumé ; HL92, résumé).**
- GGN92 : les paramètres qui quantifient l'information a priori sur le bruit et la dynamique non modélisée peuvent être estimés par maximum de vraisemblance, et servent à choisir l'ordre en tenant compte du bruit et de la sous-modélisation. L'article compare avec FPE et AIC.
- HL92 : la variance de l'erreur aléatoire peut être estimée de façon fiable sous sous-modélisation.

**Contenu non lu** : L00, RGL02, GBCSA03, BGSA01, H05, HGD96, BSGVH06 (notices seulement).

**INTERPRÉTATION.** La littérature d'identification pour la commande remplace la question « le modèle est-il vrai ? » par deux questions :
- quel ensemble de modèles est compatible avec les données ?
- le régulateur proposé est-il stable et assez performant sur tout cet ensemble ?

C'est exactement la réponse à F-LOT9-1 : l'erreur de structure est détectée, et elle doit être *bornée*, puis *confrontée* aux marges du régulateur, au lieu de servir de motif de refus.

### Q5. Défaut d'ajustement relatif à une erreur pure

**FAIT (MW-LOF).** Avec des réplicats, la somme des carrés résiduelle se décompose en un terme de défaut d'ajustement et un terme d'erreur pure. La statistique est F = CM(défaut d'ajustement) / CM(erreur pure). Elle « provides a test of bias by measuring whether the variation of the residuals is larger than the variation of the replications ». L'erreur pure est une estimation de variance « model-free » calculée sur les réplicats.

**FAIT (SL19, p. 31).** En dynamique, l'équivalent de l'erreur pure est le **niveau de bruit non paramétrique** obtenu par excitations périodiques. Si l'erreur dépasse ce plancher, des erreurs de structure sont détectées, et « The user should decide if these errors are acceptable or not ».

**DS98** : contenu non lu. Le chapitre sur les réplicats et l'erreur pure n'est pas vérifié à la source.

**INTERPRÉTATION, appliquée au moteur.**
1. Le moteur dispose d'une « erreur pure » : l'écart-type σ_b des plateaux avant l'échelon, mesuré sur n_b points.
   - La statistique F = EQM_val / σ_b² est l'analogue dynamique du défaut d'ajustement.
   - Mieux, l'estimateur σ̂_ε² = EQM_val − σ_b² mesure directement l'erreur de structure dans l'unité de la mesure, et se compare à une tolérance de procédé.
   - `maximumRmse = 2σ` en est une forme rudimentaire, sans loi de référence.
2. **Hypothèses** de cet analogue :
   - le bruit du plateau est représentatif du bruit pendant le transitoire (stationnarité, indépendance vis-à-vis du point de fonctionnement) ;
   - il est indépendant du modèle ;
   - si le bruit est coloré ou dérive, les degrés de liberté effectifs de l'EQM et de σ_b² sont inférieurs à N et n_b, et la loi F(N, n_b − 1) n'est plus valide.
   - Le plateau ne contient pas les perturbations de procédé qui surviennent pendant l'essai.
3. Ce critère ne dit pas *où* se trouve l'erreur (en basse ou haute fréquence) : il faut le compléter par une mesure de pertinence pour la commande (Q4, Q7).

**LIMITE.** Aucune source lue ne formule ce test sur la ligne de base d'un essai indiciel : c'est une transposition.

### Q6. Critères d'information et validation croisée

**FAIT (MW-MQ).** Définitions :
- FPE = det(EᵀE/N)·(1 + n_p/N)/(1 − n_p/N) ;
- AIC = N·log det(EᵀE/N) + 2n_p + N(n_y·log 2π + 1) ;
- AICc = AIC + 2n_p(n_p+1)/(N − n_p − 1), « often more reliable » quand N est petit ;
- BIC = N·log det(EᵀE/N) + N(n_y·log 2π + 1) + n_p·log N.

Ils sont calculés sur l'échantillon d'**estimation** et servent à choisir le meilleur compromis entre précision et complexité. Pour les modèles OE (`oe`, `tfest`), erreur de prédiction et erreur de simulation coïncident (option Focus).

**FAIT (SL19, p. 30-31).** La validation croisée consiste à simuler (ou prédire à k pas) le modèle sur des données neuves. Pour des modèles intégrateurs ou destinés à la commande, la prédiction à k pas « may be more revealing ».

**FAIT (GGN92, résumé).** Les critères FPE et AIC sont comparés à un choix d'ordre qui tient compte explicitement de la sous-modélisation.

**FAIT (SL19, p. 92 ; HL92, résumé).** Sous erreur de structure, les variances théoriques sont sous-estimées. Les hypothèses des critères fondés sur la vraisemblance gaussienne à résidus i.i.d. ne tiennent donc plus.

**INTERPRÉTATION.**
1. AIC, AICc, BIC et FPE **classent** des modèles. Ils ne garantissent ni l'adéquation absolue, ni une propriété de commande. Un modèle d'AIC minimal peut avoir une erreur concentrée près de la pulsation de coupure.
2. Le test de comparaison du moteur, F(n, n) sur un holdout commun, suppose deux échantillons de n résidus indépendants entre eux et dans le temps. Or :
   - les deux modèles sont évalués sur **les mêmes** points, donc les EQM sont corrélées ;
   - les résidus sont autocorrélés (r̂(1) = 0,42 dans F-LOT9-1), donc le nombre effectif de degrés de liberté est bien inférieur à n.

   Les deux effets jouent dans des sens différents, et la taille réelle du test **n'est pas connue**. Une étude de Monte-Carlo est nécessaire avant de s'y fier. Aucune source lue ne valide cette construction.
3. Le holdout du moteur, un seul échelon, est une validation croisée à un seul pli. Il ne couvre qu'une amplitude et un sens. SL19 (p. 33-34) insiste sur la couverture du domaine d'usage, ce qui pèse lourd pour un procédé non linéaire.

### Q7. Précision de modèle requise pour un PI/PID robuste (SIMC, IMC)

**FAIT (S03, p. 297, §4.1.1 et tableau 2).** Avec τc = θ, l'erreur de retard admissible est Δθ/θ = PM/(ω_c·θ) :
- pour τ1 ≤ 8θ (τI = τ1) : GM = 3,14, PM = 61,4°, Ms = 1,59, et **Δθ/θ = 2,14**. Le système ne devient instable que si le retard passe de θ à 3,14θ ;
- pour l'intégrateur : GM = 2,96, PM = 46,9°, Ms = 1,70, et Δθ/θ = 1,59.

S03 retient Ms comme mesure de robustesse : Ms < 1,7 garantit GM > 2,43 et PM > 34,2° (référence [2] de l'article).

**FAIT (S03, p. 307, annexe).** « For control purposes » :
- les approximations qui donnent un gain trop élevé sont « safe », car elles augmentent la marge de gain ;
- celles qui donnent trop de phase négative sont « safe », car elles augmentent la marge de phase.

L'approximation 1 est « always safe » ; l'approximation 2 n'est « never safe ». La règle de la moitié et la réduction de modèle visent donc **délibérément** une erreur de structure orientée du côté sûr.

**FAIT (S03, p. 306, §6.6).** τc = θ donne des réglages robustes (Ms d'environ 1,6 à 1,7) et « somewhat conservative ».

**FAIT (RMS86, p. 253, éq. 16-19).**
- Le système reste stable pour une erreur de retard δ < PM/ω_c.
- Ms (M) seul n'est qu'une indication « qualitative » de robustesse ; l'incertitude admissible sur un paramètre ne se déduit de M que si la bande passante est connue.
- L'éq. 19 illustre le compromis performance/robustesse.

**FAIT (RMS86, p. 256, éq. 59 et texte).**
- La stabilité robuste exige que le filtre soit petit là où l'écart procédé/modèle est grand, et la plage admissible de ε (le λ de l'IMC) est limitée par cet écart.
- « the closed-loop bandwidth can never be larger than the bandwidth over which the process model is valid ».
- Les modèles de procédé usuels suffisent généralement pour fixer 1/ε au moins égal à la bande passante en boucle ouverte.

**FAIT (GR18, chap. 5, p. 126-129).**
- Un PID ne devient instable que pour des erreurs de retard **positives**, alors qu'un prédicteur de Smith réglé serré peut devenir instable pour des erreurs des deux signes.
- Pour des erreurs combinées de gain et de retard, le critère de stabilité robuste ‖T·l_I‖ < 1 utilise la borne exacte de l'erreur multiplicative (Lundström 1994), approchée par w_I(s) = ((1 + r_k/2)θ_max s + r_k)/((θ_max/2)s + 1). La simplification |T(jω)| < 1/2 pour ω > π/θ_max est donnée.
- Les marges Ms_Δ et Mt_Δ de LH09 sont définies comme le pire cas de ‖S‖∞ et ‖T‖∞ sur un intervalle d'erreur de retard.

**INTERPRÉTATION.**
1. Les sources lues mesurent la précision requise en **marges** : erreur de retard admissible ≈ 2θ pour SIMC τc = θ, et erreur multiplicative bornée par 1/|T| autour de la coupure. Elles ne la mesurent pas en blancheur des résidus.
2. L'erreur de structure d'un FOPDT issu d'un procédé d'ordre élevé est **attendue et voulue** par la méthode SIMC (règle de la moitié, p. 307). Refuser un FOPDT parce que ses résidus révèlent cette erreur est **contraire à la logique de la méthode** que le moteur applique ensuite.
3. Le critère pertinent porte sur l'erreur de modèle **dans la bande ω ≈ 1/τc à 1/θ**, comparée à 1/|T(jω)|. Le constat de F-LOT9-1 (Ms ≤ 1,7 mesuré sur le procédé vrai) est cohérent avec ce point de vue.

**LIMITE.** Les valeurs du tableau 2 de S03 supposent un procédé FOPDT ou intégrateur *exact*, dont seul le retard varie. Elles ne couvrent ni l'erreur de gain simultanée, ni une dynamique d'ordre élevé arbitraire, ni l'échantillonnage (voir E-11 du référentiel). Åström & Hägglund (2006) n'ont pas été lus.

### Q8. Jeux de données réels ouverts

L'état détaillé des licences figure dans `docs/science/licences/jeux-de-donnees.md` (26-27/11/2026). Les deux jeux Manipal ci-dessous sont nouveaux.

| Jeu | Licence | Niveau | Intérêt pour valider un critère | Embarquement |
|---|---|---|---|---|
| **TCLab Take-Home Labs** (Goubej, Hedengren, Sukovatý, 2026), DOI 10.5281/zenodo.24286966 v1.1 | CC BY 4.0 (`license.id`, relu le 27/11) | V (métadonnées Zenodo) | Thermique réel, boucle ouverte SISO **avec données de validation de modèle**, et boucle fermée MIMO. Déjà extrait dans `tests/reference/tclab_cc_by` | **Oui**, fichiers d'essais seulement (attribution, mention des modifications). Exclure les fichiers d'enquête et de notes |
| **InTaSet** (quatre cuves), DOI 10.5281/zenodo.17652851 | CC BY 4.0 | V (Zenodo) | Niveau, non linéaire, boucle ouverte **et** fermée, découpage apprentissage/test : pour tester Q3 selon le type de boucle | Oui (attribution) |
| **CSTR pilote, PRBS multiniveau** (Indiran, Manipal), DOI 10.5281/zenodo.22753301 | CC BY 4.0 | V (Zenodo, `license.id`) ; **fichier non ouvert** | Température de réacteur, quatre actionneurs excités simultanément : excitation riche, donc corrélation croisée exploitable | **Après inspection** du fichier `.xlsx` (contenu, unités, provenance) |
| **Colonne à distiller pilote** (Indiran, Yadav, Manipal), DOI 10.5281/zenodo.22763516 | CC BY 4.0 | V (Zenodo) ; **fichier non ouvert** | Températures de plateaux, 2×2, boucle ouverte | Après inspection |
| **PRONTO** (écoulement polyphasique), DOI 10.5281/zenodo.1341583 | CC BY 4.0 | V (Zenodo, relevé antérieur) | Pression et débit industriels | Oui, extraits seulement |
| **NAWI** (traitement de l'eau) | CC BY 4.0 en source secondaire | V-sec | Actionneurs et capteurs réels | Après confirmation sur OpenEI (hôte refusé) |
| **Cascaded Tanks** (Nonlinear Benchmark), DOI 10.4121/12960104 | CC BY-SA 4.0 (V-sec) | V-sec | Niveau non linéaire classique | **Non** sans avis juridique (ShareAlike) ; test interne seulement |
| **DaISy** | Aucune licence (citation demandée) | NT | Procédés industriels variés | **Non** ; test interne seulement |
| Autres jeux Nonlinear Benchmark, ISDB, SACAC | Aucune licence trouvée | NT | — | Non |

**LIMITE.** Aucun de ces jeux ne fournit le *procédé vrai*. Ils permettent de mesurer des taux d'acceptation et la cohérence entre essais, mais pas des taux de faux positifs absolus. Seul un oracle synthétique le permet, ou un banc dont on maîtrise la dynamique, par exemple un TCLab avec un essai répété et des excitations variées (SL19, p. 92 : répétitions).

---

## (c) Conséquences pour le critère de validation de PID : candidats

Les risques ci-dessous sont **attendus d'après la littérature lue ou d'après mon raisonnement** (indiqué « interp. »). **Aucun n'a été mesuré.** Vocabulaire employé :
- un **faux refus** est le refus d'un modèle qui aurait donné un régulateur adéquat sur le procédé vrai ;
- une **fausse acceptation** est l'acceptation d'un modèle qui conduit à un régulateur inadéquat (instable, ou Ms au-delà de la cible).

### C1. Blancheur à un retard (actuel)
- **Hypothèses** : résidus d'un modèle de structure exacte, bruit **blanc** indépendant de u, N grand.
- **Détecte** toute erreur lisse dont la part de variance dépasse environ 2,576/(ρ_ε√N) (interp., §0).
- **Rate** :
  - les dépendances aux autres retards ;
  - les non-linéarités sans signature d'ordre 2 (SL19, p. 32) ;
  - surtout, la *conséquence* pour la commande.
- **Risques** :
  - faux refus **élevé** à fort rapport signal/bruit et N grand, ce qui est le cas de F-LOT9-1 (interp.) ;
  - faux refus aussi sous perturbation colorée avec la structure exacte (SL19, S17 ; MW-RA : « pay less attention » pour OE) ;
  - fausse acceptation faible pour les erreurs lisses, mais possible pour une erreur de signe alterné.
- **À tester** : taux de refus en fonction de N, du rapport signal/bruit, de l'ordre vrai, de la couleur du bruit, comparé à l'oracle Ms (procédé vrai).
- **Statut proposé (interp.)** : **diagnostic**, pas critère bloquant.

### C2. Portemanteau Ljung-Box à K retards
- **Hypothèses** : comme C1. Les degrés de liberté corrigés ne sont établis que pour les modèles ARMA ou fonction de transfert-bruit (BP70, LB78 résumés ; SM-LB). La correction pour un OE à trois paramètres **n'est pas établie**.
- **Détecte** une coloration plus générale que C1.
- **Rate** la pertinence pour la commande.
- **Risques** : même faux refus que C1, voire plus (interp.).
- **À tester** : la taille réelle sous la structure exacte et un bruit blanc, avec `model_df` = 0 ou 3.
- **Statut proposé** : diagnostic. Inutile comme critère bloquant si C1 est rétrogradé.

### C3. Corrélation croisée résidus/entrée (boucle ouverte, retards positifs) ou modèle d'erreur FIR de u vers les résidus
- **Hypothèses** :
  - boucle ouverte, entrée indépendante du bruit (MW-RA) ;
  - bande de confiance fondée sur e blanc (MW-RES), donc à corriger si les résidus sont colorés ;
  - **excitation suffisante** : un échelon unique est pauvre (interp.).
- **Détecte** la dynamique non modélisée *liée à l'entrée*, à distinguer de la perturbation colorée. C'est la séparation ε = G̃u + w (GHEL05).
- **Rate** :
  - les non-linéarités d'ordre élevé ;
  - les erreurs hors de la plage d'amplitude testée (SL19, p. 33-34).
  - En boucle fermée, les retards négatifs sont trompeurs (MW-RA ; DKC02).
- **Risques** :
  - faux refus réduit par rapport à C1 sous perturbation colorée (interp.) ;
  - reste élevé à fort rapport signal/bruit si l'on teste seulement « G̃ = 0 » (même raisonnement qu'en Q2) ;
  - en boucle fermée, faux refus aux retards négatifs si l'on ne les exclut pas.
- **À tester** : séparation erreur de structure / perturbation colorée sur des scénarios synthétiques ; dépendance au type d'entrée (échelon, PRBS) ; comportement sur `cl_pi` et sur InTaSet en boucle fermée.
- **Statut proposé** : **diagnostic de structure**, et surtout **source de l'estimation de G̃** pour C5.

### C4. Défaut d'ajustement relatif à l'erreur pure du plateau
- **Statistique** : σ̂_ε² = EQM_val − σ_b², ou F = EQM_val/σ_b², avec des degrés de liberté effectifs.
- **Hypothèses** : le bruit du plateau représente le bruit de l'essai (stationnaire, indépendant du point de fonctionnement) ; pas de dérive ; correction des degrés de liberté si le bruit est coloré (interp. ; MW-LOF pour le principe).
- **Détecte** la taille *absolue* de l'erreur de structure, dans l'unité de la mesure, comparable à une tolérance déclarée (SL19, p. 31 et 34 : « below a user defined level can be tolerated »).
- **Rate** :
  - la *forme* fréquentielle de l'erreur ;
  - une perturbation de procédé survenue pendant l'essai, qui sera comptée comme erreur de structure.
- **Risques** :
  - faux refus si une perturbation survient pendant l'essai ou si le plateau est trop court ;
  - fausse acceptation si l'erreur est petite en énergie mais concentrée près de la coupure (interp., RMS86 p. 256).
- **À tester** :
  - biais et variance de σ̂_ε selon la longueur du plateau ;
  - effet d'une dérive ;
  - lien entre σ̂_ε et la dégradation réelle de Ms et de l'IAE.
- **Statut proposé** : **candidat fort** pour remplacer le rôle bloquant de C1, avec une tolérance déclarée par l'humain.

### C5. Robustesse du régulateur face à l'erreur de modèle estimée (petit gain)
- **Principe** : estimer une borne de l'erreur multiplicative l(ω) à partir du modèle d'erreur (C3, ou modèles d'ordre supérieur ajustés sur la validation), puis vérifier |T(jω)|·l(jω) < 1 avec une marge, pour le régulateur effectivement proposé (GHEL05 éq. 7-8 ; GR18 éq. 5.19-5.23 ; RMS86 éq. 59).
- **Hypothèses** :
  - erreur linéaire ou à gain borné dans la plage d'amplitude testée (GHEL05) ;
  - estimation fiable de G̃ dans la bande de coupure ;
  - incertitude de G̃ prise en compte : la borne supérieure de confiance, et non l'estimation ponctuelle.
- **Détecte** précisément ce qui compte pour la commande (G04 §1 et §7 ; S03 p. 307 : direction « sûre » de l'erreur).
- **Rate** :
  - les non-linéarités hors du domaine testé ;
  - les erreurs que l'excitation n'a pas révélées : un échelon unique excite peu les hautes fréquences (interp.).
- **Risques** :
  - fausse acceptation si G̃ est sous-estimé, par exemple faute d'excitation ou si σ²(JᵀJ)⁻¹ sous-estime l'incertitude (SL19, p. 92 ; HL92) ;
  - faux refus si la borne est très conservatrice (G04 §6 : certaines bornes « very conservative »).
- **À tester** :
  - sur des scénarios synthétiques, comparer le verdict à |T₀·l₀| calculé avec le procédé vrai ;
  - couverture des bornes de G̃ par Monte-Carlo.
- **Statut proposé** : **candidat principal** à étudier. C'est un critère qui porte sur la paire (modèle, régulateur), et non sur le modèle seul.

### C6. Robustesse sur un ensemble de modèles compatibles avec les données (pire cas)
- **Principe** : construire un ensemble D (FOPDT dans leur région de confiance **élargie** pour l'erreur de structure, plus SOPDT et modèles d'ordre supérieur non invalidés), puis exiger Ms du pire cas ≤ Ms_max et la stabilité sur tout D (G04 §7, définition par la marge du pire cas ; BGSA01 et GBCSA03, notices ; SD92, non-invalidation). On peut ajouter un intervalle d'erreur de retard de type Ms_Δ (LH09 via GR18 éq. 5.24).
- **Hypothèses** :
  - D contient le procédé vrai avec une probabilité contrôlée ;
  - SL19 (p. 34, p. 92) avertit qu'en présence d'erreur de structure les bornes théoriques sous-estiment la variabilité : il faut des répétitions ou un élargissement.
- **Détecte** les modèles dont l'incertitude, structure comprise, rend le réglage dangereux.
- **Rate** les dynamiques absentes de toutes les familles de D.
- **Risques** :
  - fausse acceptation si D est trop étroit (covariance classique sous sous-modélisation) ;
  - faux refus si D est trop large, car le réglage devient alors très prudent : c'est plutôt une *détente* du réglage qu'un refus (interp.).
- **À tester** :
  - couverture de D sur l'oracle synthétique (ordre 1 à 4, non-linéarité, rapport signal/bruit) ;
  - écart entre le Ms du pire cas sur D et le Ms vrai ;
  - effet de l'élargissement.
- **Statut proposé** : **candidat principal**, complémentaire de C5. Il est plus simple à mettre en œuvre avec les familles déjà présentes dans le moteur.

### C7. Marge d'erreur de retard et de gain contre l'incertitude identifiée (forme simplifiée de C6)
- **Principe** : comparer l'intervalle de retard identifié (`delayIntervalSeconds`, NC-03) et l'incertitude de gain aux marges du régulateur : Δθ/θ admissible = PM/(ω_c·θ) (S03 p. 297 ; RMS86 éq. 17), et marge de gain.
- **Hypothèses** : procédé proche d'un FOPDT ; erreurs de gain et de retard dominantes ; borne combinée par Lundström (GR18, éq. 5.20, non lu à la source).
- **Détecte** les identifications dont l'incertitude de retard approche la marge, par exemple un FOPDT très incertain.
- **Rate** les dynamiques d'ordre élevé hors du couple (gain, retard).
- **Risques** : fausse acceptation si l'erreur de structure n'est pas un simple retard (interp.).
- **À tester** : mêmes scénarios que C6.
- **Statut proposé** : garde-fou bon marché, en complément.

### C8. Comparaison FOPDT/SOPDT par F(n, n) sur le holdout (actuel)
- **Hypothèses** : résidus indépendants, échantillons indépendants. Elles sont **violées** : mêmes points, résidus autocorrélés (Q6, interp.).
- **Risques** : taille inconnue.
- **À tester** : taille et puissance par Monte-Carlo, sous un procédé FOPDT vrai avec bruit blanc puis coloré.
- **Statut proposé** : à requalifier avant tout usage décisionnel. Alternative : comparer les modèles par leur effet sur la commande (C5 et C6), comme le suggèrent G04 §1 et S03 p. 307.

### Protocole expérimental proposé (aucune exécution faite)
1. **Oracle synthétique** (générateur indépendant de `engine/`, comme `scenario_pression_pompe`). Grille de facteurs :
   - ordre vrai 1 à 4 ;
   - non-linéarité (aucune, statique, dépendante du point de fonctionnement) ;
   - bruit blanc, AR(1) ou dérive lente ;
   - rapport signal/bruit de 5 à 200 ;
   - N de 200 à 5000 ;
   - échelon ou PRBS ;
   - boucle ouverte ou fermée.
2. **Vérité de terrain pour la commande** : pour chaque modèle identifié, appliquer le régulateur proposé au **procédé vrai**. Déclarer « adéquat » si la boucle est stable, si Ms_vrai ≤ Ms_cible + δ et si la dégradation de l'IAE est ≤ x %. δ et x sont à fixer **avant** l'exécution.
3. **Mesures** : pour chaque critère C1 à C8, taux de faux refus et de fausses acceptations par cellule de la grille ; courbe ROC en fonction du seuil ; graines fixées.
4. **Données réelles** (sans vérité de terrain) : cohérence des verdicts entre essais répétés et entre sens d'échelon, sur TCLab CC BY puis InTaSet ; CSTR et distillation Manipal après inspection.
5. **Couverture des incertitudes** : comparer la covariance théorique à la dispersion observée sur des répétitions (SL19, p. 34 et p. 92). Ce contrôle conditionne C5 et C6.

---

## (d) Non lu / à obtenir

| Source | Raison | Ce qu'elle trancherait | Accès suggéré |
|---|---|---|---|
| Ljung 1999, *System Identification*, §16.6 (et chap. 9) | Livre, pas de texte libre | Énoncé exact des tests d'autocorrélation et d'indépendance, distribution des résidus OE, corrélation croisée en boucle fermée | Bibliothèque |
| L99-MEM (Åström Symposium, p. 15-42) | DiVA et urn.kb.se refusés | Construction du modèle d'erreur et de sa région de confiance, base de C3 et C5 | Autoriser liu.diva-portal.org et urn.kb.se |
| L00 (SYSID 2000), RGL02, LG97 (texte) | DiVA refusé, ScienceDirect et IEEE refusés | Bornes d'erreur issues de la validation standard | Idem |
| GBCSA03 (Parties I et II), BGSA01, BSGVH06 | perso.uclouvain.be et ScienceDirect refusés | Validation pour la commande et du régulateur sur l'ensemble PE, base de C6 | Autoriser perso.uclouvain.be (copies de l'auteur signalées par la recherche) |
| H05, HGD96, G05, H09 | Éditeurs refusés | Identification pour la commande, plan d'expérience | Accès institutionnel |
| SD92 | research-collection.ethz.ch refusé | Non-invalidation avec perturbations bornées | Autoriser www.research-collection.ethz.ch |
| BV86 | eprints.whiterose.ac.uk refusé | Tests de corrélation non linéaires | Autoriser eprints.whiterose.ac.uk |
| BP70, LB78, DW50, P71 (texte) | JSTOR, OUP et T&F refusés | Correction des degrés de liberté, puissance, cas fonction de transfert-bruit | Accès institutionnel |
| DS98 (chapitre sur l'erreur pure) | Wiley refusé | Énoncé du test de défaut d'ajustement et conditions | Bibliothèque ; ou autoriser www.itl.nist.gov (e-Handbook, sections de défaut d'ajustement et de Ljung-Box) |
| Söderström & Stoica 1989 | Livre | Tests de corrélation, conditions en boucle fermée | Bibliothèque |
| Åström & Hägglund 2006 ; Morari & Zafiriou 1989 ; Lundström 1994 ; LH09 (texte) | Livres ou éditeurs refusés | Exigences de précision pour PID, borne d'erreur multiplicative | Bibliothèque ; IEEE |
| Fichiers des jeux CSTR et distillation Manipal | Non ouverts | Contenu, unités, réalité expérimentale | zenodo.org est accessible : inspection à faire avant tout emploi |

**Hôtes dont l'autorisation débloquerait les sources clés** (par priorité) : liu.diva-portal.org et urn.kb.se (Ljung, en accès ouvert) ; perso.uclouvain.be (Gevers, Bombois) ; www.itl.nist.gov ; www.research-collection.ethz.ch ; eprints.whiterose.ac.uk ; www.sciencedirect.com et ieeexplore.ieee.org (accès institutionnel requis de toute façon).

---

## Synthèse

**FAIT.**
- Les sources lues (SL19, MW-RA, G04, S03, RMS86, GR18) convergent sur trois points :
  - un test de blancheur est un test d'*adéquation statistique* ;
  - pour un modèle OE, c'est le test d'indépendance résidus/entrée qui juge G ;
  - pour la commande, ce qui compte est l'erreur dans la bande de coupure, comparée aux marges du régulateur. SIMC accepte par construction une erreur de structure « sûre ».

**INTERPRÉTATION.**
- Le refus de F-LOT9-1 est la conséquence attendue d'un test convergent contre une hypothèse nulle fausse mais sans enjeu pour la commande.
- Les candidats à étudier en priorité sont C4 (taille de l'erreur relative au bruit), C5 (petit gain sur l'erreur estimée) et C6 (pire cas sur un ensemble compatible). C1 et C2 restent des diagnostics, et C8 doit être requalifié.

**LIMITE.**
- Aucune source primaire de Ljung, de Söderström & Stoica ni de l'école de Louvain n'a été lue.
- Aucune mesure de faux refus ou de fausse acceptation n'a été faite : tout reste à démontrer par le protocole ci-dessus.
