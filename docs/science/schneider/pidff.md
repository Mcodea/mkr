# PIDFF — fiche (Control Expert, bibliothèque Control)

**Statut : documentaire, lu dans le manuel. « Conversion non qualifiée ».**

Sources de cette fiche :
- **M35** : 33002535.16, 09/2020, *Control Block Library*, chap. 17 « PIDFF: Complete PID controller », p. 163–189. C'est la référence principale.
- **M36** : 33002536.16, 09/2020, édition FR, chap. 17, p. 167–193, pour contrôler les pages citées par le dépôt.
- **FAQ** : FA410692, FAQ000256113, FAQ000279181, FAQ000279186, FA377339 (voir le README, §4).

## 1. Disponibilité, version, exécution

| Fait | Source |
|---|---|
| EFB de la famille *Controller* : M340 +, M580 +, Quantum +, Momentum +, Premium + | M35 ch. 2, p. 28 |
| Le manuel vaut pour Control Expert 15.0 et suivants. Unity Pro est l'ancien nom, jusqu'à 13.1. | M35 p. 13 ; p. 18 |
| CPU, micrologiciel, version de bibliothèque | **Non donnés** |
| Le bloc calcule lui-même l'intervalle entre deux exécutions et se place n'importe où dans la section. Un cadencement est possible par SAMPLETM sur EN. Au-delà d'une seconde, piloter le manuel avec MS ou MS_DB. | M35 « Scanning », p. 34 ; p. 164 |
| dt : « Time elapsed since the last function block execution » | M35 p. 177 |
| Aucune exigence de type de tâche (MAST/FAST) n'est écrite pour la bibliothèque Control | M35 p. 31–37 (absence) |
| Cas de support sur M580 avec CE V14.0 : le bit de réussite se fige au passage Manuel → Auto | FA377339 |

## 2. Équations lues (M35 ; identiques dans M36)

Elles sont lues sur les images des pages. Le texte en est résumé ici, sans reproduction des figures.

- **Fonction de transfert** (M35 p. 169 ; M36 p. 173) :
  - mixte : OUT = kp·[1 + 1/(ti·p) + td·p/(1 + (td/kd)·p)]·IN ;
  - parallèle : OUT = [kp + α/(ti·p) + α·td·p/(1 + (td/kd)·p)]·IN, avec α = (out_sup − out_inf)/(pv_sup − pv_inf).
  - IN n'est pas défini sur cette page. Les équations détaillées utilisent dev = PV − SP (Info_PIDFF, p. 168).
- **Formes de calcul** (p. 169) :
  - ti = 0, absolu : OUT = TermP + TermD + TermFF + outbias ;
  - ti > 0, incrémental : OUTD = TermP + TermI + TermD + TermFF, puis OUT = OUT(old) + OUTD(new).
- **Variables intermédiaires** (p. 177–178) :
  - DEV_WGH = PV − (1 − ovs_att)·SP ;
  - K = kp si `mix_par = 0` et kp ≠ 0 ; K = α si `mix_par = 1` ou kp = 0 ;
  - VAR = PV si `pv_dev = 0`, VAR = dev si `pv_dev = 1` ;
  - Δ(x(t)) = x(t) − x(t−1).
- **Sens d'action** (p. 178) : sense = +1 pour `rev_dir = 1`, et un écart PV − SP positif augmente la sortie ; sense = −1 pour `rev_dir = 0`.
- **Algorithme absolu, PD** (ti = 0, p. 178) :
  - TermP = sense·kp·dev ;
  - TermD = sense·[td·TermD(old) + K·td·kd·(VAR(new) − VAR(old))]/(kd·dt + td) ;
  - TermFF = (FF − ff_inf)(otff_sup − otff_inf)/(ff_sup − ff_inf) + otff_inf ;
  - OUT = limiter(OUT).
- **Algorithme incrémental, PID, `aw_type = 0`** (p. 179 ; M36 p. 183) :
  - TermP = sense·kp·Δ(DEV_WGH) ;
  - TermI = sense·kp·(dt/ti)·dev ;
  - TermD = Δ[ sense·(td·TermD(old) + K·td·kd·(VAR(new) − VAR(old)))/(kd·dt + td) ] ;
  - TermFF = Δ[ … ] ;
  - OUT = limiter(OUT) ;
  - OUT = OUT(old) + OUTD(new), ou RCPY + OUTD(new) si `en_rcpy = 1`.
- **Incrémental, `aw_type = 1`** (p. 180) :
  - OUTD = TermP + TermI + TermD + TermFF + TermAW ;
  - OUTc = OUTc(old) + OUTD(new) ;
  - OUT = limiter(OUTc) ;
  - TermAW = (dt/ti)·[OUT(old) − OUTc(old)], ou (dt/ti)·[RCPY − OUTc(old)] si `en_rcpy = 1`.
- **Mode intégral pur** (kp = 0, p. 181–182) : TermI = sense·α·(dt/ti)·dev, avec les mêmes deux variantes `aw_type`.
- **Contrôle analytique de TermD.** Si la grandeur entre crochets est la composante absolue, la récurrence donne en continu D = K·td·p/(1 + (td/kd)·p)·VAR, ce qui concorde avec p. 169. Le manuel emploie pourtant « TermD(old) » à l'intérieur d'un Δ[…] : le symbole est **ambigu**. L'interprétation de `engine/pidff_reference.py:11-14` (composante absolue X, puis TermD = X(k) − X(k−1)) reste la seule cohérente. C'est une **lecture**, pas un texte du manuel.

## 3. Paramètres (Para_PIDFF, M35 p. 167–168 ; M36 p. 171–172)

| Élément | Type | Ce que dit le manuel |
|---|---|---|
| pv_inf, pv_sup | REAL | Plage de la mesure ; ce sont aussi les bornes de la consigne (p. 175). Égalité : erreur, bit 7 (p. 188). |
| out_inf, out_sup | REAL | Plage de sortie : limites de OUT en manuel et en tracking (p. 175–176) |
| out_min, out_max | REAL | Limites de OUT en automatique (p. 175–176) ; ramenées dans [out_inf, out_sup] (p. 188) |
| rev_dir | BOOL | Table p. 167 : « 0 : opposite », « 1 : direct ». **Contredit p. 178** (rev_dir = 1 est « opposite », sense +1). Le comportement est celui de p. 178. |
| mix_par | BOOL | 0 : mixte, 1 : parallèle |
| aw_type | BOOL | « 1 : Anti-windup halt is filtered » (p. 167) ; sens détaillé p. 173 (§4) |
| en_rcpy | BOOL | 1 : RCPY utilisé ; dans ce cas, OUT n'est pas limité (p. 173) |
| kp | REAL | « Proportional action coefficient (gain) ». **L'unité n'est pas écrite** ; grandeurs en unités physiques (p. 164). Négatif : remplacé par 0, avec avertissement (p. 188–189). kp = 0 donne le mode intégral pur. |
| ti, td | TIME | Temps intégral et temps dérivé. ti = 0 donne l'algorithme absolu (p. 172). |
| kd | REAL | Gain du filtre de dérivée. kd < 1 avec td ≠ 0 : le bloc prend 1 (p. 188). |
| pv_dev | BOOL | 0 : dérivée sur la mesure ; 1 : sur l'écart |
| bump | BOOL | 0 : passage en automatique sans à-coup ; 1 : avec à-coup |
| dband, gain_kp | REAL | Zone morte et pente dans la zone morte ; l'écart modifié alimente P et I (p. 174). Négatifs : remplacés par 0. |
| ovs_att | REAL | Pondération de consigne, active si ti > 0 ; 0 donne l'écart classique (p. 173). Hors de [0, 1] : borné. |
| outbias | REAL | Décalage ; il sert si ti = 0 (p. 175) et figure dans les équations absolues seulement (p. 169, 178). Recalculé au passage sans à-coup quand ti = 0 (p. 176). |
| outrate | REAL | Limite de variation, « in units per second (≥ 0) » (p. 168). Négatif : remplacé par 0. **La valeur qui désactive la limite n'est pas donnée.** |
| ff_inf, ff_sup, otff_inf, otff_sup | REAL | Si ff_sup = ff_inf, l'anticipation est ignorée (p. 175) |

## 4. Anti-emballement, modes, statut

- **`aw_type`** (p. 173 ; M36 p. 177).
  - `aw_type = 0` est la mesure anti-emballement **de base de la forme incrémentale**. La sortie peut quitter brusquement sa limite même si le signe de l'écart n'a pas changé.
  - `aw_type = 1` empêche la sortie de quitter sa limite tant que l'écart ne change pas de signe.
  - **La FAQ000279186 (« 0 = no anti-reset windup ») contredit le manuel.**
- **Modes** (p. 176).
  - Tracking (TR_S = 1) prioritaire : OUT = TR_I, dans [out_inf, out_sup].
  - Manuel : OUT est écrit par l'utilisateur, dans [out_inf, out_sup].
  - Automatique : dans [out_min, out_max].
  - Le passage vers automatique se fait selon `bump`.
  - Règles communes de la bibliothèque : p. 32–33.
- **Statut** (p. 35 et 188–189).
  - Bits 0 à 3 : erreurs flottantes.
  - Bit 4 : une valeur a été bornée. Cas listés : SP hors de [pv_inf, pv_sup] ; kp, dband, gain_kp ou outrate négatif ; kd < 1 avec td ≠ 0 ; ovs_att hors de [0, 1] ; out_min/out_max hors de [out_inf, out_sup] ; outbias, otff_* hors de leur plage.
  - Bits 5 et 6 : OUT a atteint out_min ou out_max.
  - Bit 7 : pv_inf = pv_sup.
  - En cas d'erreur, OUT, OUTD, MA_O et INFO restent inchangés.
  - Codes : 30110 (W_WARN_OUT_OF_RANGE), −30121 (16#8A57, E_ERR_NULL_INPUT_SCALE) (p. 354) ; erreurs flottantes p. 359.
- **Migration PL7** : aucune mention du PL7 dans M35 ni dans M36.

## 5. Comparaison ligne à ligne avec le dépôt (lecture seule)

| Élément du dépôt | Manuel | Verdict |
|---|---|---|
| `engine/block_adapters.py:38-39` : fonction de transfert mixte, Kp = kp, Ti = ti, Td = td, Tf = td/kd | M35 p. 169 / M36 p. 173 | **Conforme** |
| `block_adapters.py:40-41,74` : rev_dir = 0 si Kp > 0, rev_dir = 1 si Kp < 0, kp = \|Kp\| | M35 p. 178 / M36 p. 182 ; u = Kp(SP − PV) = −Kp·dev | **Conforme** au comportement. Les libellés de la table (p. 167 / p. 171) sont inversés. |
| `block_adapters.py:42` : « The block refuses a negative kp (status 080) » | M35 p. 188–189 | **Inexact.** Un kp négatif est remplacé par 0 (avertissement, bit 4). 080 = bits 4 + 6. La conduite de l'adaptateur reste juste. |
| `block_adapters.py:47-50` : refus de la structure parallèle | p. 172 et p. 177 (K = α pour I et D) contre p. 179 (TermI contient kp) ; p. 181 (TermI contient α si kp = 0) | **Incohérence confirmée en EN** : le refus est justifié |
| `block_adapters.py:63-64` : refus si kd < 1 | p. 188 | **Conforme** |
| `block_adapters.py:75` : neutres ovs_att = 0, dband = 0, ff_sup = ff_inf, outbias = 0, en_rcpy = 0 | p. 173, 174, 175, 169/178 | **Conformes**. outbias n'intervient pas quand ti > 0 (p. 179) ; gain_kp est sans effet quand dband = 0. |
| `block_adapters.py:76` : à déterminer (outrate, aw_type, bump, plages, dt) | p. 167–168, 173, 176 | **Conforme.** outrate n'a pas de valeur neutre documentée. |
| `block_adapters.py:80-81` et `docs/catalogue/pidff-conversion.md:52` : TermI intègre l'écart du cycle courant | p. 179 | **Conforme** |
| `docs/catalogue/pidff-conversion.md:3,9,22,30-34,40,47,52` : pages FR 167–187, 173, 177, 178, 179, 182, 183, 176, 171 | M36 : vérifiées une à une | **Exactes pour M36.** Équivalents M35 : −4 pages. |
| `pidff-conversion.md:48` : « PI_B numérote rev_dir à l'inverse » | PI_B p. 155 et p. 160 contre PIDFF p. 167 et p. 178 | Vrai pour les **libellés** de la table du PIDFF seulement. Le **comportement** est identique (sense = +1 pour rev_dir = 1 dans les deux blocs). |
| `engine/pidff_reference.py:4-8` : `aw_type = 0` décrit comme cas simple | p. 173 | À préciser : c'est l'anti-emballement de base. Sans limites, l'oracle n'en est pas affecté. |

## 6. Ce qui manque pour qualifier

1. CPU, micrologiciel et version de Control Expert de la cible. Si la version est antérieure à 15.0, ces manuels ne s'appliquent pas tels quels.
2. Pour une conversion parallèle : la clarification de TermI (p. 179) par Schneider.
3. La valeur de outrate qui désactive la limite.
4. Le comportement réel des limiteurs et de `aw_type = 1`.
5. Les essais de banc E0 à E6, en tenant compte de FA377339 sur M580.
