> **Annexe du LOT 0 — rapport d'audit indépendant (sous-agent, lecture seule), révision auditée c7915cd.** Les constats ont été vérifiés par échantillonnage par le responsable du lot (voir 00-synthese-audit.md, §1) ; tout constat non cité dans la synthèse reste à confirmer avant d'être retenu.

# LOT 0 — E. Référentiel bibliographique et état de l'art (ORIAS)

Documentaliste scientifique indépendant. Rédigé le 26/11/2026. Dépôt consulté en lecture seule : `/home/user/mkr` (le dépôt s'appelle **MKR** dans son code et sa documentation. Le nom « ORIAS » n'y apparaît pas : je le signale sans rien en conclure).

---

## 0. Méthode et limites d'accès (à lire avant tout)

**Accès réseau réellement disponible pendant ce travail :**
- **Consultation directe possible** : `pypi.org` (API JSON), `github.com` (clone git superficiel en lecture), le dépôt local.
- **Consultation directe impossible** (proxy de session : `getaddrinfo ENOTFOUND` / HTTP 403) : skoge.folk.ntnu.no, api.crossref.org, doi.org, sciencedirect.com, ieeexplore.ieee.org, openalex.org, apmonitor.com, homes.esat.kuleuven.be (DaISy), nonlinearbenchmark.org, controlstation.com, valmet.com, mathworks.com, support.industry.siemens.com, iec.ch, webstore.iec.ch, wikipedia, archive.org, zenodo.org, ntnuopen.ntnu.no. Pour les consulter, un humain doit autoriser ces hôtes dans les réglages réseau de la session, ou faire les vérifications lui-même.
- **Moteur de recherche (WebSearch)** : disponible. Il renvoie des titres, des URL et une synthèse automatique des pages, **sans que j'aie lu la page elle-même**.

**Niveaux de vérification employés (stricts) :**
- **V — vérifiée (source consultée : URL)** : j'ai lu moi-même la source primaire (fichier LICENSE, pyproject, métadonnées PyPI, historique git, fichier du dépôt).
- **NV-c — non vérifiée (à confirmer), corroborée** : la notice bibliographique concorde entre plusieurs résultats de recherche indépendants (catalogues de bibliothèques, dépôts institutionnels, index), mais je n'ai pas consulté la page de l'éditeur ni la notice DOI. **Elle reste à confirmer par un humain.**
- **NV — non vérifiée (à confirmer)** : une seule source secondaire, des sources contradictoires, ou un élément manquant (DOI, pages).

Conséquence : **aucune référence bibliographique de ce document n'atteint le niveau V**, faute d'accès aux éditeurs et à Crossref. Seules les fiches logicielles (§2) et les éléments tirés du dépôt le sont. Aucune équation n'est reproduite d'après une source non lue, sauf les relations de Broïda : je les ai **recalculées** moi-même (§1.8), et le calcul est fourni.

---

## 1. Références scientifiques

### 1.1 Ljung — *System Identification: Theory for the User*
- **Auteur** : Lennart Ljung. **Éditeur** : Prentice Hall PTR, Upper Saddle River (NJ). **2ᵉ édition** : © 1999 (publiée fin 1998 selon Pearson). **ISBN** de l'édition papier le plus cité : 0-13-656695-2 / 978-0-13-656695-3. Il existe d'autres ISBN : livre numérique 978-0-13-244110-0 ; Pearson 978-0-13-244053-0 ; O'Reilly 978-0-13-244193-3. Environ 672 pages selon un catalogue. 1ʳᵉ édition : 1987, non recherchée.
- **Niveau** : NV-c. Sources : https://search.worldcat.org/oclc/38884169 ; https://www.informit.com/store/system-identification-theory-for-the-user-9780136566953 ; https://pearson.com/en-us/subject-catalog/p/system-identification-theory-for-the-user/P200000000437/9780132440530
- **Apport pour ORIAS** : c'est l'ouvrage de référence de l'approche par erreur de prédiction (PEM), des structures ARX, OE, ARMAX et BJ, de la validation des modèles (analyse des résidus, simulation sur des données de validation disjointes) et de l'identification en boucle fermée. Il fonde la démarche « estimation / validation séparées » et les tests de blancheur des résidus déjà présents dans MKR (`engine/arx.py`). **Les numéros de chapitres et de pages sont à relever sur un exemplaire** : je ne les cite pas.

### 1.2 Söderström & Stoica — *System Identification*
- **Auteurs** : Torsten Söderström, Petre Stoica. **Éditeur** : Prentice Hall International (série *Systems and Control Engineering*), 1989. **ISBN** 0-13-881236-5. xxiv + 612 p.
- **Niveau** : NV-c. Sources : https://search.library.uitm.edu.my/Record/wils-132851/Description ; https://www.cambridge.org/core/journals/econometric-theory/article/system-identificationt-soderstrom-and-p-stoica-prentice-hall-international-1989/586555F5E831891F67178147691D3D11 (compte rendu dans *Econometric Theory*). Des copies auteur en libre accès circulent parfois, mais je n'en ai trouvé aucune de légitime.
- **Apport** : méthodes des moindres carrés et des variables instrumentales, consistance, excitation persistante, identifiabilité, conditions de l'identification en boucle fermée, choix de l'ordre des modèles. C'est la base théorique pour **refuser des données insuffisamment excitantes**.

### 1.3 Åström & Hägglund — *Advanced PID Control*
- **Auteurs** : Karl Johan Åström, Tore Hägglund (Lund University). **Éditeur** : ISA (Instrumentation, Systems, and Automation Society), Research Triangle Park (NC), 2006. **ISBN** 1-55617-942-1 / 978-1-55617-942-6. 13 chapitres. Une fiche Wiley-VCH mentionne une réédition en 2026 (480 p.) : non confirmée.
- **Niveau** : NV-c. Sources : https://lup.lub.lu.se/record/535630 ; https://search.worldcat.org/fr/title/advanced-pid-control/oclc/60557376 ; https://www.wiley-vch.de/en/areas-interest/engineering/advanced-pid-control-978-1-55617-942-6
- **Apport** : modèles de procédé pour le PID (FOPDT, intégrateur), réglage par loop shaping robuste avec contrainte sur Ms (règles AMIGO), pondération de consigne, filtrage de la dérivée, anti-emballement, effets des non-linéarités d'actionneur. C'est la référence pour les critères de robustesse (Ms) et de performance (IAE sur perturbation de charge).
- Article associé : **Åström & Hägglund, « Revisiting the Ziegler–Nichols step response method for PID control »**, *Journal of Process Control* 14(6), 635–650, 2004, DOI 10.1016/j.jprocont.2004.01.002. NV-c, source : https://lup.lub.lu.se/record/161428. L'ordre des auteurs diffère selon les notices. Je n'ai pas pu confirmer que l'article introduit le nom « AMIGO ».
- Article associé : **Panagopoulos, Åström & Hägglund, « Design of PI controllers based on non-convex optimization »**, *Automatica* 34(5), 585–601, 1998. **DOI non trouvé.** L'ordre des auteurs diffère selon les notices. NV, sources : https://dblp.org/rec/journals/automatica/AstromPH98.html ; https://lup.lub.lu.se/search/publication/8497601. Apport : optimisation du rejet de perturbation de charge sous contrainte de sensibilité (MIGO).

### 1.4 Åström & Murray — *Feedback Systems: An Introduction for Scientists and Engineers*
- **Auteurs** : Karl Johan Åström, Richard M. Murray. **Éditeur** : Princeton University Press. **2ᵉ édition** : 2021 (relié ISBN 978-0-691-19398-4 ; livre numérique 978-0-691-21347-7). 1ʳᵉ édition : 2008. Les auteurs diffusent une version en ligne (wiki Caltech).
- **Niveau** : NV-c. Sources : https://press.princeton.edu/node/59031 ; https://lup.lub.lu.se/record/34219d15-d2c5-4ea3-adb6-c732c4b99c83 ; https://www.cds.caltech.edu/~murray/amwiki/Second_Edition.html
- **Apport** : définitions rigoureuses des fonctions de sensibilité S et T, des marges de gain et de phase, de Ms, du critère de Nyquist et des limites fondamentales. C'est une référence pédagogique libre d'accès pour justifier les indicateurs de stabilité.

### 1.5 Skogestad — « Simple analytic rules for model reduction and PID controller tuning »
- **Auteur** : Sigurd Skogestad. ***Journal of Process Control*** **13**(4), 291–309, 2003. **DOI 10.1016/S0959-1524(02)00062-8.** **Erratum** : *J. Process Control* 14 (2004) 465, DOI 10.1016/j.jprocont.2003.09.001 (DOI distinct, à ne pas confondre avec celui de l'article). Réimpression : *Modeling, Identification and Control* (MIC), 2004 (identifiant MIC-2004-2-2) ; volume et pages exacts NV.
- **Niveau** : NV-c pour l'article ; NV pour la pagination de la réimpression MIC. Le numéro d'issue (4) est à confirmer. Sources : https://www.bohrium.com/paper-details/simple-analytic-rules-for-model-reduction-and-pid-controller-tuning/812435852343902212-2546 ; https://skoge.folk.ntnu.no/publications/2003/tuningPID/ (index de l'auteur, inaccessible depuis la session) ; https://www.mic-journal.no/ABS/MIC-2004-2-2
- **Apport** : règle de la « moitié » pour réduire un modèle à un FOPDT ou un SOPDT ; règles SIMC pour PI et PID, de paramètre unique τc (réglage recommandé τc = θ), avec un traitement spécifique des procédés intégrateurs et à constante de temps dominante. C'est la référence directe des réglages PI/PID de MKR (`docs/science/simc-pid-sopdt.md`, `pi-fopdt.md`). **L'erratum doit être lu avant de figer une implémentation.**

### 1.6 Rivera, Morari & Skogestad — « Internal Model Control: PID Controller Design »
- **Auteurs** : Daniel E. Rivera, Manfred Morari, Sigurd Skogestad. ***Industrial & Engineering Chemistry Process Design and Development*** **25**(1), 252–265, janvier 1986. **DOI 10.1021/i200032a041.**
- **Niveau** : NV-c. Sources : https://skoge.folk.ntnu.no/publications/1986/Rivera86/README.html (non consultable depuis la session, vue dans les résultats) ; https://www.bohrium.com/paper-details/internal-model-control-pid-controller-design/812298601513353217-16105
- **Apport** : montre que la synthèse IMC conduit, pour les modèles SISO usuels, à des correcteurs PID (parfois complétés d'un filtre du premier ordre). C'est le fondement du réglage « lambda » ou IMC. Limite connue, rappelée par Skogestad (2003) : réponse lente aux perturbations pour les procédés à constante de temps dominante ou intégrateurs.

### 1.7 Grimholt & Skogestad — publications identifiées comme réelles
Les quatre titres suivants sont attestés par plusieurs notices concordantes (NTNU Open, ScienceDirect, British Library). La thèse les rassemble.

| # | Référence | Niveau | Sources |
|---|---|---|---|
| G1 | C. Grimholt, S. Skogestad, « **Optimal PI-control and verification of the SIMC tuning rule** », *IFAC Conference on Advances in PID Control* (PID'12), Brescia, 28–30 mars 2012. Les actes ont paru dans *IFAC Proceedings Volumes* 45(3), mais **le DOI n'est pas confirmé**. | NV (DOI manquant) | https://skoge.folk.ntnu.no/prost/proceedings/PID-12/papers/0126.pdf (vu dans les résultats seulement) |
| G2 | C. Grimholt, S. Skogestad, « **Optimal PID-control on first order plus time delay systems & verification of the SIMC rules** », 10ᵉ IFAC DYCOPS, Mumbai, 2013, *IFAC Proceedings Volumes* 46, 265–270. DOI non relevé. | NV | https://www.sciencedirect.com/science/article/pii/S1474667015382689 |
| G3 | C. Grimholt, S. Skogestad, « **Optimal PI and PID control of first-order plus delay processes and evaluation of the original and improved SIMC rules** », ***Journal of Process Control*** **70**, 36–46, 2018, **DOI 10.1016/j.jprocont.2018.06.011**. | NV-c | https://ntnuopen.ntnu.no/ntnu-xmlui/handle/11250/2593081 ; https://eld.bl.uk/catalog/100070642276_0x000001 |
| G4 | C. Grimholt, S. Skogestad, « **Optimization of fixed-order controllers using exact gradients** », ***Journal of Process Control*** **71**, 130–138, 2018, **DOI 10.1016/j.jprocont.2018.09.001**. | NV-c | https://ntnuopen.ntnu.no/ntnu-xmlui/handle/11250/2590967 ; https://eld.bl.uk/catalog/100072757572_0x000001 |
| G5 | C. Grimholt, S. Skogestad, « **Should we forget the Smith Predictor?** », 3ʳᵈ IFAC Conference on Advances in PID Control, Gand, 9–11 mai 2018, *IFAC-PapersOnLine* 51(4), 769–774. DOI non relevé. | NV | https://www.sciencedirect.com/science/article/pii/S2405896318305007 |
| Thèse | C. Grimholt, *Optimal tuning of PID controllers and the verification of the SIMC rules*, thèse de doctorat, NTNU, 2018. La liste des publications figure à la section 1.5 selon un résumé. | NV | https://skoge.folk.ntnu.no/publications/thesis/2018_grimholt/phd-thesis-grimholt-2018.pdf |
| Chapitre associé | S. Skogestad, C. Grimholt, « The SIMC method for smooth PID controller tuning », chapitre d'ouvrage, 2012 (dans *PID Control in the Third Millennium*, Springer ; titre de l'ouvrage, pages et DOI non confirmés). | NV | https://www.researchgate.net/publication/288897834_The_SIMC_method_for_smooth_PID_controller_tuning |

**Apport pour ORIAS** :
- G3 définit un critère de performance fondé sur l'IAE pour des perturbations d'entrée et de sortie, normalisé par un réglage de référence, à niveau de robustesse fixé (Ms). Ses conclusions selon le résumé : SIMC PI est proche de l'optimum, sauf pour les procédés à retard dominant ; il propose une variante « SIMC améliorée » avec action dérivée.
- G4 optimise sous contraintes Ms et Mt avec gradients exacts : une base possible pour un **réglage de référence indépendant** dans un benchmark.
- **Je n'ai pas lu les textes intégraux** : formules et valeurs numériques sont à relever sur les PDF.

### 1.8 Broïda — identification FOPDT à deux points (28 % / 40 %)
- **Source primaire retrouvée (au niveau de la notice)** : **V. Broïda, « The determination of large time-constants by step-response extrapolation »**, ***Automatica*** **5**(5), 677–683, septembre 1969 (*brief paper*). Identifiant ScienceDirect : pii 0005109869900338. **Le DOI correspondant serait 10.1016/0005-1098(69)90033-8**, mais je l'ai déduit du pii : il est **à confirmer**. Selon le résumé ScienceDirect rapporté par la recherche, la version complète en français a paru dans *Automatisme* en mars 1969.
- **Version française** : V. Broïda, « L'extrapolation des réponses indicielles apériodiques », ***Automatisme***, t. XIV, n° 3, mars 1969, p. 105–114. Une source indique « XVI », probablement une coquille.
- **Niveau** : NV. Notices concordantes mais secondaires ; texte primaire non lu ; DOI déduit. Sources : https://www.sciencedirect.com/science/article/abs/pii/0005109869900338 ; https://stm.cairn.info/pratique-de-la-regulation-industrielle--9782100779710-page-91?lang=fr
- **Point critique** : **je n'ai pas pu vérifier que les seuils 28 % / 40 % et les coefficients 5,5 / 2,8 / 1,8 figurent tels quels dans la source primaire.** Ces valeurs sont celles de l'enseignement francophone (BTS CIRA, cours universitaires) et des références secondaires déjà citées par MKR (`docs/science/ID-07A-broida.md`).
- **Contrôle mathématique indépendant, refait par moi** : pour un FOPDT `y(t) = 1 − exp(−(t−θ)/τ)`, on a `t28 = θ + τ·ln(1/0,72) ≈ θ + 0,3285 τ` et `t40 = θ + τ·ln(1/0,60) ≈ θ + 0,5108 τ`. D'où `τ = (t40 − t28)/0,1823 ≈ 5,49 (t40 − t28)` et `θ ≈ 2,80 t28 − 1,80 t40`. Les coefficients arrondis 5,5 / 2,8 / 1,8 sont donc cohérents avec le modèle exact. C'est un **calcul**, pas une citation, et il ne remplace pas la consultation de l'article. Le code `engine/broida.py` emploie bien ces valeurs (V, source consultée : `/home/user/mkr/engine/broida.py`, lignes 57-64).

### 1.9 Critères d'information et analyse des résidus — sources standard
| Référence | Fiche | Niveau | Sources |
|---|---|---|---|
| AIC | H. Akaike, « A new look at the statistical model identification », *IEEE Transactions on Automatic Control* 19(6), 716–723, déc. 1974, **DOI 10.1109/TAC.1974.1100705** | NV-c | https://ieeexplore.ieee.org/document/1100705 |
| BIC | G. Schwarz, « Estimating the dimension of a model », *The Annals of Statistics* 6(2), 461–464, 1978, **DOI 10.1214/aos/1176344136** (libre accès Project Euclid) | NV-c | https://www.projecteuclid.org/euclid.aos/1176344136 |
| AICc | C. M. Hurvich, C.-L. Tsai, « Regression and time series model selection in small samples », *Biometrika* 76(2), 297–307, 1989, DOI 10.1093/biomet/76.2.297 (DOI attesté par une seule source) | NV | https://www.stat.berkeley.edu/~binyu/summer08/Hurvich.AICc.pdf |
| Ouvrage de synthèse | K. P. Burnham, D. R. Anderson, *Model Selection and Multimodel Inference: A Practical Information-Theoretic Approach*, 2ᵉ éd., Springer, New York, 2002, **DOI 10.1007/b97636**, ISBN 978-0-387-95364-9 | NV-c | https://link.springer.com/doi/10.1007/b97636 |
| Test de blancheur | G. M. Ljung, G. E. P. Box, « On a measure of lack of fit in time series models », *Biometrika* 65(2), 297–303, 1978, DOI 10.1093/biomet/65.2.297 (aussi 10.2307/2335207, JSTOR) | NV-c | https://svn.r-project.org/browse/R/trunk/src/library/stats/man/box.test.Rd?peg=77458 ; https://itl.nist.gov/div898/handbook/pmc/section4/pmc4481.htm |
| Analyse des résidus en identification | Ljung (1999) et Söderström & Stoica (1989) : autocorrélation des résidus et corrélation croisée résidus–entrée. Chapitres à relever. | NV-c (ouvrages) | voir §1.1–1.2 |

**Réserves d'usage** :
- AIC et BIC supposent une vraisemblance correctement spécifiée et des échantillons indépendants, ou des innovations blanches. Sur des données de procédé autocorrélées, le nombre effectif d'observations est inférieur au nombre d'échantillons.
- L'AICc a été dérivé pour la régression et les modèles AR. Son extension aux modèles à retard pur (FOPDT) est une **hypothèse à justifier**.

### 1.10 Identification en boucle fermée — sources standard
| Référence | Fiche | Niveau | Apport |
|---|---|---|---|
| Gustavsson, Ljung, Söderström 1977 | I. Gustavsson, L. Ljung, T. Söderström, « Identification of processes in closed loop—identifiability and accuracy aspects », *Automatica* 13(1), 59–75, 1977, **DOI 10.1016/0005-1098(77)90009-7** | NV-c (https://lup.lub.lu.se/record/2be44ad1-1767-46eb-8018-99363f89f087) | Identifiabilité ; comparaison des approches directe, indirecte et entrée-sortie jointe ; rôle d'une excitation externe ou d'un correcteur suffisamment complexe |
| Forssell & Ljung 1999 | U. Forssell, L. Ljung, « Closed-loop identification revisited », *Automatica* 35(7), 1215–1241, 1999, **DOI 10.1016/S0005-1098(99)00022-9** | NV-c (https://www.scilit.net/publications/1d97cf9f24050978980d67a1dcb9a527 ; https://portal.mardi4nfdi.de/wiki/Publication:1301435) | Synthèse de référence : méthodes directe, indirecte, entrée-sortie jointe, deux étages et projection. **Biais de la méthode directe quand le modèle de bruit est mal spécifié** (corrélation entre l'entrée et le bruit via la rétroaction) |
| Van den Hof & Schrama 1993 | P. M. J. Van den Hof, R. J. P. Schrama, « An indirect method for transfer function estimation from closed loop data », *Automatica* 29(6), 1523–1527, 1993, **DOI 10.1016/0005-1098(93)90015-L** | NV-c (https://researchr.org/publication/HofS93/bibtex) | **Méthode à deux étages** : estimation consistante de G indépendamment du modèle de bruit, à partir d'un signal de référence externe |
| Van den Hof 1998 | P. M. J. Van den Hof, « Closed-loop issues in system identification », *Annual Reviews in Control* 22, 173–186, 1998 ; DOI 10.1016/S1367-5788(98)00016-9 (attesté par une seule source douteuse) | NV | Revue ; https://ibl.kb.nl/article/0000000011327/22/C/6/lang/uk |

**Conséquence pour ORIAS** : identifier sur des données en boucle fermée **sans excitation externe** (consigne ou signal ajouté) et avec un correcteur linéaire simple pose un problème d'identifiabilité (Gustavsson et al. 1977). La méthode directe est biaisée si le modèle de bruit est faux (Forssell & Ljung 1999). Ces deux sources justifient de **refuser** ou de **qualifier d'expérimental** un résultat obtenu en boucle fermée, ce que MKR fait déjà (`engine/closed_loop.py`, mention « boucle fermée expérimentale » dans la documentation).

---

## 2. Bibliothèques logicielles

Toutes les données de cette section sont **V** (consultées le 26/11/2026) : métadonnées `https://pypi.org/pypi/<nom>/json`, fichiers LICENSE et pyproject lus dans un clone git superficiel de `https://github.com/<dépôt>`, et `/home/user/mkr/engine/requirements.txt`.

**Versions réellement employées par MKR** (`engine/requirements.txt`) : `numpy==2.3.5`, `scipy==1.17.0`, `openpyxl==3.1.5`, `et-xmlfile==2.0.0`, `tzdata==2025.2`.

| Bibliothèque | Licence (source) | Version employée par MKR | Dernière version PyPI (date) | Dernier commit (branche par défaut) | Domaine / hypothèses | Pertinence pour ORIAS |
|---|---|---|---|---|---|---|
| **NumPy** | 2.3.5 : classificateur « OSI Approved :: BSD License », texte « Copyright (c) 2005-2025, NumPy Developers ». La 2.5.5 déclare `BSD-3-Clause AND 0BSD AND MIT AND Zlib AND CC0-1.0` (composants embarqués) | **2.3.5** (publiée le 16/11/2025) | 2.5.5 (15/11/2026) | non relevé | Tableaux, algèbre linéaire (moindres carrés) | Socle déjà employé. La version figée a un an de retard : décision de mise à jour à prendre, sans urgence scientifique |
| **SciPy** | Classificateur « BSD License » (texte « Copyright (c) 2001-2002 Enthought, Inc. 2003, SciPy Developers ») | **1.17.0** (10/01/2026) | 1.18.1 (21/08/2026) | non relevé | Optimisation, `signal` (lsim, lti), statistiques | Déjà employé. Les fonctions utilisées par MKR n'ont pas été auditées ici |
| **python-control** | **BSD-3-Clause** (pyproject et LICENSE : Caltech, TU Delft, développeurs python-control) | non utilisé | 0.10.2 (05/07/2025), statut « 4 - Beta » | 27/10/2026 (`bfe9380`) | Analyse et synthèse LTI : tf/ss, retard approché (Padé), marges, Nyquist, simulation. Dépend de numpy ≥ 1.23, scipy ≥ 1.8, matplotlib ≥ 3.6. **Pas une boîte d'identification** | Candidat comme **calcul de contrôle indépendant** des marges, de Ms et des réponses, pour un benchmark. Article à citer selon le README : Fuller et al., « The Python Control Systems Library (python-control) », 60ᵗʰ IEEE CDC, 2021, p. 4875–4881 (DOI non relevé, NV) |
| **SIPPY** (`sippy_unipi`) | **LGPL-3.0-or-later** (LICENSE et pyproject) | non utilisé | 1.0.1 (02/09/2025), « 4 - Beta » | 05/06/2026 (`50c76fb`) | Identification linéaire en temps discret : FIR, ARX, ARMAX, ARMA, ARARX, ARARMAX, OE, BJ, GEN ; sous-espaces N4SID, MOESP, CVA, PARSIM. Critères d'information en SISO. Dépend de casadi, control, sympy, tf2ss. Université de Pise (G. Armenise, G. Pannocchia) | Utile comme **référence croisée** ARX/OE/BJ. **Attention** : la LGPL impose des obligations de redistribution (remplaçabilité de la bibliothèque) à analyser juridiquement avant toute intégration. Le nom PyPI `sippy` désigne une **autre** bibliothèque (pile SIP/VoIP, BSD), source de confusion à éviter |
| **SysIdentPy** | **BSD-3-Clause** (LICENSE : © 2019 W. Rocha, L. Pascoal, S. Oliveira, S. Martins) | non utilisé | 0.9.0 (13/06/2026), « 5 - Production/Stable » | 11/08/2026 (`76b75e4`) | Modèles NARMAX (NARX, ARX, ARMAX, etc.), sélection de structure (FROLS), réseaux neuronaux. Le pyproject borne `numpy<=2.3.5` | Orienté non linéaire et polynomial, **peu adapté** à l'identification FOPDT/SOPDT à retard continu. Éventuellement utile pour un contrôle ARX croisé. Article : Lacerda et al., *JOSS* 5(54), 2384, 2020, DOI 10.21105/joss.02384 (V : README du dépôt ; article lui-même non consulté) |
| openpyxl / et-xmlfile | MIT / MIT (PyPI) | 3.1.5 / 2.0.0 | — | — | Lecture Excel | Hors périmètre scientifique |
| tzdata | Apache-2.0 (PyPI) | 2025.2 | — | — | Fuseaux horaires | Hors périmètre scientifique |

Non relevé : l'état des correctifs de sécurité des versions figées.

---

## 3. Jeux de données publics

| Jeu | Nature des signaux et conditions | Licence / conditions d'usage | Pertinence FOPDT / SOPDT / intégrateur / boucle fermée | Niveau |
|---|---|---|---|---|
| **DaISy** (KU Leuven, ESAT/STADIUS ; éd. B. De Moor) | Base de mesures et de simulations pour l'identification, en catégories (procédés, mécanique, biomédical, environnement, thermique, etc.). Relecture « légère » ; les déposants certifient l'absence de clause de confidentialité. Article : De Moor, De Gersem, De Schutter, Favoreel, « DAISY: A database for identification of systems », *Journal A* 38(3), 4–5, sept. 1997 | **Aucune licence explicite trouvée.** Le site demande seulement une forme de citation. Usage dans un benchmark publié : **à faire confirmer** auprès du mainteneur | Variable selon le jeu. Plusieurs jeux sont multivariables. **Je ne peux pas désigner un jeu FOPDT ou en boucle fermée sans avoir lu les fiches** (site inaccessible) | NV ; https://homes.esat.kuleuven.be/~smc/daisy/ ; https://www.dcsc.tudelft.nl/~bdeschutter/pub/abs/97_70.html |
| **Nonlinear Benchmark** (nonlinearbenchmark.org) | Jeux avec découpage officiel apprentissage / test : Wiener-Hammerstein, Silverbox, Cascaded Tanks (avec débordement), EMPS, Coupled Electric Drives, etc. Exemple vérifié : Wiener-Hammerstein, 100 000 points d'apprentissage et 78 800 de test, Te = 1,953e-05 s | Le chargeur officiel `nonlinear-benchmarks` 1.0.1 (PyPI, 20/10/2025) déclare « BSD 3-Clause License » dans son champ licence, mais le classificateur dit « Other/Proprietary » (V, PyPI). **Cette licence couvre le code du chargeur, pas nécessairement les données.** Conditions d'usage des données : NV | Systèmes **non linéaires**, souvent électriques ou mécaniques, à excitation riche. Cascaded Tanks est un procédé hydraulique proche d'une dynamique de niveau. Pertinence **faible** pour un FOPDT strict ; utile pour tester le **refus** de modèles linéaires inadéquats. Pas de données en boucle fermée identifiées. Article Cascaded Tanks cité comme Schoukens & Noël 2017 (titre exact non confirmé) | V (chargeur PyPI) ; NV (données) ; https://pypi.org/project/nonlinear-benchmarks/ |
| **TCLab** (APMonitor/BYU, J. Hedengren) | Banc Arduino à 2 chauffages (transistors) et 2 thermistances. Thermique, couplé, **non linéaire** (pertes dépendantes de la température), pas typique de 1 à 5 s. Article : Park, Martin, Kelly, Hedengren, « Benchmark temperature microcontroller for process dynamics and control », *Computers & Chemical Engineering* 135, 106736, 2020, DOI 10.1016/j.compchemeng.2020.106736 (NV-c) | **Code** : `APMonitor/TCLab` et `jckantor/TCLab` sous Apache-2.0 (V : LICENSE.txt). Paquet `tclab` 1.0.0 sous Apache (PyPI, 11/09/2023). Le dépôt `APMonitor/TCLab` (dernier commit 23/10/2026) contient du code et des notebooks, **pas de jeu de mesures de référence** (V : arbre git). **Données** publiées sur apmonitor.com (ex. `tclab_dyn_data2.txt`) : **licence non trouvée** (site inaccessible) | FOPDT approché en SISO si une seule entrée bouge, avec plateaux stables. Très utile pour le **refus justifié** (déjà démontré dans `docs/validation/donnees-reelles-tclab.md`). Les essais en boucle fermée PID d'APMonitor existent selon la documentation pédagogique (NV) | V (code) ; NV (données) |
| **Dépôt `reference-tclab` du projet** | **Non trouvé** : aucune occurrence de « reference-tclab » dans `/home/user/mkr` (V, recherche dans .md, .py et .json). Le dépôt contient `tests/reference/tclab_dyn_data2_extract.csv` (121 points, transcription non recoupée, licence non vérifiée selon `tests/reference/README-tclab.md`) | — | — | V (absence dans le dépôt) |
| Autres (pistes NV) | Base Jelali & Huang : boucles industrielles avec et sans frottement de vanne, données et logiciel MATLAB associés à l'ouvrage (§6), sur le site de l'Université d'Alberta (https://sites.ualberta.ca/~bhuang/Stiction-Book.htm). **Licence à vérifier** ; pertinence **forte pour les diagnostics** de vanne en boucle fermée | — | — | NV |

**Réserve générale** : aucun jeu public identifié ne fournit de **vérité terrain** sur les paramètres FOPDT, SOPDT ou intégrateur d'un procédé industriel réel. Un benchmark de précision d'identification devra donc combiner :
- des **simulations à vérité connue** (bruit et quantification maîtrisés) ;
- des **données réelles**, pour la seule capacité de prédiction sur une période de validation disjointe.

---

## 4. Logiciels industriels (documentation publique uniquement — aucune comparaison avec ORIAS, aucune équivalence)

Tous ces éléments sont **NV** : les sites des éditeurs étaient inaccessibles depuis la session, et le contenu provient des résultats de recherche pointant vers les pages officielles listées.

### 4.1 Control Station — LOOP-PRO Tuner, technologie « NSS »
- Selon l'éditeur, la modélisation **Non-Steady State (NSS)** « élimine le besoin d'un régime permanent avant le début de l'essai » et « n'utilise pas un point de donnée spécifique ou moyen comme valeur connue ». Une page produit mentionne des données en **boucle ouverte et en boucle fermée** (simple puce, sans explication technique). Fonctions listées pour Tuner : réponse simulée à la consigne, réponse simulée à une perturbation, analyse de stabilité.
- Brevet : l'éditeur annonce un dépôt à l'USPTO en juin 2008. Une demande publiée **US 2011/0093247** (inventeur B. Beauregard, cessionnaire Control Station) décrit la modélisation d'un procédé à partir de valeurs hors régime permanent de la variable de procédé. Statut de délivrance et date de dépôt exacte **non confirmés** (incohérence 2008 / 2009 entre les sources).
- Yokogawa indique que csTuner intègre la technologie NSS de Control Station.
- **L'algorithme n'est pas publiquement documenté à ma connaissance. Je ne prétends pas le connaître.**
- URL : https://controlstation.com/non-steady-state-modeling-nss-modeling/ ; https://controlstation.com/?p=34817 ; https://controlstation.com/august-19-2008-control-station-files-patent-for-unique-process-modeling-technology/ ; https://patents.justia.com/assignee/control-station-inc ; https://yokogawa.com/us/solutions/products-platforms/control-system/distributed-control-systems-dcs/advanced-solutions/pid-loop-auto-tuning-cstuner

### 4.2 Valmet — ExperTune PID Loop Optimizer (et PlantTriage)
- D'après la brochure et les pages produit : sélection automatique du « meilleur modèle » ajusté aux données ; simulation de la réponse ; vue de **robustesse** (compromis réglage / stabilité) ; technologie « Active Model Capture » de PlantTriage pour le réglage automatique ; **analyse de l'état des vannes** ; « Valve Stiction Wizard » pour le frottement ; spectre de puissance et intercorrélation pour l'origine des oscillations ; analyses cascade et anticipation, linéarisation pH selon la version (Standard, Standard Plus, Advanced).
- Méthode de détection du frottement et seuils : **non publics** dans ce que j'ai vu. L'éditeur affirme que jusqu'à 30 % des vannes installées auraient des défauts mécaniques : c'est une affirmation commerciale, sans mesure indépendante.
- URL : https://www.valmet.com/automation/services/process-performance/pid-tuning-process-control/pid-loop-optimizer/ ; https://www.valmet.com/globalassets/automation/br81922_en_01-valmets-pid-loop-optimizer.pdf ; https://www.valmet.com/globalassets/flow-control/industries-services-products-blocks/services-pages-blocks/valve-services/pid-tuning-and-process-control-services/expertune-pid-loop-optimizer-standard-version.pdf ; https://www.valmet.com/automation/services/process-performance/pid-tuning-process-control/planttriage/

### 4.3 MATLAB — PID Tuner et System Identification Toolbox
- **PID Tuner** :
  - Il estime un modèle de procédé à partir de données de réponse mesurées ou simulées (tableau, `timeseries`, `iddata`) : importation, identification, puis réglage.
  - Il **suppose** que les données représentent un procédé relié au PID en contre-réaction négative, avec un échelon injecté à l'entrée u et la réponse mesurée en y.
  - Prétraitement : retrait d'offset, filtrage, extraction de sous-ensembles.
  - Structures de modèle : un pôle, paire sous-amortie, espace d'état. Les modèles identifiés (`idproc`, `idss`) **requièrent System Identification Toolbox**.
- **System Identification Toolbox** :
  - Modèles de procédé à pôles et retard (`procest`, `idproc`), page de référence non consultée.
  - En boucle fermée, d'après un fil MATLAB Answers (secondaire) : méthode directe via `pem` avec `DisturbanceModel = 'estimate'`, avec renvoi à Forssell & Ljung 1999. **Page officielle sur la boucle fermée non retrouvée.**
- URL : https://www.mathworks.com/help/slcontrol/ug/interactively-estimate-plant-from-measured-or-simulated-response-data.html ; https://www.mathworks.com/help/control/getstart/interactively-estimate-plant-parameters-from-response-data.html ; https://www.mathworks.com/company/technical-articles/tuning-a-pid-controller-when-a-plant-model-is-not-available.html ; https://www.mathworks.com/matlabcentral/answers/11670

### 4.4 Siemens — PID_Compact (S7-1200 / S7-1500) et PID Tuner (PCS 7)
- **PID_Compact** (manuel fonctionnel « PID control » S7-1200/S7-1500, édition 11/2023 ; aide en ligne TIA V20, 11/2024) :
  - **pré-réglage** (*pretuning*) : calcul de P, I et D ;
  - **réglage fin** (*fine tuning*) : oscillation entretenue limitée de la mesure, paramètres optimisés d'après l'amplitude et la fréquence de cette oscillation (édition 03/2017, à recouper avec l'édition courante) ;
  - modes de fonctionnement 0 à 5 (inactif, pré-réglage, réglage fin, automatique, manuel, sortie de substitution), selon un **forum**, à confirmer dans le manuel.
- **PCS 7 « PID Tuner »** : section « Controller Optimization Using the PID Tuner » de la documentation Standard Library V7.1. Exemple d'application 38755162 (2009) : détermination du modèle de procédé et des paramètres, cas non linéaire avec séquencement de gains. Documentation des versions V9.x **non retrouvée**.
- URL : https://support.industry.siemens.com/cs/attachments/108210036/s71500_pid_control_function_manual_en-US_en-US.pdf ; https://docs.tia.siemens.cloud/r/en-us/v20/using-pid_compact-s7-1200-s7-1500/technology-object-pid_compact-s7-1200-s7-1500 ; https://support.industry.siemens.com/cs/attachments/38755162/38755162_GainSched_PID-Tun_EN.pdf

---

## 5. Critères de benchmark (proposés AVANT toute comparaison)

Principe : chaque tolérance est **à justifier** à partir d'une incertitude de mesure, d'une exigence d'usage ou d'une source. **Aucune valeur n'est fixée ici.** Protocole commun suggéré :
- jeux de cas définis et publiés avant l'exécution (pré-enregistrement) ;
- découpage apprentissage / validation disjoint ;
- graines aléatoires déclarées ;
- outil de référence indépendant du code évalué.

| Critère | Métrique candidate | Source indépendante possible | Tolérance : fondement à établir |
|---|---|---|---|
| Précision d'identification | Erreur relative sur K, τ, θ (et τ₂, ou Ki pour l'intégrateur) sur simulations à vérité connue, distribution Monte-Carlo (biais, dispersion) | Ljung 1999 ; Söderström & Stoica 1989 (consistance, variance asymptotique) ; simulateur indépendant (SciPy `signal` ou python-control) | Dériver de la **sensibilité du réglage** à l'erreur de modèle (dégradation de Ms et de l'IAE pour une erreur donnée), et de la résolution et du bruit de mesure déclarés |
| Qualité de prédiction | NRMSE ou « FIT » en **simulation libre** sur validation disjointe ; RMSE en unités physiques | Ljung 1999 (validation croisée) ; pratique des jeux nonlinearbenchmark (RMS sur test) | Comparer à l'écart-type de la mesure et au bruit plancher estimé ; ne pas fixer après observation (écueil déjà noté dans `docs/validation/donnees-reelles-tclab.md`) |
| Incertitudes | Taux de couverture empirique des intervalles annoncés (ex. 95 %) sur Monte-Carlo ; largeur des intervalles | Ljung 1999 (covariance asymptotique) ; bootstrap | La couverture nominale est le critère ; l'écart admissible dépend du nombre de tirages (intervalle binomial) |
| Robustesse et validité des modèles | Blancheur des résidus (autocorrélation, Ljung-Box), corrélation croisée résidus–entrée ; stabilité des paramètres entre sous-fenêtres | Ljung & Box 1978 ; Ljung 1999 ; Söderström & Stoica 1989 | Niveau de test statistique déclaré a priori ; correction du nombre effectif d'échantillons |
| Stabilité | Ms, Mt, marges de gain et de phase, robustesse à une erreur de retard (marge de retard) | Åström & Murray 2021 ; Åström & Hägglund 2006 ; calcul croisé python-control | Plages de Ms **issues de la littérature** (Åström & Hägglund ; Skogestad 2003 ; Grimholt & Skogestad 2018), à citer avec page exacte après lecture |
| Performance des réglages | IAE (et ISE) en réponse à la consigne, dépassement, temps d'établissement | Grimholt & Skogestad 2018 (IAE normalisé à robustesse fixée) ; optimum de référence obtenu par optimisation indépendante (G4) | Écart à l'optimum à **robustesse égale**, justifié par la courbe de Pareto performance/robustesse |
| Rejet de perturbations | IAE pour une perturbation d'entrée (charge) et une perturbation de sortie, normalisé | Grimholt & Skogestad 2018 ; Åström & Hägglund 2006 (perturbation de charge) | Idem : à robustesse égale |
| Effort de commande | Variation totale de u (TV), pic de u, ‖KS‖∞ (sensibilité de la commande au bruit) | Skogestad 2003 et littérature SIMC (compromis performance / effort) : **source précise à relever** | Fondé sur les limites de l'actionneur (saturation, usure) déclarées par l'utilisateur |
| Temps de calcul | Temps réel médian et 95ᵉ centile par cas, matériel et version déclarés | Aucune source scientifique : mesure instrumentée reproductible | Fondé sur l'exigence d'usage (interactivité) à expliciter dans les exigences du projet |
| Capacité à refuser des données inadéquates | Matrice de confusion (refus justifiés / injustifiés) sur un corpus **étiqueté a priori** : excitation insuffisante, absence de plateau, boucle fermée sans excitation externe, saturation, quantification, non-linéarité | Söderström & Stoica 1989 (excitation persistante) ; Gustavsson et al. 1977 et Forssell & Ljung 1999 (identifiabilité en boucle fermée) ; jeux non linéaires (§3) | Taux de faux acceptés à fixer selon le coût d'un mauvais réglage (analyse de risque), plus strict que le taux de faux refus |
| Simplicité d'utilisation | Taux de réussite des tâches, temps par tâche, erreurs ; questionnaire standardisé (ex. SUS) | ISO 9241-11 (définition de l'utilisabilité) et Brooke (SUS, 1996) : **toutes deux NV, non recherchées** | Fixée par un protocole de test utilisateur avec un panel représentatif |

---

## 6. Diagnostics de vannes et causes de mauvaise régulation — sources établies

| Thème | Référence | Niveau / source |
|---|---|---|
| Frottement (*stiction*) : modèle | M. A. A. S. Choudhury, N. F. Thornhill, S. L. Shah, « Modelling valve stiction », *Control Engineering Practice* 13, 641–658, 2005. **DOI non confirmé** (préfixe 10.1016/j.conengprac… tronqué) | NV ; https://discovery-pp.ucl.ac.uk/486/1/ADCHEM.pdf |
| Frottement : détection par intercorrélation | A. Horch, « A simple method for detection of stiction in control valves », *Control Engineering Practice*, 1999, DOI 10.1016/S0967-0661(99)00100-8 (une seule source) ; volume 7 et pages 1221 et suivantes **non confirmés**. Limites rapportées : inapplicable aux procédés intégrateurs (niveau) ; faux positifs en présence d'une perturbation sinusoïdale | NV ; https://www.bohrium.com/paper-details/a-simple-method-for-detection-of-stiction-in-control-valves/813044979671760898-2564 |
| Non-linéarité ou frottement : statistiques d'ordre supérieur | M. A. A. S. Choudhury, S. L. Shah, N. F. Thornhill, « Diagnosis of poor control-loop performance using higher-order statistics », *Automatica* 40(10), 1719–1728, 2004, DOI 10.1016/j.automatica.2004.03.022 | NV-c ; https://researchr.org/publication/ChoudhuryST04/bibtex |
| Ouvrage de synthèse sur le frottement | M. Jelali, B. Huang (éd.), *Detection and Diagnosis of Stiction in Control Loops: State of the Art and Advanced Methods*, Springer London, série *Advances in Industrial Control*, 2010, DOI 10.1007/978-1-84882-775-2, ISBN 978-1-84882-774-5 | NV-c ; https://sites.ualberta.ca/~bhuang/Stiction-Book.htm |
| Jeu mécanique / hystérésis (*backlash*) | T. Hägglund, « Automatic on-line estimation of backlash in control loops », *Journal of Process Control* 17(6), 489–499, 2007, DOI 10.1016/j.jprocont.2007.01.002 | NV-c ; https://lup.lub.lu.se/record/657769 |
| Oscillations : détection et diagnostic | N. F. Thornhill, T. Hägglund, « Detection and diagnosis of oscillation in control loops », *Control Engineering Practice* 5(10), 1343–1354, 1997. DOI non relevé | NV ; https://spiral.imperial.ac.uk/entities/publication/a8bb2d1a-cb77-4077-82d4-79d83e279dd1 |
| Évaluation des performances (variance minimale) | T. J. Harris, « Assessment of control loop performance », *Canadian Journal of Chemical Engineering* 67(5), 856–861, 1989. DOI non relevé | NV ; https://scholarhub.vn/publication/Assessment-of-control-loop-performance/a1d1a9a5-241e-41d9-8c53-1dd02c451353 |
| Revue de l'évaluation des performances | M. Jelali, « An overview of control performance assessment technology and industrial applications », *Control Engineering Practice* 14(5), 441–466, 2006, DOI 10.1016/j.conengprac.2005.11.005 ; ouvrage : M. Jelali, *Control Performance Management in Industrial Automation*, Springer, 2013, DOI 10.1007/978-1-4471-4546-2 | NV-c ; https://link.springer.com/book/10.1007/978-1-4471-4546-2 |
| Changement de dynamique (détection de ruptures) | M. Basseville, I. V. Nikiforov, *Detection of Abrupt Changes: Theory and Application*, Prentice Hall, 1993, ISBN 0-13-126780-9 ; PDF diffusé par l'autrice depuis 1998 | NV-c ; https://people.irisa.fr/Michele.Basseville/kniga/kniga.html |
| Échantillonnage, repliement, commande numérique | K. J. Åström, B. Wittenmark, *Computer-Controlled Systems: Theory and Design*, 3ᵉ éd., Prentice Hall, 1997, ISBN 0-13-314899-8 (réédition Dover existante) ; chapitre 2, section « Aliasing or Frequency Folding » | NV-c ; https://lup.lub.lu.se/record/4780736 |
| Mesure défectueuse / capteur (diagnostic de défauts) | R. Isermann, *Fault-Diagnosis Systems*, Springer, 2006 : **notice non recherchée** | NV (à rechercher) |
| Saturation de l'actionneur et anti-emballement | Åström & Hägglund 2006 (§1.3) ; Åström & Murray 2021 (§1.4) : chapitres à relever | NV-c (ouvrages) |
| Perturbations | Thornhill & Hägglund 1997 ; Choudhury et al. 2004 ; Jelali 2006 (perturbations à l'échelle de l'usine traitées dans les revues citées) | voir ci-dessus |

---

## 7. Normes IEC (périmètre seulement — **aucune conformité revendiquée**)

Les pages officielles iec.ch étaient inaccessibles. Les périmètres ci-dessous sont **NV** et décrits à grands traits d'après des sources secondaires (BSI, aperçus de normes, etc.). Les textes normatifs sont payants et n'ont pas été lus.

- **IEC 61131 (série)** : automates programmables. La partie 3 définit la syntaxe et la sémantique des langages de programmation (édition 2013 supplantée, selon une source, par **IEC 61131-3:2025** ; date à confirmer). La partie 6 traite de la sécurité fonctionnelle des automates. Lien avec ORIAS : forme des algorithmes PID et des blocs côté automate. Un logiciel d'assistance hors automate n'est pas, en soi, un programme IEC 61131-3. Sources : https://shop.standards.ie/en-ie/standards/iec-61131-3-2013-562238_saig_iec_iec_1282197 ; https://www.vde-verlag.de/iec-normen/preview-pdf/info_iec61131-3{ed4.0}b.pdf
- **IEC 61508 (série)** : sécurité fonctionnelle des systèmes électriques, électroniques et électroniques programmables relatifs à la sécurité ; cycle de vie de sécurité, niveaux SIL ; la partie 3 porte sur le logiciel. Source secondaire : https://knowledge.bsigroup.com/products/industrial-process-measurement-control-and-automation-framework-for-functional-safety-and-security
- **IEC 61511 (série)** : sécurité fonctionnelle des systèmes instrumentés de sécurité (SIS) pour le secteur des industries de transformation, application sectorielle de l'IEC 61508. Elle couvre aussi la détermination des SIL requis. **Un régulateur PID de conduite (BPCS) n'est pas un SIS.** Un outil de réglage ne doit pas être présenté comme contribuant à une fonction de sécurité. Source : https://webstore.ansi.org/preview-pages/IEC/preview_iec61511-1%257Bed1.0%257Den.pdf
- **IEC 62443 (série)** : sécurité informatique des systèmes d'automatisation et de commande industriels (IACS). Parties générales, politiques et procédures, système (ex. 62443-3-3 : exigences de sécurité système et niveaux de sécurité) et composant (62443-4-x : développement sécurisé, exigences des composants). Pertinente si ORIAS se connecte à des automates ou à des historiens. Source : https://blog.iosb.fraunhofer.de/wp-content/uploads/2017/01/The_62443_Series-1.pdf

---

## 8. Liste des références NON vérifiées, à confirmer par un humain

Rappel : **toutes** les références bibliographiques sont non vérifiées au sens strict, faute d'accès aux éditeurs, à Crossref et à doi.org. La priorité indique l'urgence de la vérification.

**Priorité haute (DOI absent, déduit, unique ou contradictoire ; source primaire indispensable) :**
1. Broïda 1969, *Automatica* 5(5), 677–683 : DOI 10.1016/0005-1098(69)90033-8 **déduit du pii**. Lire le texte pour confirmer les seuils 28 % / 40 % et les coefficients 5,5 / 2,8 / 1,8.
2. Broïda 1969, *Automatisme* t. XIV n° 3, p. 105–114 : version française, tome (XIV ou XVI) et pages à confirmer.
3. Skogestad 2003 : numéro d'issue (4) et réimpression MIC 2004 (volume, pages). **Lire l'erratum** (*JPC* 14, 2004, 465).
4. Grimholt & Skogestad G1 (PID'12), G2 (DYCOPS 2013) et G5 (PID'18) : DOI ; G2 (pages 265–270).
5. Chapitre « The SIMC method for smooth PID controller tuning » : ouvrage, éditeurs, pages, DOI.
6. Panagopoulos, Åström & Hägglund 1998 : DOI et ordre des auteurs.
7. Horch 1999 : volume (7), pages, DOI 10.1016/S0967-0661(99)00100-8.
8. Choudhury, Thornhill & Shah 2005, « Modelling valve stiction » : DOI complet.
9. Thornhill & Hägglund 1997 ; Harris 1989 : DOI.
10. Van den Hof 1998 : DOI 10.1016/S1367-5788(98)00016-9 (source douteuse).
11. Hurvich & Tsai 1989 : DOI 10.1093/biomet/76.2.297 (source unique).
12. Control Station : numéro, date de dépôt et statut de délivrance du brevet NSS (US 2011/0093247 ?).
13. Licences des **données** : DaISy, nonlinearbenchmark.org, mesures TCLab d'apmonitor.com, base Jelali & Huang.
14. Isermann 2006, ISO 9241-11, Brooke 1996 (SUS) : non recherchés.

**Priorité normale (notices concordantes ; à confirmer sur la page de l'éditeur ou la notice DOI) :**
Ljung 1999 ; Söderström & Stoica 1989 ; Åström & Hägglund 2006 ; Åström & Hägglund 2004 (*JPC* 14, 635–650) ; Åström & Murray 2021 ; Rivera, Morari & Skogestad 1986 ; Grimholt & Skogestad G3 et G4 ; Akaike 1974 ; Schwarz 1978 ; Burnham & Anderson 2002 ; Ljung & Box 1978 ; Gustavsson, Ljung & Söderström 1977 ; Forssell & Ljung 1999 ; Van den Hof & Schrama 1993 ; Choudhury, Shah & Thornhill 2004 ; Jelali & Huang 2010 ; Hägglund 2007 ; Jelali 2006 et 2013 ; Basseville & Nikiforov 1993 ; Åström & Wittenmark 1997 ; Park et al. 2020 (TCLab) ; De Moor et al. 1997 (DaISy) ; Fuller et al. 2021 (python-control, DOI non relevé) ; Lacerda et al. 2020 (SysIdentPy, DOI pris dans le README).

**Tous les éléments du §4 (logiciels industriels) et du §7 (normes)** : à relire sur les pages officielles, qui n'ont pas pu être consultées depuis la session.

**Vérifiés (V)** : licences, versions et dates PyPI et git de NumPy, SciPy, python-control, SIPPY, SysIdentPy, TCLab et du chargeur `nonlinear-benchmarks` ; versions de `engine/requirements.txt` ; absence de « reference-tclab » dans le dépôt ; coefficients de Broïda employés dans `engine/broida.py` et leur cohérence mathématique (calcul).
