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

---

## Addendum (28/11/2026) : boucle fermée réelle avec réglages documentés (règle §7 iv)

**But** : trouver des **mesures** réelles en boucle fermée sous PID dont les réglages sont publiés, avec la consigne et la sortie enregistrées, et si possible un essai en boucle ouverte du même procédé.

**Ce qui n'a pas été fait** : aucune identification ni aucune décision de critère n'a été exécutée. La seule opération de calcul est un contrôle de cohérence : la commande enregistrée a été recalculée à partir des gains et de l'erreur enregistrés.

**Recherche** :
- API de recherche Zenodo (`/api/records?q=…&type=dataset`) avec 13 requêtes : PID, boucle fermée, TCLab, cuves, pilote, gains, autoréglage par relais ;
- liste des benchmarks de www.nonlinearbenchmark.org : aucun benchmark en boucle fermée sous PID ;
- arXiv et apmonitor.com ont déjà été traités plus haut, sans nouveau résultat.

**Données** : téléchargées dans des sous-dossiers neufs du dossier scratchpad `real/` (`twotank_23529910/`, `throttle_23493909/`). Rien n'est versionné, et aucun des scripts livrés dans les archives n'a été exécuté.

### A1. Two-Tank Water System, v2.0 (Gómez-González et al., Univ. A Coruña), DOI 10.5281/zenodo.23529910

- **Licence** :
  - API Zenodo : `license.id = cc-by-4.0` ;
  - `README.txt` de l'archive : « Creative Commons Attribution 4.0 International (CC BY 4.0) » ;
  - article de données associé : Sci Data 13, 1465 (2026), doi 10.1038/s41597-026-07686-2.
- **Intégrité** :
  - MD5 publié `3a70e0b3b9d14b4c0520110963c3d33e`, identique au fichier téléchargé ;
  - SHA-256 ORIAS `e31b41da2376d885774891606d346b26926d93db8483d9d7ac6bfcc23e24128e` ;
  - archive `Dataset_v2.zip` de 35,5 Mo.
- **Ce qui est mesuré** (banc de laboratoire réel) :
  - 25 essais de 30 600 échantillons à 1 Hz (≈ 8,5 h chacun), réalisés du 08/07 au 27/10/2025 ;
  - procédé SISO : commande de pompe `Control_Signal_pct` (% de 0-10 V, variateur 0-50 Hz) vers le niveau du réservoir haut `Level_pct` (% de 0-25 cm, quantifié sur 8 bits, pas de 0,98 mm) ;
  - consigne `Setpoint_pct` : escalier de 10 à 90 % par pas de 5 %, 30 min par palier (16 changements par essai) ;
  - erreur `Error_pct` ;
  - indicateurs `Sensor_Dropout` (pertes du capteur ultrasonore) et `perturbation` (vanne manuelle déplacée, fichiers Disturbance).
- **Réglages documentés : oui, et vérifiés.**
  - Le README donne la loi exacte : PID incrémental, synthèse de Dahlin (λ = 3 s), Ts = 1 s, `u(k) = u(k−1) + Kp[Δe + (Ts/Ti)e + (Td/Ts)Δ²e]`, saturation [0, 100].
  - Les gains `Kp`, `Ki = Kp/Ti` et `Kd = Kp·Td` sont **enregistrés à chaque échantillon**. Ils ont été corrigés en v2 (Ti et Td étaient inversés en v1, voir `CHANGELOG.md`).
  - Contrôle ORIAS : la loi, appliquée aux gains et à l'erreur enregistrés **du même échantillon**, reconstruit la commande enregistrée à moins de 10⁻³ % près sur **99,9 à 100 %** des échantillons, dans les 25 fichiers (écart médian ≈ 2·10⁻⁹).
- **Limites pour la règle iv** :
  1. **Correcteur adaptatif** : les gains sont recalculés à chaque seconde à partir d'un modèle identifié en ligne par moindres carrés récursifs (RLS). Ils varient fortement, même en fin de palier : le coefficient de variation médian de Kp sur les 600 dernières secondes des paliers vaut de 1,0 à 12 selon le fichier. Ce n'est **pas** un réglage PI ou PID fixe comme celui que recommande MKR.
  2. **Aucun essai en boucle ouverte** du même banc : le modèle ne pourrait être identifié que sur des données en boucle fermée, ce qui sort du chemin prévu par le protocole (identification sur un essai en boucle ouverte).
  3. Fichiers PassiveOutflow et Disturbance_HighOutflow : saturation de la pompe sur 17 à 80 % des échantillons et 276 à 2 470 pertes du capteur. Les fichiers Disturbance_LowOutflow ne sont pas saturés et n'ont que 1 à 2 pertes. Les 10 fichiers **Baseline** (13 au 27/10/2025) n'ont ni saturation ni presque aucune perte (0 à 1).
- **Utilisable pour la règle iv : partiellement.**
  - C'est une **vraie vérité de commande réelle**, au sens où l'on connaît exactement le correcteur appliqué. On peut donc comparer la réponse en boucle fermée prédite par un modèle, simulé avec la **suite de gains enregistrée**, à la réponse mesurée.
  - En revanche, on ne peut ni tester « le PI recommandé à partir d'un modèle identifié en boucle ouverte », ni obtenir une identification en boucle ouverte indépendante.
  - **Usage autorisé** : embarquable sous CC BY 4.0, avec attribution et liste des modifications.

### A2. Iterative Feedback Tuning on Throttle Valves (Martínez-Jiménez, Miller-Murphy, Ondoa-Essomba, Witrant ; soumis à L4DC 2027), DOI 10.5281/zenodo.23493909

- **Licence** : API Zenodo `license.id = cc-by-4.0`. Les données d'identification `data/` viennent de Witrant, Landau et Vaillant, *Control Eng. Practice* 139, 105634 (2023), et sont republiées dans cet enregistrement par un coauteur. Leur licence d'origine n'a pas été vérifiée séparément.
- **Intégrité** :
  - MD5 publié `9589262e3ef2d688b96ba6dc7adf83a7`, identique au fichier téléchargé ;
  - SHA-256 ORIAS `7c1a187563693c7ab41fcd768ed5d107dd386cdd6d9380d87fc84257a2197900` ;
  - archive `data_code_L4DC2027.zip` de 5,7 Mo.
- **Ce qui est mesuré** : deux vannes papillon électroniques réelles d'automobile, p0 et p9.
  - Campagne matérielle des 13 et 15/10/2026 : journaux bruts `code/results/bench_p0` et `bench_p9`.
  - Ces journaux sont **distincts** des jumeaux numériques simulés (`bench_twin0`, `bench_twin9`). Le backend est choisi par `--port` (matériel, `/dev/ttyACM0` dans tous les journaux `bench_p*`) contre `--twin` (simulation), d'après `make_bench` dans `benchlib.py`.
  - Chaque essai contient 400 échantillons à Ts = 50 ms : consigne `r` (paliers de 15 et 65°), angle mesuré `y` (en degrés) et commande `u` (MLI de −255 à 255).
  - Il y a 174 essais matériels, dont des **validations répétées** : par exemple `p0_1565_validate.json` contient 5 réglages × 3 répétitions.
- **Réglages documentés : oui, fixes par essai.**
  - Correcteur iP, équivalent à un PI incrémental : `u_k = u_{k−1} − q0·e_k − q1·e_{k−1}`, avec e = y − r, q0 = (1 + Ts·K)/(α·Ts) et q1 = −1/(α·Ts) (README du code).
  - Les valeurs (K, α) sont enregistrées dans chaque essai.
  - L'anti-emballement est intégré, d'après `bench/README.md`.
- **Boucle ouverte du même procédé : oui, mais ancienne** : `data/ident_prbs_p2_ts50_p0.csv` et `_p9.csv` (SBPA), plus `ident_preliminary_gain_multi_p0.csv` et `_p9.csv` (escalier de MLI). Colonnes : temps (ms), MLI (0-255), angle (°). Ces essais datent de la campagne de 2023.
- **Limites pour la règle iv** :
  1. **Hors du domaine « procédé »** : il s'agit d'un asservissement électromécanique de position, avec ressort de rappel, frottement et collage (« stiction », que l'article décrit comme variable d'un essai et d'une vanne à l'autre). La pertinence d'un modèle FOPDT ou SOPDT n'est pas établie.
  2. Les essais en boucle ouverte ont **trois ans de plus** que les essais en boucle fermée, alors que le frottement dérive (avec la température notamment).
  3. La commande atteint les butées ±255 lors des échelons, ce qui rend la boucle non linéaire.
  4. Petite incohérence de traçabilité : le journal de campagne de `bench/README.md` date tout du 13/10/2026, alors que plusieurs fichiers p0 sont horodatés du 15/10/2026.
- **Utilisable pour la règle iv : oui sur le plan formel, mais pas représentatif du domaine.** C'est le seul jeu trouvé qui réunit les trois conditions : une boucle fermée réelle à réglages fixes publiés, la consigne et la sortie enregistrées, et une boucle ouverte du même équipement. Il peut servir de **contrôle de non-contradiction hors domaine**, à condition de le déclarer comme tel. Il ne peut pas soutenir à lui seul une conclusion sur des boucles de procédé (température, niveau, pression, débit). **Usage autorisé** : embarquable sous CC BY 4.0, à confirmer pour `data/` (origine CEP 2023).

### A3. Autres résultats écartés

- **Écartés sans téléchargement, sur la base de leur description Zenodo** :
  - 14774155 (réglage PID multiboucle) et 13150842 (Error Governor) : **simulations** ;
  - 20584671 (module de chauffage PID) : fichiers de conception et modèle ANSYS, sans mesure en boucle fermée ;
  - 24433892 (robot suiveur de ligne) : journaux historiques et nouvelle **simulation**, hors du domaine ;
  - 24328344 (direction d'un robot agricole, PID à 250 Hz) et 19228319 (véhicule autonome) : mécatronique, hors du domaine, non examinés en détail ;
  - 15862451 (pompe à chaleur CoBra) : données d'exploitation, sans réglages de régulateur annoncés ;
  - 22771629 et 22753597 (CSTR de Manipal) : boucle ouverte seulement.
- **Ce qui n'existe pas dans les sources autorisées** : aucune boucle de **procédé** réelle sous **PID à réglages fixes publiés** accompagnée d'un essai en boucle ouverte du même procédé. Le jeu de procédé le plus proche (A1) est adaptatif et n'a pas de boucle ouverte. Le seul jeu complet (A2) est un servomécanisme hors du domaine.

### A4. Conséquence pour la règle §7 iv (constat, pas décision)

- La conclusion du §4 n'est que **partiellement** levée : il existe désormais une vérité de commande réelle, mais elle est soit adaptative (A1), soit hors du domaine (A2).
- Sur une boucle de procédé, la condition iv reste **non démontrable** avec les données publiques accessibles.
- La voie la plus directe reste celle du §5 : un essai TCLab dédié d'ORIAS, avec deux échelons en boucle ouverte et un PI à réglage fixe consigné.
