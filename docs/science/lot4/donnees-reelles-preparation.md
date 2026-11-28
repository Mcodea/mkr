# LOT 4 : préparation des données réelles pour l'étude du critère de validation

> **Statut de relecture (architecte, 28/11/2026)** : produit par un agent de recherche interrompu par une limite d'API ; **recoupement partiel seulement** (licence CC BY 4.0 du jeu TCLab revérifiée à la source). Les autres mentions de licence, empreintes et chiffres restent à recouper avant toute redistribution ; aucune donnée brute n'est versionnée et aucun résultat de l'étude n'en dépend.

- **Date** : 28/11/2026. Toutes les URL ont été consultées ce jour-là, sauf mention contraire.
- **Statut** : préparation et inspection. **Aucune identification ni aucune décision de critère n'a été exécutée sur ces données.**
- **Références** :
  - `docs/science/lot4/protocole-validation-critere.md` (§5, §7 iv, amendements v1.1 et v1.2) ;
  - `docs/science/licences/jeux-de-donnees.md` (inventaire du 26-27/11) ;
  - `docs/science/references/validation-residus-etat-de-l-art.md` (Q8).
- **Fichiers produits** :
  - `tests/reference/validation_study/real/fetch_and_describe.py` : téléchargement, contrôle d'intégrité, description et criblage. Le script ne s'exécute que si on l'appelle avec une sous-commande. Il refuse d'écrire dans le dépôt.
  - `tests/reference/validation_study/real/trials_real.json` : segments retenus et rejetés, paires et boucles fermées, avec chemins, colonnes, lignes et unités.
- **Données brutes** : **aucune** n'est versionnée. Les téléchargements sont dans le dossier scratchpad `real/`, un sous-dossier par jeu. Tout le code Python a été lancé avec `-I`. Les archives sont lues en mémoire ; rien de ce qui a été téléchargé n'a été exécuté.
- **Nature des données** : tout ce qui est appelé « réel » ici est une mesure publiée comme telle par son auteur. Les fichiers de **simulation** livrés avec certains jeux sont signalés et **jamais** traités comme des mesures.

## 1. Méthode

1. **Licence lue à la source** : champ `metadata.license.id` de l'API Zenodo (`https://zenodo.org/api/records/<id>`) **et** libellé de la page de l'enregistrement, plus le `readme` de l'archive quand il existe.
2. **Intégrité** : Zenodo publie un **MD5** par fichier, **pas de SHA-256**. Les MD5 publiés ont été vérifiés sur tous les fichiers téléchargés ; ils sont identiques. Les SHA-256 du script ont été calculés par ORIAS au premier téléchargement : ce sont des empreintes **ORIAS**, pas des empreintes de l'éditeur.
3. **Structure** : colonnes, unités, pas, durée, changements d'entrée. Tout est **lu dans les fichiers**, sans hypothèse.
4. **Criblage des échelons** : la règle `RULE` du script a été écrite **avant** le criblage et s'applique de la même façon à tous les jeux. Un segment est retenu si :
   - une seule entrée change ;
   - la ligne de base dure au moins 10 s, avec une dérive inférieure à 10 % de |ΔY| ;
   - le palier dure au moins 30 s, avec une dérive sur les 10 dernières secondes inférieure à 5 % de |ΔY| ;
   - |ΔY| vaut au moins 10 fois l'écart type de la ligne de base.

   **Limite constatée** : les fenêtres de la règle sont en secondes absolues. Pour un procédé lent, une réponse qui bouge encore peut passer le test, surtout si la mesure est quantifiée. Le cas s'est produit sur TCLab (§3). Chaque segment signalé a donc aussi été **examiné à la main**. La règle n'a pas été modifiée après coup : le script affiche seulement en plus un diagnostic de dérive pour 100 s.

## 2. Tableau par jeu

| Jeu | Licence (citation), URL, date | Structure observée | Segments exploitables | Essais indépendants | Boucle fermée avec réglages documentés | Limites | Usage autorisé |
|---|---|---|---|---|---|---|---|
| **TCLab Take-Home Labs** v1.1 (Goubej, Hedengren, Sukovatý) | **CC BY 4.0**. API Zenodo : `license.id = cc-by-4.0` ; page : « Creative Commons Attribution 4.0 International » ; `readme.md` : « Unless stated otherwise, the data are provided under the Creative Commons Attribution 4.0 International license (CC BY 4.0). » https://zenodo.org/records/24286966, 28/11/2026. Prépublication arXiv:2611.21200 : licence CC BY 4.0 sur la page arXiv | `experiment_siso_model_validation.csv` : 1 001 lignes de données, pas de 1 s, 0 à 1 000 s ; colonnes `time_s`, `Q1_pct` et `Q2_pct` (%), `T1_meas_C` et `T2_meas_C` (°C) ; `controller = open_loop`. Q1 : 0 → 70 % à la ligne 20 ; Q2 : 0 → 30 % à la ligne 300. `experiment_mimo_robust_control_real.csv` : 3 600 lignes, 1 s, boucle fermée `robust_mu` ; consignes `T1_ref_C` et `T2_ref_C` modifiées aux lignes 899, 1800 et 2700 ; perturbations d'entrée `d1` (ligne 1350) et `d2` (ligne 2250), incluses dans Q. Fichiers `*_filtered` (mesure filtrée) et `simulation_*` : **non mesurés** | **1, avec réserves** : Q1 → T1, lignes 1 à 299. Échelon de +70 %, ligne de base 1 à 19 (écart type 0,275 °C), ΔT1 = 46,3 °C, rapport signal sur bruit ≈ 168. **Réponse non établie** à la ligne 299 (0,078 °C/s, environ 17 % de ΔY pour 100 s). Les autres canaux sont rejetés (§3) | **Aucun** sur le même canal. Le seul autre essai réel (MIMO, boucle fermée) exige un modèle 2×2 (§3) | **Non.** Correcteur µ MIMO, pas un PID, réalisation non publiée. Le PI SISO de la fig. 3 : données absentes de l'archive, Kp et Ki non chiffrés dans la prépublication (seule la loi `Q1 = Kp e + Ki ∫e` est donnée) | Un seul kit (`TCLab_001`), un seul essai par fichier, dates non publiées, quantification 0,49 °C. La ligne 0 (23/23 °C) est une valeur par défaut, pas une mesure. Bruit du fichier MIMO brut : écart type ≈ 1,0 à 1,3 °C | **Embarquable** (fichiers d'essais seulement, attribution et liste des modifications). Ne pas reprendre les fichiers d'enquête et de notes |
| **InTaSet** (Putri, Villacrés, Raza, Iwakin, Moazeni, 2025) | **CC BY 4.0**. API : `license.id = cc-by-4.0` ; page : « Creative Commons Attribution 4.0 International ». https://zenodo.org/records/17652851, 28/11/2026. L'archive ne contient **ni readme ni licence** | 12 fichiers xlsx. Dans InTaSet-ID (2T, 3T, 4T-M1, 4T-M2), découpage *training* / *testing* : colonnes `Time`, `Tank 1..4`, `Pump 1..2`, pas de 0,1 s, de 300 s à 2 100 s par fichier. InTaSet-FD (« closed-loop operating modes » selon Zenodo) : colonnes `x_Tank*`, `x_Pump*` et `Label`. **Aucune unité documentée** (niveaux de −0,2 à 13,6, pompes de 0 à 100) | **0** selon la règle déclarée, sur les 8 fichiers ID. Les pompes changent à chaque palier, le plus long palier sans changement dure 62 à 104 s selon le fichier, et les niveaux bougent encore en fin de palier | Découpage *training* / *testing* fourni, mais sans échelon exploitable. **Anomalie d'intégrité** (§3) | **Non.** Aucune consigne, aucun réglage, aucune colonne de mode | Unités inconnues, provenance des découpages non décrite, anomalie de duplication | Licence : embarquable. **Recommandation : ne pas embarquer**, et ne s'en servir en test interne qu'après explication de l'anomalie par les auteurs |
| **CSTR pilote, PRBS multiniveau** (Indiran, Manipal) | **CC BY 4.0**. API : `license.id = cc-by-4.0` ; page : « Creative Commons Attribution 4.0 International ». https://zenodo.org/records/22753301, 28/11/2026 | Fichier `CSTR_24aug_PRBS_dataset.xlsx`, feuille « CSTR Dataset » : 3 131 lignes de données (lignes de la feuille 3 à 3133), pas de 0,5 s, 0 à 1 565 s. Quatre entrées à 5 niveaux, chacune changée 6 à 8 fois : « Hot oil circulation » (0 à 440, **unité non indiquée**), débits Feed A et Feed B (ml/min), « Hot Oil bath » (0 à 230, **unité non indiquée**). Sorties : Tr, Tj, températures de Feed A et B (°C). Les feuilles de graphiques sont vides | **0** : aucun changement isolé ne suit une ligne de base stable. Tr dérive de 31 à 52 °C | Non | Non (boucle ouverte) | Excitation MIMO simultanée : un FOPDT SISO par échelon n'est pas applicable. Identification MIMO de type ARX possible, mais hors du périmètre FOPDT et SOPDT | Licence : embarquable. Aucun segment utile pour l'étude |
| **Colonne à distiller pilote** (Indiran / Yadav, Manipal) | **CC BY 4.0**. API : `license.id = cc-by-4.0` ; page : « Creative Commons Attribution 4.0 International ». https://zenodo.org/records/22763516, 28/11/2026 | 4 fichiers xlsx (`T1Heater`, `T1Reflux`, `T5Heater`, `T5Reflux`) de **1 seule colonne sans en-tête**, de 140 000 à 180 001 valeurs. **Ni temps, ni entrée, ni instant ni amplitude d'échelon, ni unité** (valeurs de 63 à 76, probablement en °C). L'allure est celle de réponses indicielles | **0** : sans entrée ni pas d'échantillonnage, il est impossible d'identifier K et τ | Non | Non | Données de sortie seules | Licence : embarquable. **Non utilisable** pour l'identification |
| **CONSENS, cas d'étude 1** (Kern et al., 2018) | **CC BY 4.0**. API : `license.id = cc-by-4.0` ; page : « Creative Commons Attribution 4.0 International ». https://zenodo.org/records/1438233, 28/11/2026 | `PCS_data_csv.zip` : 4 jours (26/09, 28/09, 10/10 et 17/10/2017), pas de 1 s, séparateur `;`, virgule décimale, une colonne de temps par grandeur. Fichiers `*_Bilanz` : régulateurs de débit massique, avec `*_w` = consigne et `*_x` = mesure (kg/h selon `matlab_variables_explanation.xlsx`), et `*_M0013` (signal **non documenté**, de 0 à 6 600). Fichiers de niveaux, pressions, températures et vannes. Fichiers NMR et NIR (plusieurs Go) non téléchargés | **Non criblés** pour la boucle ouverte : les débits sont asservis par leurs propres régulateurs | Quatre jours distincts, mais sans échelon en boucle ouverte | **Non.** Boucles de débit réelles avec des changements de consigne (par exemple BP11, jour 1 : 5,6 → 6,16 → 6,72 → 4,48 kg/h), mais réglages des régulateurs non publiés | Exploitation d'un pilote, signal d'actionneur non documenté | Licence : embarquable. Pertinence faible pour l'étude |
| **Cascaded Tanks** (Nonlinear Benchmark) | Page https://www.nonlinearbenchmark.org/benchmarks/cascaded-tanks lue le 28/11/2026 : **aucun texte de licence**, seulement une citation demandée (Schoukens & Noël, IFAC 2017, doi 10.1016/j.ifacol.2017.08.071 ; Schoukens, Mattsson, Wigren, Noël, doi 10.4121/12960104). La licence CC BY-SA 4.0 n'est vue qu'en source secondaire (V-sec) et **n'est pas confirmée** | **Non accessible** : lien de téléchargement vers `drive.google.com` (hôte non autorisé) ; `doi.org/10.4121/12960104` redirige vers `data.4tu.nl` (refusé, voir l'inventaire) | — | — | — | — | **Test interne seulement**, et seulement si l'accès est ouvert. Juriste nécessaire pour le partage à l'identique (ShareAlike) |
| **TCLab APMonitor** (`tclab_dyn_data2.txt`, `tclab_2sec.txt`) | **Aucune licence trouvée** (NT, voir l'inventaire). https://apmonitor.com/do/uploads/Main/, 28/11/2026 | `tclab_dyn_data2.txt` : 601 lignes, 1 s ; H1 change aux lignes 10, 200, 280 et 400 ; H2 aux lignes 120, 320 et 520. `tclab_2sec.txt` : 600 lignes, 2 s ; H1 et H2 alternent toutes les 50 lignes | **0** échelon SISO propre : l'autre chauffage change pendant la réponse | Non | Non : les pages « PI control » donnent des gains d'exercice (`Kc = 1/0.9`, `tauI = 175`) sans fichier de mesure associé | — | **Test interne seulement**, jamais embarqué |
| **CBE30338 (Notre Dame)**, `notebooks/data/data_2020*.csv` | Aucun fichier LICENSE (NT). https://github.com/jckantor/CBE30338-book, commit `bec88ef9f6d9`, 28/11/2026 | 48 fichiers `Time,T1,T2,U1,U2,SP1,SP2` (boucle fermée), référencés par **aucun** carnet | 0 | — | **Non** : aucun réglage associé aux fichiers. Origine réelle ou simulée **non établie** (premières lignes : T1 = T2 = 20,9495 exactement) | — | Test interne seulement, et seulement après établissement de l'origine |
| **DaISy** | Non accessible : `homes.esat.kuleuven.be` (connexion réinitialisée par le proxy le 28/11/2026), `ftp.esat.kuleuven.be` refusé (inventaire). Aucun contournement tenté | — | — | — | — | — | Test interne seulement (inventaire), non accessible |

## 3. Détails vérifiés

**TCLab SISO** (`data/experiment_siso_model_validation.csv` ; indices = lignes de données, `time_s` = indice) :
- **Q1 → T1, lignes 1 à 299** : seul segment exploitable, avec réserves.
  - La règle le classe « CANDIDATE », mais seulement parce que sa fenêtre finale de 10 s ne voit pas la dérive sous une quantification de 0,49 °C.
  - Sur les lignes 270 à 299, T1 monte encore de 0,078 °C/s. T1 vaut 65,8 °C à la ligne 299 et atteint plus tard 83,9 °C, avec Q2 en marche.
  - Une identification FOPDT est possible, mais avec un régime final **extrapolé**.
- **Q2 → T2, échelon à la ligne 300** : classé « CANDIDATE » par la règle, **rejeté à l'examen**. La « ligne de base » des lignes 270 à 299 est le transitoire dû à Q1 (T2 monte de 0,068 °C/s) : la réponse superpose deux entrées.
- **Q1 → T2** (couplage) : rejeté, réponse loin d'être établie.
- **Q2 → T1** : rejeté, dérive de la ligne de base.
- **Observation non interprétée** : T2 dépasse sa ligne de base de plus de 1 °C dès la ligne 31, avant T1 (ligne 38). Cela peut venir de la quantification.

**Paire candidate rejetée** : estimation sur Q1 → T1 (SISO), validation sur l'essai MIMO réel en boucle fermée, par simulation du modèle sur les Q1 et Q2 enregistrés. Elle est rejetée pour trois raisons :
- il faudrait un modèle 2×2 ;
- les canaux Q2 → T ne sont pas identifiables proprement à partir de l'essai SISO ;
- le point de fonctionnement diffère (T entre 46 et 74 °C).

Elle reste une piste pour une étude MIMO, **hors du protocole actuel**.

**InTaSet, anomalie d'intégrité** dans `InTaSet-ID-3T_Testing.xlsx` :
- On compare les lignes 700 à 3599 aux lignes 3020 à 5919 (décalage de 2 320 lignes, soit 232 s).
- Tank 2 et Tank 3 sont **identiques au bit près** sur 2 300 des 2 900 lignes. Tank 1 et Tank 4 diffèrent légèrement.
- La seconde moitié du fichier n'est donc **pas** une répétition indépendante. À signaler aux auteurs avant tout usage.

## 4. Ce que ces données permettent et ne permettent pas de démontrer

**Elles permettent**, une fois l'étude exécutée sur le segment TCLab Q1 → T1 :
- de vérifier que la chaîne d'identification et chaque critère candidat **fonctionnent sur une mesure réelle** : quantification, bruit réel, réponse non établie ;
- de constater leurs décisions (accepter ou refuser) et la **cohérence** entre critères sur ce seul cas ;
- de mesurer la qualité de l'**ajustement** sur l'essai d'estimation.

**Elles ne permettent pas** :
- **la validation indépendante requise par le protocole (§5 i-ii)**. Aucun jeu accessible ne fournit un second essai indépendant sur le même canal. **Nombre de paires estimation / validation indépendantes : 0.** Le « contrôle de non-contradiction » du §7 iv ne peut donc pas être fait au sens du protocole : on ne peut pas confronter la décision d'un critère à une erreur de **prédiction** sur un essai tenu à l'écart ;
- **une vérité de commande réelle**. Il faudrait une boucle fermée réelle dont les réglages (Kp, Ti, Td, période) sont publiés, pour comparer la réponse prédite par le modèle à la réponse observée. **Aucun essai de ce type n'a été trouvé** :
  - TCLab : correcteur µ non publié, et données du PI absentes ;
  - InTaSet et CONSENS : réglages absents ;
  - CBE30338 : réglages absents et origine incertaine ;
- **des taux de faux positifs ou de faux négatifs**. Aucun de ces jeux ne fournit le procédé vrai ; seules les populations synthétiques le permettent ;
- **une généralisation** : un seul kit TCLab, un seul essai, un seul sens d'échelon.

**Conséquence pour le protocole** (constat, pas décision) : selon l'amendement v1.2, la condition §7 iv reste **non satisfaisable** avec les données accessibles. La conclusion la plus forte possible reste « complémentarité démontrée **sur données synthétiques seulement** ». La porte actuelle est conservée.

## 5. Ce qui débloquerait la situation (propositions, non engagées)

1. **Essais TCLab ORIAS dédiés** (proposition, à arbitrer) : sur un banc dont on maîtrise la procédure, réaliser :
   - au moins deux échelons en boucle ouverte sur le même canal, d'amplitude et de sens différents, à des dates différentes, avec l'autre chauffage à 0 et des paliers d'au moins 5 τ ;
   - puis un essai en boucle fermée PI avec Kp, Ti, période et anti-emballement **consignés**.

   C'est la seule voie identifiée vers une vérité de commande réelle.
2. **Demander aux auteurs du TCLab Take-Home** les données du PI de la fig. 3 et les gains utilisés, ou la réalisation du correcteur µ.
3. **Demander aux auteurs d'InTaSet** les unités, le protocole et l'explication de la duplication. Demander à Manipal les entrées et le pas d'échantillonnage du jeu de la colonne.
4. **Ouvrir `data.4tu.nl`** (Cascaded Tanks, NAWI via OpenEI) : décision d'environnement, hors du périmètre de cette mission.

## 6. Hôtes

- **Utilisés et autorisés** : `zenodo.org`, `arxiv.org`, `www.nonlinearbenchmark.org`, `apmonitor.com`, `github.com` (clonage public), `doi.org` (redirection seulement).
- **Refusés ou non autorisés, aucun contournement** :
  - `data.4tu.nl` (cible de la redirection DOI de Cascaded Tanks) ;
  - `drive.google.com` (fichiers Cascaded Tanks du site Nonlinear Benchmark) ;
  - `homes.esat.kuleuven.be` (connexion réinitialisée) et `ftp.esat.kuleuven.be` (DaISy) ;
  - ainsi que les hôtes déjà listés dans `docs/science/licences/jeux-de-donnees.md`.
- Un essai de sonde vers ces hôtes refusés a été bloqué par la politique de la session ; il n'a pas été renouvelé.

## 7. Reproduction

```
python -I tests/reference/validation_study/real/fetch_and_describe.py fetch    --dest <scratch>/real [--include-large]
python -I tests/reference/validation_study/real/fetch_and_describe.py verify   --dest <scratch>/real
python -I tests/reference/validation_study/real/fetch_and_describe.py describe --dest <scratch>/real
python -I tests/reference/validation_study/real/fetch_and_describe.py screen   --dest <scratch>/real
```

- `fetch` relit la licence (`license.id`) et le MD5 publiés par Zenodo, puis s'arrête si l'enregistrement a été révisé.
- `verify` contrôle le MD5 publié et le SHA-256 ORIAS.
- `screen` affiche chaque segment examiné, avec son verdict selon la règle et la dérive pour 100 s.
- Le script exige `openpyxl`, déjà épinglé dans `engine/requirements.txt`.
- Les sous-commandes `verify`, `describe` et `screen` ont été exécutées le 28/11/2026 sur les fichiers téléchargés.
- La sous-commande `fetch`, sous sa forme scriptée, **n'a pas été exécutée** : les téléchargements de cette session ont été faits avec `curl`, avec les mêmes URL et les mêmes MD5.
