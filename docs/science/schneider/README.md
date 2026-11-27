# Schneider Electric — PIDFF, PI_B et PID_INT : état des sources (27/11/2026)

**Statut : vérification documentaire PARTIELLE.** Aucune vérification sur automate. Aucune qualification. Ce dossier ne contient aucun résultat de banc. Il n'existe dans cet environnement ni M340, ni M580, ni Premium, ni Quantum, ni licence Control Expert.

**Correction du client (27/11/2026).** « PIFINT » était une erreur de nomenclature : le bloc visé est **PID_INT**. Le dépôt garde une fiche `PIFINT` vide et plusieurs mentions de ce nom : voir [ecarts-depot.md](ecarts-depot.md), E1.

| Fiche | Contenu |
|---|---|
| [pidff.md](pidff.md) | PIDFF, bibliothèque de régulation (Control) |
| [pi_b.md](pi_b.md) | PI_B, bibliothèque de régulation (Control) |
| [pid_int.md](pid_int.md) | PID_INT, bibliothèque standard |
| [ecarts-depot.md](ecarts-depot.md) | Corrections à apporter au catalogue et à la documentation du dépôt (fichier:ligne) |

## 1. Ce qui a pu être lu, et ce qui ne l'a pas été

Hôtes autorisés pour la session : `www.se.com` et `download.schneider-electric.com`. **Aucun refus n'a été contourné.**

| Ressource | Résultat le 27/11/2026 |
|---|---|
| Page du document (a) `www.se.com/fr/fr/download/document/33002520K01000/` | **Lue** (HTTP 200). Elle donne les métadonnées seulement, pas le contenu. |
| Page du document (b) `www.se.com/be/en/download/document/33002535K01000/` | **Lue** (HTTP 200), métadonnées seulement |
| Page du document 33002536K01000 (édition FR de la régulation, celle que cite le dépôt) | **Lue** (HTTP 200), métadonnées seulement |
| **PDF** des trois manuels. La page renvoie vers `download.se.com/files?p_Doc_Ref=…`. | **Refusé par le proxy de la session** (`CONNECT tunnel failed, response 403`). `download.se.com` n'est pas dans la liste des hôtes. **Aucune page de manuel n'a été lue.** |
| PDF par `download.schneider-electric.com/files?p_Doc_Ref=…&p_File_Name=…` (hôte autorisé) | **Refusé par le proxy** (`CONNECT … 403`), pour 33002519, 33002520, 33002535 et 33002536 |
| FAQ (c) FAQ000256113 | **Texte lu** (HTTP 200). Les deux schémas sont des images hébergées sur `se.my.site.com`. Cet hôte est refusé par le proxy : **schémas non lus**. |
| Autres FAQ officielles www.se.com (liste au §3) | Lues pour la plupart. FA377379 (PI_B) : HTTP 503 répété (4 essais espacés). FA226088 (PID_INT) : HTTP 404. |
| Catalogue Control Expert (`iportal.se.com`), aide en ligne (`product-help.se.com`), images des FAQ (`se.my.site.com`) | Hôtes refusés par le proxy : **non lus** |
| Forums `community.se.com` | **Écartés** : ce ne sont pas des documents officiels |

Remarque technique. Avec un en-tête `User-Agent` de navigateur, www.se.com a répondu « Access Denied » (Akamai, HTTP 403). Avec l'en-tête par défaut du client, il a répondu normalement. Aucun autre réglage n'a été modifié.

### Métadonnées des manuels, lues sur www.se.com

| Réf. | Titre exact | Langue | Version | Date | Fichier | Empreinte publiée (`checkSum`) |
|---|---|---|---|---|---|---|
| 33002520K01000 (a) | EcoStruxure™ Control Expert - Standard, Bibliothèque de blocs | français | 20 | 05 Sept 2020 | 33002520_K01_000_20.pdf (2,7 MB) | ec6f7f27dbc57547eaca7d91142adda3 |
| 33002535K01000 (b) | EcoStruxure™ Control Expert - Control Block Library | English | 16 | 06 Sept 2020 | 33002535_K01_000_16.pdf (2.2 MB) | 61e85b04fcefc0605f7c522bc634c72e |
| 33002536K01000 | EcoStruxure™ Control Expert - Régulation, Bibliothèque de blocs | français | 16 | 05 Sept 2020 | 33002536_K01_000_16.pdf (2,3 MB) | cdf634f4f9607d2e0f8d55add03044be |
| 33002519K01000 | EcoStruxure™ Control Expert - Standard Block Library | English | 20 | 06 Sept 2020 | 33002519_K01_000_20.pdf (2.6 MB) | non relevée (copie de la page du 26/11/2026) |

Chaque page rattache le document à la gamme « EcoStruxure Control Expert - Unity Pro ». **Elles ne donnent ni version de Control Expert, ni CPU, ni gamme d'automate.** Un PDF remis plus tard pourra être comparé à l'empreinte `checkSum`. Ce champ ressemble à un MD5 (32 caractères hexadécimaux), mais son algorithme n'est pas documenté sur la page.

**Point de vigilance.** Le client désigne l'édition **anglaise** de la régulation (33002535). Les transcriptions du dépôt citent l'édition **française** (33002536). La pagination des deux langues n'est pas vérifiée : une page citée dans l'une ne vaut pas pour l'autre.

## 2. Tableau comparatif (seulement ce qui est sourcé)

Niveaux de preuve employés :
- **D2** : lu par moi à la source officielle, le 27/11/2026.
- **D1m** : transcrit dans le dépôt depuis un PDF officiel fourni par le mainteneur le 24/11/2026. Ce PDF est absent du dépôt et je ne l'ai pas relu.
- **D1s** : extrait relayé par un moteur de recherche, page non ouverte.
- **NV** : non vérifié.

| Aspect | PIDFF | PI_B | PID_INT |
|---|---|---|---|
| Bibliothèque | Régulation / Control (33002535 EN, 33002536 FR) [D2, titre du document ; appartenance du bloc : D1m] ; nom logiciel « CONT_CTL » [D1s] | Régulation / Control [D1s, catalogue ; D1m indirect] | Standard (33002520) [D1m, chap. 30] ; **appartenance NV par moi** |
| Disponibilité M340 | NV (« CONT_CTL » compatible M340, M580, Momentum, Premium, Quantum : D1s seulement) | NV (même extrait D1s) | **NV** |
| Disponibilité M580 | Une FAQ officielle décrit un cas PIDFF sur **M580, Control Expert V14.0** (FA377339, D2). Ce n'est pas une table de compatibilité. | NV | **NV** |
| Premium / Quantum | Catalogue Premium 2012 (dépôt, non relu) | Catalogue Premium 2012 (dépôt, non relu) | NV |
| Version de Control Expert / Unity | Aucune version minimale lue. V14.0 apparaît comme environnement d'une FAQ (D2). | NV | NV |
| Structure | Mixte ou parallèle (`mix_par`) [D1m ; D1s] | PI « mixte (série/parallèle) » [D1s] | Forme incrémentale mixte [D1m] |
| Équation | Mixte : OUT = kp·[1 + 1/(ti·p) + td·p/(1+(td/kd)·p)]·IN, avec IN = PV − SP [D1m] | **Aucune équation lue** | ΔOUT = KP·[Δε + TS/(10·TI)·ε]/100, avec ε = SP − PV [D1m] |
| Sens d'action | `rev_dir` (BOOL) ; kp < 0 remplacé par 0 avec avertissement [D2, FA410692] | `rev_dir`, numérotation donnée « inverse » de celle du PIDFF [D1m, page non citée correctement] | Signe de KP [D1m] |
| Dérivée | Sur PV ou sur l'écart (`pv_dev`), filtre td/kd ; kd < 1 remplacé par 1 si td ≠ 0 [D2 pour kd ; D1m pour le reste] | Sans objet (PI) [D1s] | « Dt » sans équation [D1m] : **inexploitable** |
| Unités | Grandeurs physiques en REAL, ti et td de type TIME [D1m]. Une FAQ donne une base de temps en ms pour un « PID » non nommé (FA403904, D2). | NV | Entiers [0..10000], KP ×100, TI en 0,1 s, TS en 0,01 s [D1m] |
| Anti-windup | `aw_type` : 0 = aucun, 1 = actif, par « bumpless back calculation » [D2, FAQ000279186] | NV | NV |
| Manuel / auto / suivi | MAN_AUTO, TR_I/TR_S, `bump` [D1m] ; anomalie du bit de réussite au passage Manuel→Auto sur M580 [D2, FA377339] | NV | Démarrage à froid en manuel, sortie à 0 [D1m] |
| Période | dt = temps écoulé depuis la dernière exécution [D1m] | NV | TS arrondi au multiple de la période de la tâche [D1m] |
| Migration PL7 → Unity / Control Expert | NV | NV | Le PID de PL7 Pro a PARA = table de 43 mots [D2, FA137704]. Le lien avec PID_INT n'est que suggéré (FA226088 : D1s, page 404). **Correspondance de conversion NV.** |
| Adaptateur MKR | `pidff`, structure mixte seulement (`engine/block_adapters.py:35`) | **Aucun** (refus) | `pid_int`, PI seulement (`engine/block_adapters.py:93`) |

**Les trois blocs ne sont pas interchangeables :**
- **Types.** PIDFF travaille en REAL, en unités physiques, avec des temps TIME. PID_INT travaille en INT normalisé [0..10000] avec des registres mis à l'échelle (D1m). Le type de PI_B n'est pas lu.
- **Signe de l'écart.** PIDFF prend IN = PV − SP et donne le sens par `rev_dir`. PID_INT prend ε = SP − PV et donne le sens par le signe de KP (D1m).
- **Dérivée.** Le PIDFF a une dérivée filtrée documentée. Celle du PID_INT n'est pas spécifiée. PI_B n'en a pas.

## 3. Sources officielles lues le 27/11/2026 (D2)

| ID | Titre | Publiée / modifiée | Apport |
|---|---|---|---|
| FAQ000256113 | Structure diagram for the PIDFF mixed mode is missing from the help. | 2024-02-20 / 2026-04-20 | Texte seulement : schémas en image (non lus) ; renvoie à l'aide en ligne pour les équations détaillées |
| FA410692 | What does PIDFF status 080 indicate in Control Expert? | 2020-07-20 / 2022-07-17 | Avertissements, erreurs, codes (voir pidff.md) |
| FAQ000279181 | What is the purpose for the 'ovs_att' PIDFF parameter. | 2025-12-09 / 2026-04-20 | ovs_att ∈ [0, 1] ; 0 = aucune atténuation |
| FAQ000279186 | What is the purpose for the 'aw_type' PIDFF parameter. | 2025-12-09 / 2026-04-20 | aw_type 0/1, « bumpless back calculation » |
| FA377339 | How to analyze the issue of operation successful bit of PID block freeze？ | 2019-09-08 / 2026-04-22 | PIDFF, M580, Control Expert V14.0, Windows 10 |
| FA273873 | How can I retain the value of configuration parameter… PID during application download in Unity Pro | 2015-10-31 / 2026-05-12 | PARA du PIDFF est une structure (exemple `para_pid.out_inf`) |
| FA403904 (dans le recueil FAQSET0000009) | What is the time base for the UNITY-Control Expert PID integral? | recueil : 22-06-2026 / 03-08-2026 | Intégrale d'un « PID » : %MW en ms, TIME en ms. **Le bloc n'est pas nommé.** |
| FA137704 | Quels sont les paramètres de la fonction PID sous PL7 Pro ? | 2011-10-28 / 2026-02-20 | PL7 Pro : PID(TAG, UNIT, PV, OUT, AUTO, PARA), PARA = 43 mots (tableaux en image, non lus) |
| FA16760, FA289143, FA138003, FA17369, FA17396 | Conversion PL7 → Unity Pro | 2009 à 2016 / 2026 | Procédure générale (.FEF, PL7 ≥ V4.3, rapport de conversion). La table de correspondance est dans l'aide en ligne (non lue). **Rien de spécifique aux PID.** |
| FA194605 | PID blocks… ON - OFF control? | 2013-10-16 / 2025-07-03 | Pas de mode TOR (hors sujet de conversion) |
| FA192997 | How many PID Loops can a M340 handle? | 2008-10-28 / 2026-10-15 | Pas de maximum recommandé. Ne nomme aucun bloc. |
| FA343611 | Does Unity Pro have a cascade PID function block? | (copie du 26/11) | PIP, PPI (hors périmètre) |
| FA226101 | Proworx NXT → Unity Pro, PID2 | 2013-12-20 / 2025-12-01 | Hors périmètre (Quantum/Concept), cité pour mémoire |

Les copies de travail sont dans le répertoire temporaire de la session. Elles ne sont pas versionnées et ne doivent pas l'être (contenus protégés).

## 4. Niveaux de preuve atteints

| Niveau | PIDFF | PI_B | PID_INT |
|---|---|---|---|
| Vérification documentaire (manuel) | **D1m seulement** : aucune page relue | **Aucune** | **D1m seulement** : aucune page relue |
| Vérification documentaire (FAQ) | D2 : 6 FAQ | D1s : 1 FAQ (FA377379 inaccessible) | D2 (PL7 PID, lien indirect) ; D1s (FA226088) |
| Vérification numérique | Oracle du dépôt `engine/pidff_reference.py`, même lecteur que l'adaptateur, donc non indépendant. Les tests passent le 27/11/2026 (26 tests réussis avec PID_INT et le catalogue). | Aucune | Oracle de la forme PI du dépôt ; mêmes réserves |
| Qualification sur automate | **Non réalisée** | **Non réalisée** | **Non réalisée** |

**Conclusion.** Aucune compatibilité M340/M580 n'est établie pour une conversion. Pour les trois blocs : **« Conversion non qualifiée »**. PI_B : **spécification insuffisante pour écrire un adaptateur** (voir [pi_b.md](pi_b.md)).

## 5. Ce qui débloquerait la suite

1. **Les PDF 33002520, 33002535 et 33002536, remis par le client** (vérifier leur empreinte au §1), ou l'ajout de `download.se.com` aux hôtes de la session. Le lien de téléchargement des pages se.com pointe vers cet hôte, et non vers `download.schneider-electric.com`.
2. L'accès à `se.my.site.com`, pour les schémas des FAQ FAQ000256113, FA410692 et FA137704.
3. Sur l'installation : la version de Control Expert, la CPU, le micrologiciel, et la version de bibliothèque (Explorateur de types).
