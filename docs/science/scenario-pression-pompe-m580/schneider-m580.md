# Schneider M580 / EcoStruxure Control Expert — blocs PIDFF, PID_INT et « PIFINT »

> **Correction du 2026-11-27 (arbitrage du client).** « PIFINT » était une erreur de nomenclature : le bloc visé est **PID_INT**. La fiche vide « PIFINT » a été retirée du catalogue (`engine/catalogue/schneider.json`) ; PID_INT est documenté dans `docs/science/schneider/pid_int.md` et modélisé dans `engine/block_pid_int.py`. Les sections ci-dessous sur « PIFINT » sont conservées pour la traçabilité de la recherche et ne décrivent plus un bloc à implémenter.

**Statut : vérification documentaire PARTIELLE. Aucune vérification sur automate, aucune qualification.** Il n'existe aucun banc, aucun M580 et aucune licence Control Expert dans cet environnement.

## 0. Ce qui a pu être lu, et ce qui ne l'a pas été (27/11/2026)

| Action | Résultat |
|---|---|
| Pages de métadonnées www.se.com des manuels | **Lues** (tableau ci-dessous) |
| Téléchargement des PDF (`download.se.com`, `download.schneider-electric.com`) | **Refusé par le proxy (HTTP 403)**. Le refus n'a pas été contourné. **Je n'ai lu aucune page des manuels.** |
| FAQ officielles se.com | **Lues** directement (§4) |
| Copies tierces (Scribd, pdfcoffee, blogs) | **Écartées** : ce ne sont pas des sources officielles |

Les pages et équations citées aux §2 et §3 sont donc **reprises du dépôt**. Elles y ont été transcrites par le mainteneur le 24/11/2026, à partir des PDF fournis (`docs/catalogue/pidff-conversion.md`, `docs/catalogue/pidint-conversion.md`, `engine/catalogue/schneider.json`). Je les ai **relues dans le dépôt, pas dans les manuels**. Elles restent à recontrôler à la source.

### Manuels identifiés sur se.com (métadonnées vérifiées)

| Document | Titre | Fichier | Publication |
|---|---|---|---|
| 33002536K01000 | EcoStruxure Control Expert – Régulation, Bibliothèque de blocs (FR) | 33002536_K01_000_16.pdf | 2020-09-05 |
| 33002535K01000 | EcoStruxure Control Expert – Control Block Library (EN) | 33002535_K01_000_16.pdf | 2020-09-06 |
| 33002520K01000 | EcoStruxure Control Expert – Standard, Bibliothèque de blocs (FR) | 33002520_K01_000_20.pdf | 2020-09-05 |
| 33002519K01000 | EcoStruxure Control Expert – Standard Block Library (EN) | 33002519_K01_000_20.pdf | 2020-09-06 |

Ces deux paires (36/35 et 20/19) ressemblent à des éditions FR/EN d'un même manuel. **La concordance des pages entre les deux langues n'est pas vérifiée.**

## 1. « PIFINT » existe-t-il ?

**Aucune source officielle consultée ne mentionne un bloc nommé « PIFINT ».** Recherches effectuées :
- moteur de recherche général ;
- moteur restreint à se.com, schneider-electric.com et product-help.schneider-electric.com ;
- variantes « PIFINT » et « PI_FINT ».

Les blocs PI ou PID de Control Expert relevés dans les sources officielles sont les suivants :
- **PIDFF** et **PI_B** dans la bibliothèque Régulation (catalogue du dépôt) ;
- **PID_INT** dans la bibliothèque Standard (33002520, d'après le dépôt) ;
- **PIP** et **PPI** pour la cascade (FAQ FA343611, lue).

La mention « PIFINT » n'apparaît dans le dépôt que dans `docs/catalogue/qualification.md`, avec une fiche vide `PIFINT` (statut `to_verify`) dans `engine/catalogue/schneider.json`.

**Conclusion.** Je ne conclus pas que le bloc n'existe pas : une absence de résultat n'est pas une preuve d'inexistence, et les PDF n'ont pas pu être parcourus. Il faut **demander au client** s'il désigne PID_INT, PI_B ou PIP/PPI. En attendant, aucune fiche PIFINT ne doit être documentée.

## 2. PIDFF (bibliothèque Régulation) — d'après le dépôt et les FAQ

Source : 33002536, édition 09/2020 (.16), chapitre 17, p. 167 à 187.

| Aspect | Contenu | Page ou source |
|---|---|---|
| Structure mixte (`mix_par = 0`) | OUT = kp·[1 + 1/(ti·p) + td·p/(1 + (td/kd)·p)]·IN, avec IN = PV − SP en unités physiques | p. 173 |
| Structure parallèle (`mix_par = 1`) | OUT = [kp + α/(ti·p) + α·td·p/(1 + (td/kd)·p)]·IN, avec α = (out_sup − out_inf)/(pv_sup − pv_inf). **Incohérence** entre la p. 176 et la p. 183 : `kp` figure dans TermI | p. 173, 176, 183 |
| Unités | Valeurs en unités physiques (p. 168) ; `ti` et `td` de type TIME (p. 171), résolution 1 ms ; `kp` dépend des échelles | p. 168, 171 |
| Sens d'action | `rev_dir = 1` : sortie plus haute si PV − SP > 0. `rev_dir = 0` : sortie plus basse. **Les libellés « direct/inverse » se contredisent** entre la p. 171 et la p. 182 | p. 171, 182 |
| kp négatif | Refusé (statut 080, valeur remplacée par 0). Le sens d'action passe par `rev_dir` | FAQ FA410692 (lue) |
| Algorithme | Incrémental si ti > 0 : TermI = sense·kp·(dt/ti)·dev, avec dt le temps écoulé depuis la dernière exécution. Absolu si ti = 0 | p. 182–183 |
| Filtre de dérivée | Premier ordre, constante td/kd ; kd < 1 est remplacé par 1 | p. 173, 178 ; FA410692 |
| Limites | OUT borné à [out_min, out_max] en automatique et à [out_inf, out_sup] en manuel et en suivi ; `outrate` limite la pente en unités par seconde | p. 172, 179 |
| Modes | Le suivi TR_S est prioritaire sur MAN_AUTO ; `bump = 0` assure un passage en automatique sans à-coup | p. 172, 180 |
| Anti-saturation | `aw_type` : voir la contradiction C3 | p. 183 ; FAQ000279186 |
| Pondération de consigne | `ovs_att` ∈ [0, 1] ; 0 donne l'écart classique | p. 177 ; FAQ000279181 (lue) |
| Période d'exécution | dt mesuré par le bloc ; aucune valeur imposée n'est relevée | p. 183 |

**Application au scénario.** Le gain du procédé est positif : plus de vitesse donne plus de pression. Avec IN = PV − SP, on prend donc `rev_dir = 0` et `kp = |Kc|` en %/bar, si MV et PV sont en unités physiques. Cela suppose la structure mixte et des fonctions annexes neutres (`ovs_att = 0`, `dband = 0`, `ff_sup = ff_inf`, `outbias = 0`, d'après le dépôt). **Correspondance non qualifiée.**

## 3. PID_INT (bibliothèque Standard) — d'après le dépôt

Source : 33002520, édition 09/2020 (.20), chapitre 30, p. 135 à 141.

| Aspect | Contenu | Page |
|---|---|---|
| Format | Entrées et sortie INT normalisées sur [0..10000] | p. 135 à 141 |
| Écart | ε = SP − PV (attention : signe opposé au PIDFF) | p. 137 |
| Équation | TI = 0 : OUT = KP·ε/100 + 5000. TI > 0 : ΔOUT = KP·[Δε + TS/(10·TI)·ε]/100 et OUT += ΔOUT (forme incrémentale, mixte) | p. 141 |
| Registres | KP ×100 (−10000 à +10000, signé) ; TI en 0,1 s (0 à 20000) ; TD en 0,1 s (0 à 10000) ; TS en 0,01 s (1 à 32000), arrondi au multiple de la période de tâche | p. 135 à 141 |
| Sens d'action | Donné par le signe de KP (libellés « avant/arrière » non définis) | p. 141 |
| Dérivée | « Dt » cité sans équation : **non exploitable** | p. 141 |
| Démarrage | Redémarrage à froid : mode manuel, sortie à 0 | catalogue |
| Non documenté dans le dépôt | Arithmétique entière et arrondis, anti-saturation, transition sans à-coup détaillée | – |

**Application au scénario**, avec PV de 0 à 10 bar et MV de 0 à 100 % représentés sur [0..10000] :
- KP sans dimension = Kc × (10 bar / 100 %) = 12,75 × 0,1 = 1,275, soit un **registre KP = 128** (arrondi) ;
- **registre TI = 47** (4,7 s) et **registre TS = 10** (0,1 s, si la tâche le permet).

Un KP positif suffit, puisque ε = SP − PV. **Exemple de calcul, non qualifié.**

**Disponibilité sur M580 : non vérifiée.** Je n'ai lu aucun tableau de compatibilité entre les CPU et la bibliothèque.

## 4. Contradictions et points relevés

| # | Constat | Sources | Conséquence |
|---|---|---|---|
| C1 | « PIFINT » figure dans le périmètre (`qualification.md`) et dans le catalogue, sans aucune source | dépôt / recherches | Renommer ou retirer **après arbitrage du client** |
| C2 | `blocs-observes.md` §3 dit que PID_INT n'a été trouvé nulle part ; `pidint-conversion.md` le documente dans 33002520, ch. 30 | dépôt | Le §3 est antérieur à la lecture : à mettre à jour (hors de mon périmètre d'écriture) |
| C3 | FAQ000279186 (lue) : `aw_type` 0 = sans anti-saturation, 1 = avec, méthode « bumpless back calculation ». Le catalogue cite l'algorithme de la p. 183 **pour `aw_type = 0`**, et `pidff-conversion.md` parle de « deux types d'anti-enroulement » | FAQ / dépôt | Sémantique d'`aw_type` à relire p. 183 et à vérifier sur banc |
| C4 | `blocs-observes.md` §4 Q2 désigne 33002535 pour l'aide PIDFF ; les fiches citent 33002536. La source `SE-PIDFF-167` cite des pages de 33002535 | dépôt / métadonnées | Probablement EN et FR du même manuel : concordance des pages à vérifier |
| C5 | Le catalogue garde la mention « Document non lu » pour 33002536 alors que d'autres faits en citent les pages | `schneider.json` | Mention obsolète |
| C6 | Conventions de signe opposées : PIDFF IN = PV − SP avec `rev_dir`, PID_INT ε = SP − PV avec signe de KP | dépôt | Une conversion doit traiter le signe bloc par bloc |
| C7 | Le PI_B numérote `rev_dir` à l'inverse du tableau du PIDFF | dépôt | À vérifier sur banc |

## 5. Niveaux de preuve

| Niveau | État |
|---|---|
| Vérification documentaire | Partielle : métadonnées et FAQ lues par moi ; pages des manuels lues par le mainteneur uniquement |
| Vérification numérique | Forme des conversions vérifiée par les oracles du dépôt (`pidff-conversion.md`, `pidint-conversion.md`). **Ce lot n'apporte aucune vérification numérique nouvelle des blocs.** |
| Qualification sur automate réel | **Non réalisée** : aucun banc, aucune version de CPU, de firmware ou de Control Expert identifiée |
