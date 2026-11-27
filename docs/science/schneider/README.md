# Schneider Electric — PIDFF, PI_B et PID_INT : état des sources (mise à jour du 27/11/2026)

**Statut : vérification documentaire faite sur les manuels officiels. Aucune vérification sur automate. Aucune qualification.** Dans cet environnement, il n'existe ni CPU (M340, M580, Premium, Quantum, Momentum) ni licence Control Expert. **Toute conversion reste « Conversion non qualifiée ».**

**Correction du client.** « PIFINT » était une erreur de nomenclature : le bloc visé est **PID_INT** (voir [ecarts-depot.md](ecarts-depot.md), E1).

| Fiche | Contenu |
|---|---|
| [pidff.md](pidff.md) | PIDFF, bibliothèque Control (régulation) |
| [pi_b.md](pi_b.md) | PI_B, bibliothèque Control (régulation) |
| [pid_int.md](pid_int.md) | PID_INT, bibliothèque standard (famille CLC_INT) |
| [ecarts-depot.md](ecarts-depot.md) | Corrections à apporter au dépôt (fichier:ligne) et verdict sur E1 à E17 |

## 1. Manuels lus

Les PDF sont conservés **hors dépôt**, dans le répertoire temporaire de la session. Ils sont protégés par le droit d'auteur et ne doivent pas être versionnés. Les pages citées sont les **numéros imprimés** en pied de page. Dans ces trois PDF, le numéro imprimé est égal au numéro de page du PDF.

| Réf. (sigle) | Titre lu en couverture | Édition lue | Champ d'application lu | Empreinte MD5 = `checkSum` se.com | SHA-256 recalculé |
|---|---|---|---|---|---|
| **33002535** (M35) | EcoStruxure™ Control Expert — Control Block Library, « Original instructions » | 33002535.16, 09/2020 (366 p.) | « valid for EcoStruxure™ Control Expert 15.0 or later » (*About the Book*, p. 13) | 61e85b04fcefc0605f7c522bc634c72e ✔ | 84353f385326c3a96b9fe12ea7ceedfae5e82a7951a811a2c02beecc85d773f4 |
| **33002536** (M36) | EcoStruxure™ Control Expert — Régulation, Bibliothèque de blocs, « Traduction de la notice originale » | 33002536.16, 09/2020 (372 p.) | « applicable à EcoStruxure™ Control Expert 15.0 ou version ultérieure » (p. 13) | cdf634f4f9607d2e0f8d55add03044be ✔ | 9dcd557afce4b31ef3158ec312827dc7dd67daadfe253bf20d17ad5c44473cef |
| **33002520** (M20) | EcoStruxure™ Control Expert — Standard, Bibliothèque de blocs | 33002520.20, 09/2020 (524 p.) | « applicable à EcoStruxure™ Control Expert 15.0 ou version ultérieure » (*À propos de ce manuel*, p. 19) | ec6f7f27dbc57547eaca7d91142adda3 ✔ | 4e362bb10f1ec03b16eb85babdd67089f3974b6ced26f689d4c68650be5924cc |

Contrôles d'identité :
- **Correspondance avec les pages se.com.** Chaque MD5 est égal au `checkSum` publié sur la page se.com du document. Les numéros de document et les éditions de couverture (.16, .16, .20) correspondent aux versions de ces pages (16, 16, 20).
- **Origine de M36.** Je l'ai téléchargé moi-même depuis le lien de sa page se.com (`download.se.com`, hôte autorisé par le client).
- **⚠ Empreintes SHA-256 inversées dans la consigne.** La consigne attribuait 4e362bb… à 33002535.pdf et 84353f3… à 33002520.pdf. Le calcul donne l'inverse : 33002520.pdf → 4e362bb…, 33002535.pdf → 84353f3…. Les MD5, comparés aux pages se.com, confirment que **les fichiers eux-mêmes sont les bons**.

Autres sources lues :
- les FAQ officielles listées au §4 ;
- les schémas des FAQ FAQ000256113 et FA410692, sur `se.my.site.com`, désormais autorisé (voir pidff.md §1).

**Restent non lus :** FA226088 (HTTP 404) et le catalogue `iportal.se.com`. La table de correspondance du convertisseur PL7 se trouve dans l'aide en ligne : elle n'est pas lue.

**Les éditions FR et EN de la régulation ont des paginations différentes.** Pour le PIDFF, la page FR (M36) vaut la page EN (M35) **+ 4** ; pour PI_B, **+ 2**. Les pages citées par le dépôt sont celles de M36 : elles sont exactes pour M36 (voir E5).

## 2. Tableau comparatif (faits lus dans les manuels)

| Aspect | PIDFF | PI_B | PID_INT |
|---|---|---|---|
| Bibliothèque / famille | Control, famille *Controller*, type EFB (M35 p. 28) | Control, *Controller*, EFB (M35 p. 28) | Standard, famille **CLC_INT**, type « Procédure » (M20 p. 34, partie III p. 127). La p. 136 parle d'une « fonction élémentaire (EF) ». |
| Disponibilité lue | M340 +, M580 +, Quantum +, Momentum +, Premium + (M35 p. 28) | Mêmes cinq gammes, + (M35 p. 28) | Mêmes cinq gammes, + ; non défini par la CEI 61131-3 (M20 p. 34) |
| Version de logiciel couverte | CE ≥ 15.0 (M35 p. 13) | CE ≥ 15.0 | CE ≥ 15.0 (M20 p. 19) |
| CPU / micrologiciel | **Non donnés** | **Non donnés** | **Non donnés** |
| Type des grandeurs | REAL, unités physiques (M35 p. 164, 166–168) | REAL, unités physiques (M35 p. 152, 154–155) | INT normalisé [0..10000] (M20 p. 133, 136, 139) |
| Écart utilisé | dev = PV − SP (M35 p. 168) | DEV = PV − SP (M35 p. 155) | ε = SP − PV (M20 p. 137, 141) |
| Structure | Mixte (`mix_par = 0`) ou parallèle (`mix_par = 1`) (M35 p. 167, 169, 172) | PI mixte (M35 p. 152, 156) | « mixte (série - parallèle) » (M20 p. 130, 136), sans paramètre de choix |
| Fonction de transfert | Mixte : OUT = kp·[1 + 1/(ti·p) + td·p/(1+(td/kd)·p)]·IN (M35 p. 169) | OUT = kp·(1 + 1/(ti·p))·IN (M35 p. 156) | Pas de fonction de transfert ; équation par cycle seulement (M20 p. 141) |
| Sens d'action | `rev_dir`. Comportement : rev_dir = 1 donne sense = +1, et un écart PV − SP positif augmente la sortie (M35 p. 178). **Libellés de la table p. 167 inversés.** | `rev_dir`, même comportement (M35 p. 160) ; libellés de la table p. 155 cohérents | Signe de KP : « négatif : avant, positif : arrière » (M20 p. 140) |
| Dérivée | Sur PV ou sur l'écart (`pv_dev`), filtre du 1er ordre td/kd (M35 p. 168, 174, 178–179) | Aucune | TD et PV_DEV existent, mais **aucune équation** (M20 p. 140–141) |
| Temps | ti et td de type TIME (M35 p. 167) | ti de type TIME (M35 p. 155) | TI et TD en 0,1 s, TS en 0,01 s (M20 p. 140) |
| Période | dt = temps écoulé depuis la dernière exécution, calculé par le bloc (M35 p. 34, 177) | Idem (M35 p. 34, 160) | TS, arrondi au multiple de la période de tâche ; tâche MAST ou FAST **périodique** obligatoire (M20 p. 133, 136, 140) |
| Anti-emballement | Deux mesures : `aw_type = 0` (de base, forme incrémentale) et `aw_type = 1` (TermAW, « bumpless ») (M35 p. 164, 173, 180, 182) | Limiteur sur OUT ; le schéma p. 157 porte « Anti-Windup-Reset » ; pas de paramètre | « anti-saturation de l'action intégrale » annoncée, sans équation (M20 p. 136) |
| Modes | Auto / Manuel / Tracking, le tracking prioritaire ; `bump` (M35 p. 176) | Auto / Manuel / Tracking ; passage sans à-coup si ti > 0, avec à-coup si ti = 0 (M35 p. 159) | Manuel / Auto ; NO_BUMP ; démarrage à froid en manuel, sortie à 0 (M20 p. 134, 140–141) |
| Migration PL7 | Aucune mention dans M35 ni M36 | Aucune mention | Seule mention : « Le paramètre PL7 parameter DEVAL_MMI n'est pas disponible dans Control Expert » (M20 p. 141) |
| Adaptateur MKR | `pidff`, mixte, avec dérivée | **Aucun** | `pid_int`, PI seulement |

**Les blocs ne sont pas interchangeables :**
- PIDFF et PI_B partagent les conventions écart/sens, mais pas leurs paramètres.
- PID_INT utilise l'écart opposé, des entiers, des registres mis à l'échelle et une tâche périodique imposée.

## 3. Niveaux de preuve

| Niveau | PIDFF | PI_B | PID_INT |
|---|---|---|---|
| Vérification documentaire | **Faite** sur M35, et sur M36 pour les pages citées par le dépôt | **Faite** sur M35 | **Faite** sur M20 |
| Vérification numérique | Oracle du dépôt (`engine/pidff_reference.py`), écrit par le même lecteur que l'adaptateur, donc non indépendant. Équations relues : identiques à M35 p. 179 et M36 p. 183. | **Aucune** | Oracle de la forme PI du dépôt ; équation relue (M20 p. 141) |
| Qualification sur automate | **Non réalisée** | **Non réalisée** | **Non réalisée** |

Ce que ces lectures autorisent et n'autorisent pas :
- **Gammes.** Les tables de disponibilité permettent d'écrire « disponible sur M340 et M580 d'après le manuel 09/2020, Control Expert ≥ 15.0 ». **Elles ne qualifient aucune conversion :** ni CPU, ni micrologiciel, ni versions antérieures à 15.0 (Unity Pro ≤ 13.1, « ancien nom », M35 p. 18).
- **PI_B.** Il est **désormais assez spécifié pour un adaptateur documentaire PI** (structure mixte, ti > 0, `en_rcpy = 0`). Cet adaptateur ne serait pas qualifié. La liste de ce qui manque encore est dans [pi_b.md](pi_b.md).

## 4. FAQ officielles (www.se.com, lues le 27/11/2026)

| FAQ | Apport | Accord avec le manuel |
|---|---|---|
| FAQ000256113 | Deux schémas (« Parallel structure… », « Mixed structure… ») et un renvoi à l'aide en ligne | Le schéma « parallèle » est identique à M35 p. 171. Le schéma « mixte » est **générique** (ERR = SP − PV, gain, YMAN, ymax/ymin) et identique au schéma du PI_B (M35 p. 157). Il ne correspond pas aux noms de paramètres du PIDFF : **inexploitable** pour une conversion. |
| FA410692 | Statut 080 ; texte et image identiques à M35 p. 188–189 | Concorde. 080 (déc.) = bit 4 (une valeur de paramètre a été bornée) + bit 6 (OUT a atteint out_max) (M35 p. 35, 188). |
| FAQ000279181 | ovs_att entre 0 et 1 | Concorde (M35 p. 173, 188) |
| FAQ000279186 | « 0 = no anti-reset windup, 1 = anti-reset windup » | **Contredit le manuel.** `aw_type = 0` est déjà une mesure anti-emballement, celle de base de la forme incrémentale (M35 p. 173 ; M36 p. 177). Le manuel prévaut. |
| FA377339 | PIDFF sur M580, Control Expert V14.0 : le bit de réussite se fige au passage Manuel → Auto | Limitation connue, absente du manuel. Elle est antérieure au champ d'application CE ≥ 15.0. |
| FA137704 | PID de PL7 Pro : PID(TAG, UNIT, PV, OUT, AUTO, PARA), PARA de 43 mots | Même interface que le PID_INT (M20 p. 138–139) : la correspondance est structurelle, **pas une règle de conversion** |
| FA16760, FA289143, FA138003, FA17369, FA17396 | Procédure générale de conversion PL7 → Unity Pro | Rien de spécifique aux PID |
