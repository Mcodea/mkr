# Écarts du dépôt : corrections à apporter (Schneider PIDFF, PI_B, PID_INT)

Relevé du 27/11/2026, mis à jour après la lecture des manuels :
- **M35** : 33002535.16, 09/2020 ;
- **M36** : 33002536.16, 09/2020 ;
- **M20** : 33002520.20, 09/2020.

Les pages citées sont les numéros imprimés. **Rien n'a été modifié en dehors de `docs/science/schneider/`.** Les corrections touchant engine/, src/ ou tests/ reviennent au mainteneur.

Gravité :
- **A** : affirmation fausse, ou qui peut orienter une conversion ;
- **B** : source mal citée ou statut de lecture périmé ;
- **C** : clarification.

## 1. Verdict sur E1 à E17 (premier relevé)

| # | Grav. | Fichier:ligne | Verdict après lecture | Correction proposée |
|---|---|---|---|---|
| E1 | A | `engine/catalogue/schneider.json:337-351` ; `docs/catalogue/qualification.md:3` ; `engine/test_block_catalogue.py:8` ; `engine/test_pidff_conversion.py:121` ; `docs/science/scenario-pression-pompe-m580/schneider-m580.md:1,27-41,91` ; `…/README.md:12` | **Maintenu.** Aucun bloc « PIFINT » dans M35, M36 ni M20. PID_INT existe (M20 ch. 30, p. 135). | Supprimer la fiche PIFINT et écrire PID_INT. Mettre les deux tests à jour (hors de ce lot). Résoudre C1. |
| E2 | A | `engine/catalogue/schneider.json:261` ; `docs/catalogue/blocs-observes.md:37` ; `docs/catalogue/pidff-conversion.md:24` ; `engine/block_adapters.py:42` | **Confirmé par le manuel.** Un kp négatif (de même dband, gain_kp, outrate) donne un **avertissement** : le bloc utilise 0 et met le bit 4 à 1 (M35 p. 188–189 ; M36 p. 192–193). Les sorties inchangées ne concernent que les **erreurs** (entrée non flottante, calcul flottant, pv_inf = pv_sup). **080 = bit 4 (paramètre borné) + bit 6 (OUT a atteint out_max)** (M35 p. 35, 188). | Réécrire en ce sens. La conduite de l'adaptateur (\|kp\| et rev_dir) reste juste. |
| E3 | B | `engine/catalogue/schneider.json:259-270` | **Confirmé** (M35 p. 188–189, p. 354). Il manque : outrate < 0 remplacé par 0 ; SP borné à [pv_inf, pv_sup] ; out_min/out_max ramenés dans [out_inf, out_sup] ; outbias et otff_* bornés ; codes 30110 et −30121. | Ajouter ces faits, sourcés sur le manuel (et non plus sur la FAQ) |
| E4 | B | `engine/catalogue/schneider.json:76, 85, 94` ; `docs/catalogue/blocs-observes.md:33, 86` | **Confirmé** : les trois FAQ sont lues, ainsi que leurs images (`se.my.site.com`). L'image de FA410692 est identique à M35 p. 188. | Requalifier en « lu à la source le 2026-11-27 ». Préférer les pages du manuel comme source. |
| E5 | B | `engine/catalogue/schneider.json:33-42` (`SE-PIDFF-167` : 33002535, « p. 167 et paramétrage p. 172–173 ») ; `docs/catalogue/blocs-observes.md:75` | **Infirmé : pas d'erreur d'attribution.** Dans M35, p. 167 = Para_PIDFF, p. 172 = mix_par et algorithmes, p. 173 = en_rcpy, anti-emballement et ovs_att. Les pages des faits `SE-33002536K01000` sont exactes pour M36. Décalage : **PIDFF FR = EN + 4**, **PI_B FR = EN + 2**. | Indiquer l'édition (FR/EN) sur chaque page citée. Ne rien renuméroter. |
| E6 | B | `engine/catalogue/schneider.json:277` | **Confirmé** : M36 est lu (p. 167–193 vérifiées). | Supprimer « Document non lu » |
| E7 | **A** | `engine/catalogue/schneider.json:225, 281` ; `docs/catalogue/pidff-conversion.md:52` ; `engine/pidff_reference.py:4-8` | **Corrigé dans l'autre sens.** Le manuel dit : « 2 antiwindup measures » (M35 p. 164). `aw_type = 0` est la mesure de base de la forme incrémentale ; `aw_type = 1` ajoute TermAW = (dt/ti)·[OUT(old) − OUTc(old)] (M35 p. 173, 180 ; M36 p. 177, 184). **La FAQ000279186 (« 0 = no anti-reset windup ») contredit le manuel**, et le manuel prévaut. « Deux types d'anti-enroulement » (`pidff-conversion.md:52`) est **exact**. | Préciser la sémantique et ne pas reprendre la FAQ. Signaler la contradiction à Schneider. |
| E8 | B | `docs/catalogue/blocs-observes.md:69` contre `:84` | **Confirmé** : PID_INT est dans M20, ch. 30, p. 135–141. | Mettre à jour le §3 ; « EB_PID » reste non identifié |
| E9 | A | `engine/catalogue/schneider.json:167-170, 321-325, 357-360` (`cpu`, versions à `null`) | **Partiellement levé.** Tables de disponibilité : PIDFF et PI_B + sur M340, M580, Quantum, Momentum, Premium (M35 p. 28) ; PID_INT de même (M20 p. 34). Manuels valables pour **Control Expert ≥ 15.0** (M35 p. 13, M36 p. 13, M20 p. 19). **Aucune CPU, aucun micrologiciel.** | Ajouter des faits « disponibilité documentaire (gamme), CE ≥ 15.0 ». **Laisser `cpu`, `firmware` et `softwareVersion` à null.** Le statut reste `to_verify`. |
| E10 | C | `engine/catalogue/schneider.json:353-439` ; `docs/catalogue/pidint-conversion.md` | **Précisé.** Seule mention : « DEVAL_MMI n'est pas disponible dans Control Expert » (M20 p. 141). Même interface que le PID de PL7 Pro (FA137704). Aucune table de correspondance. | Ajouter ces deux faits et « migration PL7 : Conversion non qualifiée » |
| E11 | C | `docs/catalogue/blocs-observes.md:71` | **Confirmé** : TI du PID_INT en dixièmes de seconde (M20 p. 140). La FA403904 (ms) ne s'applique pas au PID_INT. | Garder la vérification sur le programme « EB_PID » |
| E12 | B | `engine/catalogue/schneider.json:297` (« PI_B (p. 4805 du texte) ») ; `docs/catalogue/pidff-conversion.md:48` | **Page réelle** : table Para_PI_B à M35 p. 155 et M36 p. 157 ; sens à M35 p. 160 et M36 p. 162. **Nuance** : PI_B ne numérote l'inverse que des **libellés** de la table du PIDFF (M35 p. 167). Son **comportement** est identique : rev_dir = 1 donne sense = +1 dans les deux blocs (M35 p. 160 et p. 178). | Remplacer « p. 4805 du texte » par ces pages. Écrire « libellés inversés, comportement identique ». |
| E13 | B | `engine/catalogue/schneider.json:318-336` (PI_B) | **Levé** : bibliothèque Control, famille *Controller*, EFB (M35 p. 28, ch. 16 p. 151–162). Paramètres, équations, modes et statut sont lus (voir pi_b.md). | Renseigner `library`, `parameters` et `equation` depuis M35 p. 155–161 ; garder la conversion refusée tant qu'aucun adaptateur n'est vérifié |
| E14 | C | `engine/catalogue/schneider.json:6-14, 33-42` | Empreintes vérifiées. MD5 = `checkSum` se.com. SHA-256 : M35 84353f38…, M36 9dcd557a…, M20 4e362bb1… (README, §1). | Ajouter les empreintes aux sources |
| E15 | B | `docs/science/scenario-pression-pompe-m580/schneider-m580.md:10, 14, 85` | **Périmé** : les PDF sont lus, et la disponibilité sur M580 est documentée (M35 p. 28, M20 p. 34). | Mettre à jour §0, §3 « Disponibilité M580 : non vérifiée » et C4/C5 |
| E16 | C | `docs/catalogue/blocs-observes.md:43` | **Confirmé** : rev_dir ↔ signe est lu (M35 p. 178 ; M36 p. 182) | Aligner le texte ; garder l'essai E4 |
| E17 | A | `engine/block_catalogue.py:70-75` | Inchangé. La disponibilité par gamme ne vaut pas qualification de la CPU. | L'interface doit afficher « Conversion non qualifiée » pour `not_verified` |

## 2. Nouveaux écarts

| # | Grav. | Fichier:ligne | Constat (source) | Correction proposée |
|---|---|---|---|---|
| E18 | A | `engine/block_adapters.py:115` | Le manuel donne « −10 000 < KP < +10 000 », bornes strictes (M20 p. 140). L'adaptateur accepte \|KP\| = 10000. | Refuser \|KP\| ≥ 10000 (hors de ce lot) |
| E19 | B | `engine/block_adapters.py:117, 128` ; `docs/catalogue/pidint-conversion.md:25-26` | Il manque l'exigence d'une **tâche MAST ou FAST périodique** (inopérant en MAST cyclique) et d'une section non conditionnée (M20 p. 133, 136) | Ajouter un avertissement ou un « à déterminer » |
| E20 | B | `engine/catalogue/schneider.json:356` | La famille **CLC_INT**, de type « Procédure » (M20 p. 34, 127), n'est pas notée. La p. 136 parle d'« EF ». | Ajouter ces faits |
| E21 | C | `docs/catalogue/pidint-conversion.md` | Faits lus et non consignés : démarrage à chaud (état d'avant la coupure), ajout en ligne lié à la table PARA, RAZ des %MWi (M20 p. 133–134) ; anti-saturation annoncée sans équation (p. 136) ; PARA[10] non décrit (p. 140) | Ajouter |
| E22 | C | `docs/catalogue/blocs-observes.md:5` ; `engine/catalogue/schneider.json:44-50` (CAPTURE-PIDFF-1, « Parallel structure ») | Le même schéma figure dans le manuel sous le titre « Structure of the PIDFF controller » (M35 p. 171 ; M36 p. 175). Le titre « parallèle » vient de la FAQ000256113. | Citer M35 p. 171 comme source officielle du schéma |
| E23 | A | Toute documentation qui utiliserait le schéma « Mixed structure » de la FAQ000256113 | Ce schéma est générique (ERR = SP − PV, YMAN, ymax/ymin) et identique au schéma du PI_B (M35 p. 157). Ses noms et son signe ne sont pas ceux du PIDFF (dev = PV − SP, p. 168). | Ne pas l'utiliser pour une conversion |
| E24 | C | `engine/block_adapters.py:75` (`outbias = 0` neutre) | outbias n'apparaît pas dans les équations incrémentales (M35 p. 179), mais le schéma (p. 171) l'ajoute après rev_dir. Le neutre 0 est sans risque. | Noter que outbias n'agit qu'à ti = 0 (p. 175, 178) |
| E25 | B | `engine/block_adapters.py:47-50` | Incohérence de la structure parallèle **confirmée en EN** : K = α pour I et D (p. 172, 177), alors que TermI contient kp (p. 179) ; en mode intégral pur, TermI contient α (p. 181). | Citer les pages M35 en plus de M36 |
| E26 | C | `docs/catalogue/pidff-conversion.md:36` | **outrate** : aucune valeur neutre ou de désactivation n'est documentée (M35 p. 168 : « ≥ 0 »). Le dépôt le classe déjà « à déterminer ». | Conserver |

## 3. Documents désormais lus, et documents qui restent non lus

| Élément | État le 27/11/2026 (après lecture) |
|---|---|
| 33002535 (M35), 33002536 (M36), 33002520 (M20) | **Lus** pour les chapitres PIDFF, PI_B et PID_INT, et pour la disponibilité, le champ d'application, les règles générales et les codes d'erreur |
| FAQ000256113 (schémas) ; FA410692 (image du statut) | **Lus** |
| FA226088 (PID_INT, ARRAY[1..43]) | Non lu (HTTP 404). Le manuel confirme 43 entiers (M20 p. 139). |
| Table de correspondance du convertisseur PL7 (aide en ligne) | **Non lue** |
| Catalogue Control Expert (`iportal.se.com`) | Non lu ; remplacé par les tables de disponibilité des manuels |

## Suivi des corrections (2026-11-27)

- **E1** corrigé : fiche « PIFINT » retirée du catalogue ; `test_block_catalogue.py` et `test_pidff_conversion.py` mis à jour (« PIFINT » reste refusé comme bloc inconnu) ; `docs/catalogue/qualification.md` corrigé.
- **E2** corrigé : `kp` négatif remplacé par 0 avec le bit 4 (avertissement), pas un refus ; `block_adapters.py`, `schneider.json`, `docs/catalogue/pidff-conversion.md` et `blocs-observes.md` réécrits. Le test du catalogue vérifie désormais la lecture corrigée et l'absence de l'ancienne.
- Modèles exécutables ajoutés : `engine/block_pi_b.py`, `engine/block_pidff.py`, `engine/block_pid_int.py` (documentaires, non qualifiés ; lectures non tranchées par le manuel listées en INTERPRÉTATIONS dans chaque module). Vérification de bout en bout des conversions : `engine/test_block_conversion_closed_loop.py`.
- Constat PID_INT : la troncature entière laisse une erreur statique jusqu'à 1000·TI/(KP·TS) comptes (interprétation I3 du modèle) ; à afficher dans l'avertissement de conversion.
