# PI_B — fiche (Control Expert, bibliothèque Control)

**Statut : documentaire, lu dans le manuel. Spécification suffisante pour un adaptateur DOCUMENTAIRE de PI (structure mixte, ti > 0, `en_rcpy = 0`). « Conversion non qualifiée ».** Le dépôt refuse toujours toute conversion vers PI_B (`engine/test_pidff_conversion.py:120-123`). Ce lot n'écrit pas d'adaptateur.

Sources de cette fiche :
- **M35** : 33002535.16, 09/2020, chap. 16 « PI_B: Simple PI controller », p. 151–162. C'est la référence principale.
- **M36** : 33002536.16, chap. 16, p. 153–164 (pages EN + 2).

## 1. Disponibilité, version, exécution

| Fait | Source |
|---|---|
| EFB de la famille *Controller* : M340 +, M580 +, Quantum +, Momentum +, Premium + | M35 p. 28 |
| Manuel valable pour Control Expert ≥ 15.0 ; CPU et micrologiciel non donnés | M35 p. 13 |
| Fonctions dérivées de celles du PIDFF ; « for difficult control tasks… the PIDFF block should be used » | M35 p. 152 |
| dt = temps depuis la dernière exécution, calculé par le bloc ; pas d'exigence de tâche écrite | M35 p. 34, 160 |

## 2. Interface et paramètres (M35 p. 154–155)

- **Entrées et sorties.**
  - Entrées : PV, SP, RCPY, TR_I (REAL) ; MAN_AUTO, TR_S (BOOL) ; PARA (`Para_PI_B`).
  - Entrée/sortie : OUT (REAL).
  - Sorties : OUTD (REAL), MA_O (BOOL), **DEV = PV − SP** (REAL), STATUS (WORD).
- **Para_PI_B** :
  - id (UINT) ;
  - pv_inf, pv_sup, out_inf, out_sup (REAL) ;
  - **rev_dir** (BOOL) : « 0 : direct action », « 1 : opposite action » ;
  - en_rcpy (BOOL) ;
  - **kp** (REAL) ;
  - **ti** (TIME) ;
  - dband, outbias (REAL).
- **Absents par rapport au PIDFF** : td, kd, pv_dev, mix_par, aw_type, bump, ovs_att, gain_kp, outrate, out_min/out_max, FF.

## 3. Équations lues (images des p. 156, 158, 160–161)

- **Fonction de transfert** : OUT = kp·(1 + 1/(ti·p))·IN (p. 156).
- **Formes de calcul** (p. 156) :
  - ti = 0 : OUT = TermP + outbias, puis OUTD = OUT(new) − OUT(old) ;
  - ti > 0 : OUTD = TermP + TermI, puis OUT = OUT(old) + OUTD(new).
- **Sens d'action** (p. 160) : sense = +1 pour `rev_dir = 1` (« opposite action »), et un écart PV − SP positif augmente la sortie ; sense = −1 pour `rev_dir = 0` (« direct action »). **Les libellés de la table p. 155 sont cohérents avec cette page**, contrairement au PIDFF.
- **Algorithme absolu** (ti = 0, p. 160) :
  - TermP = sense·kp·DEV ;
  - OUT = TermP + outbias, puis OUT = limiter(OUT).
- **Algorithme incrémental** (ti > 0, p. 161) :
  - OUTD = TermP + TermI ; OUT = limiter(OUT) ;
  - OUT = OUT(old) + OUTD(new), ou RCPY + OUTD(new) si `en_rcpy = 1` ;
  - TermP = sense·kp·Δ(DEV) ;
  - TermI = sense·kp·(dt/ti)·DEV si kp > 0 ;
  - TermI = sense·(out_sup − out_inf)/(pv_sup − pv_inf)·(dt/ti)·DEV si kp = 0 (intégral pur).
- **Zone morte** : sous dband en valeur absolue, l'écart utilisé est nul (p. 158). Il n'y a pas de gain_kp.

**Équivalence avec MKR** (lecture, non vérifiée numériquement). Avec ti > 0, `en_rcpy = 0` et dband = 0, la forme est celle de la structure mixte du PIDFF sans dérivée. La correspondance serait :
- kp = |Kp| ;
- ti = Ti ;
- rev_dir = 1 si Kp < 0, sinon 0 (MKR : u = Kp·(SP − PV) = −Kp·DEV).

## 4. Limites, modes, statut

- **Bornes** (p. 159) :
  - OUT est borné à [out_inf, out_sup] **dans tous les modes** ;
  - OUTD ne tient pas compte de ce bornage ;
  - la consigne est bornée à [pv_inf, pv_sup].
- **Anti-emballement.** Il n'y a pas de paramètre. Le schéma p. 157 porte « Anti-Windup-Reset ». L'effet anti-emballement vient du limiteur appliqué à la forme incrémentale. **L'ordre exact entre le limiteur et OUT(old) n'est pas explicite** à la p. 161 : « OUT = limiter(OUT) » y est écrit avant « OUT = OUT(old) + OUTD ».
- **Modes** (p. 159) :
  - Automatique : TR_S = 0, MAN_AUTO = 1 ;
  - Manuel : TR_S = 0, MAN_AUTO = 0 ;
  - Tracking : TR_S = 1, prioritaire.
  - Le passage vers automatique est **sans à-coup si ti > 0** et **avec à-coup si ti = 0**. Il n'y a pas de paramètre `bump`.
- **Statut** (p. 162) :
  - bit 4 : SP hors de [pv_inf, pv_sup], kp ou dband négatif (remplacé par 0), ou outbias hors de plage ;
  - bits 5 et 6 : seuils « out_min » et « out_max » atteints ;
  - bit 7 : pv_inf = pv_sup. **128 = bit 7**, ce qui concorde avec l'extrait de la FAQ FA377379.
  - En cas d'erreur, OUT, OUTD, MA_O et DEV restent inchangés.
  - Codes : 30110 et −30121 (p. 354).
- **Migration PL7** : aucune mention.

## 5. Incohérences relevées dans le chapitre

1. Le **schéma p. 157** est générique : ERR = SP − PV, « gain », YMAN, ymax/ymin, qmax/qmin. Il ne correspond ni aux noms de Para_PI_B ni à DEV = PV − SP (p. 155). Le même schéma est publié comme « Mixed structure of the PIDFF » dans la FAQ000256113. **Ne pas s'en servir.**
2. Les bits 5 et 6 (p. 162) citent **out_min/out_max**, qui n'existent pas dans Para_PI_B. Les limites réelles sont out_inf/out_sup (p. 159).
3. La p. 156 ne définit pas IN ; les équations détaillées utilisent DEV.

## 6. Suffisance pour un adaptateur : verdict

**Suffisante pour un adaptateur documentaire limité à :**
- PI ;
- ti > 0 ;
- `en_rcpy = 0` ;
- dband = 0 ;
- kp > 0 ;
- sens par `rev_dir`.

Ce domaine reprend celui de l'adaptateur `pidff` en PI.

**Ce qui manque encore**, et qui interdit toute qualification :
1. **L'unité de kp** : elle n'est pas écrite. Elle se déduit des unités physiques de PV et OUT (p. 152), mais ce n'est qu'une déduction. Il faut l'exiger explicitement de l'utilisateur.
2. **L'ordre du limiteur dans l'algorithme incrémental** (p. 161), donc le comportement exact en saturation.
3. **L'initialisation de Δ(DEV)** au premier cycle et après un passage Manuel ou Tracking → Auto.
4. **La résolution de TIME** pour ti : elle n'est pas donnée par ce manuel.
5. **La période** : dt est mesuré par le bloc, et la boucle échantillonnée réelle dépend de la tâche, que le manuel n'impose pas.
6. **La cible** : CPU, micrologiciel, version de Control Expert ≥ 15.0 sur l'installation.
7. **Les preuves** : un oracle écrit depuis p. 160–161, une vérification numérique de convergence (comme pour PIDFF), puis les essais de banc E0 à E6.
8. **La décision sur les incohérences** du §5 (schéma p. 157, bits 5/6).

**À ne pas faire :**
- réutiliser l'adaptateur `pidff` tel quel, car les paramètres et les limites diffèrent (pas d'out_min/out_max, pas d'ovs_att) ;
- confondre PI_B avec PID_INT, dont l'écart et les unités sont opposés.
