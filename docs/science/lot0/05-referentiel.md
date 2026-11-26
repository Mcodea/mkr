# LOT 0 — Référentiel scientifique (état de vérification)

Le détail (auteurs, titres, publication, volume/pages, DOI, ce que la référence établit pour ORIAS, bibliothèques, jeux de données, logiciels industriels, diagnostics, normes) est dans `annexes/E-referentiel.md`. Règle : **aucune référence, DOI, page, équation ou licence n'est inventé ; ce qui n'est pas lu à la source est marqué « non vérifié ».**

## Niveaux

- **V** : source primaire lue par l'auditeur (fichier de licence, métadonnées PyPI, historique git, dépôt local).
- **NV-c** : notice concordante entre plusieurs sources secondaires, page de l'éditeur non consultée.
- **NV** : source unique, contradictoire ou incomplète.

**Aucune référence bibliographique n'atteint le niveau V** : le réseau de la session bloque doi.org, Crossref, les éditeurs, NTNU, KU Leuven, nonlinearbenchmark.org et iec.ch. Seules les fiches logicielles et les éléments du dépôt sont vérifiés.

## Bibliothèques (V)

| Bibliothèque | Licence | Version dans le dépôt / courante | Remarque |
|---|---|---|---|
| NumPy | BSD | 2.3.5 / 2.5.5 | utilisée |
| SciPy | BSD | 1.17.0 / 1.18.1 | utilisée ; sert d'oracle indépendant dans les tests de simulation |
| python-control | BSD-3 | 0.10.2 (bêta) | pas d'identification ; calcul indépendant possible des marges et de Ms |
| SIPPY (`sippy_unipi`) | **LGPL-3.0** | 1.0.1 | **analyse juridique requise avant toute intégration** ; le paquet PyPI `sippy` est une autre bibliothèque |
| SysIdentPy | BSD-3 | 0.9.0 | orienté NARMAX, peu adapté au FOPDT/SOPDT |

## Références principales (NV-c, à confirmer)

Ljung 1999 ; Söderström & Stoica 1989 ; Åström & Hägglund 2006 ; Åström & Murray 2021 ; Skogestad 2003 (*J. Process Control* 13, 291-309 ; **erratum *JPC* 14, 2004, 465 à lire avant de figer le code**) ; Rivera, Morari & Skogestad 1986 (*Ind. Eng. Chem. Process Des. Dev.* 25, 252-265) ; Grimholt & Skogestad (cinq publications identifiées, dont deux articles *JPC* 2018) ; Broïda 1969 (*Automatica* 5(5) : notice seulement, DOI déduit, **seuils 28 %/40 % non confrontés au texte**) ; Akaike 1974, Schwarz 1978, Hurvich & Tsai 1989, Ljung & Box 1978 ; Forssell & Ljung 1999, Van den Hof & Schrama 1993 ; diagnostics de vannes (Choudhury, Horch, Jelali & Huang, Hägglund, Thornhill).

## Jeux de données

Aucune licence explicite trouvée pour les **données** de DaISy, Nonlinear Benchmark et TCLab (le code TCLab est sous Apache-2.0 ; la licence BSD-3 du chargeur `nonlinear-benchmarks` couvre le code, pas les données). Aucun jeu public ne fournit de paramètres FOPDT connus d'avance : la vérité connue viendra de simulations indépendantes ; les données réelles ne servent qu'à juger la prédiction. Le dépôt « reference-tclab » mentionné par la CI n'est pas dans le dépôt local.

## Logiciels industriels (documentation publique, non relue sur les sites officiels)

Aucune équivalence ni comparaison. LOOP-PRO « NSS » : documentée comme ne demandant pas de régime permanent préalable ; algorithme propriétaire non reproduit. ExperTune : assistant de frottement de vanne et analyse de robustesse documentés. MATLAB PID Tuner : exige un procédé en boucle avec le PID ; identification par System Identification Toolbox. Siemens PID_Compact : préréglage puis réglage fin par oscillation entretenue.

## À faire en priorité par un humain ou après ouverture du réseau

Liste complète : `annexes/E-referentiel.md` §8 (14 points de priorité haute : Broïda, erratum SIMC, DOI manquants de Grimholt & Skogestad et des diagnostics, licences des données, brevet Control Station, etc.).
