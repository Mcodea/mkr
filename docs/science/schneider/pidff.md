# PIDFF — fiche (Control Expert, bibliothèque de régulation)

**Statut : documentaire partiel. « Conversion non qualifiée ».** Je n'ai lu aucune page du manuel. Les PDF sont refusés par le proxy de la session (voir le README, §1).

## 1. Sources

| Source | Niveau | Ce qui est utilisable |
|---|---|---|
| 33002535K01000 (EN, v16, 06/09/2020) : page du document lue | D2 (métadonnées) | Titre « Control Block Library », version, date, fichier, empreinte |
| 33002536K01000 (FR, v16, 05/09/2020) : page du document lue | D2 (métadonnées) | Idem. C'est l'édition que cite le dépôt (chap. 17, p. 167–187). |
| Contenu de 33002536, transcrit dans le dépôt le 24/11/2026 | D1m | Équations et pages : `docs/catalogue/pidff-conversion.md`, `engine/catalogue/schneider.json:116-122, 276-313` |
| FAQ000256113 | D2 (texte) ; schémas non lus | « The complete structure diagram for the PIDFF is shown below (Note: Reference the Control Expert online help for more information pertaining to the detailed equations relative to the structure diagram) ». Il suit deux images, « pidff » et « mixed mode », sur `se.my.site.com` (refusé). |
| FA410692 | D2 | Avertissements, erreurs, codes (§4) |
| FAQ000279181, FAQ000279186 | D2 | `ovs_att` et `aw_type` |
| FA377339 | D2 | Cas sur M580 avec Control Expert V14.0 |
| FA273873 | D2 | PARA est une structure (`para_pid.out_inf:=100.0`) |
| Catalogue Control Expert (`iportal.se.com`) | D1s | « CONT_CTL » : disponible dans toutes les versions de Control Expert ; compatible M340, M580, M580 Safety, Momentum, Premium et Quantum ; PIDFF = PID « parallel or mixed structure » ; AUTOTUNE pour PIDFF ou PI_B. **Extrait non relu.** |

## 2. Structure mathématique

Il n'y a pas d'équation lue par moi. Rappel du dépôt (D1m, 33002536) :
- **Structure mixte** (`mix_par = 0`, p. 173) : OUT = kp·[1 + 1/(ti·p) + td·p/(1 + (td/kd)·p)]·IN, avec IN = PV − SP.
- **Structure parallèle** (`mix_par = 1`, p. 173) : OUT = [kp + α/(ti·p) + α·td·p/(1+(td/kd)·p)]·IN, avec α = (out_sup − out_inf)/(pv_sup − pv_inf). Le dépôt relève une incohérence avec la p. 183.
- **Algorithme incrémental** si ti > 0 (p. 183, `aw_type = 0`), absolu si ti = 0 (p. 182). dt est le temps écoulé depuis la dernière exécution.
- **Pondération de consigne** : DEV_WGH = PV − (1 − ovs_att)·SP (p. 177).
- **Dérivée** sur la mesure (`pv_dev = 0`) ou sur l'écart (`pv_dev = 1`), filtre du premier ordre td/kd.
- **Sens d'action** : `rev_dir = 1` donne une sortie plus haute si PV − SP > 0 (p. 182). Les libellés « direct/inverse » se contredisent entre la p. 171 et la p. 182.

Confirmé par une FAQ (D2) :
- La FAQ000256113 dit seulement que le schéma de la structure mixte manque dans l'aide, et que les équations détaillées sont dans l'aide en ligne. **Elle ne confirme aucune équation sous forme de texte.**
- **`aw_type`** (FAQ000279186) : « The PIDFF function block uses the bumpless back calculation method. A value of 0 is equal to no anti-reset windup. A value of 1 is equal anti-reset windup. » L'algorithme du dépôt (p. 183, `aw_type = 0`) correspond donc au cas **sans** anti-emballement.
- **`ovs_att`** (FAQ000279181) : coefficient de 0 à 1 ; « A value equal to 1 provides the maximum attenuation. A value equal to 0 provides no attenuation. » La valeur neutre 0 est confirmée.

## 3. Paramètres, unités, plages

| Paramètre | Ce qui est sourcé | Niveau |
|---|---|---|
| kp | ≥ 0 en pratique : une valeur négative est remplacée par 0, avec un avertissement | D2 (FA410692) |
| dband, gain_kp, outrate | Une valeur négative est remplacée par 0, avec un avertissement | D2 (FA410692) |
| kd | kd < 1 avec td ≠ 0 : le bloc utilise 1 | D2 (FA410692) |
| ovs_att | Hors de [0, 1] : le bloc utilise 0 ou 1 (la FAQ écrit « [0.1] ») | D2 (FA410692, FAQ000279181) |
| out_min, out_max | Hors de [out_inf, out_sup] : le bloc utilise out_inf ou out_sup | D2 (FA410692) |
| outbias, otff_inf, otff_sup | Hors de [(out_min − out_max), (out_max − out_min)] : le bloc utilise la borne | D2 (FA410692) |
| pv_inf, pv_sup | Égaux : erreur E_ERR_NULL_INPUT_SCALE (−30121, 16#8A57) | D2 (FA410692) |
| ti, td | Type TIME [D1m]. Une FAQ sur un « PID » non nommé indique TIME en ms et %MW en ms (FA403904, D2). | D1m / D2 indirect |
| mix_par, rev_dir, pv_dev, en_rcpy, bump | BOOL et rôles [D1m] | D1m |
| Grandeurs | En unités physiques, de type REAL. Une entrée non flottante provoque une erreur. | D2 (FA410692, « non-floating point… at an input ») ; D1m |

## 4. Statut, erreurs, conditions d'exécution (FA410692, D2)

- **Statut 080** (décimal) = bits 4 et 6. Le bit 4 est mis à 1 si l'un des cas d'une liste est vrai. **Cette liste est une image, non lue.**
- **Note sur OUT** : en manuel, les bits 5 et 6 restent à 1 pendant un seul cycle quand la valeur saisie dépasse un seuil. Le bloc les remet à 0 au cycle suivant.
- **Erreur** : entrée non flottante, problème de calcul flottant ou pv_inf = pv_sup. Dans ces cas, OUT, OUTD, MA_O et INFO restent inchangées.
- **Codes** : W_WARN_OUT_OF_RANGE 30110 (16#759E), E_ERR_NULL_INPUT_SCALE −30121 (16#8A57). Erreurs flottantes à partir de −30150, écrites dans %SW125.
- **FA377339** (M580, Control Expert V14.0) : le bit de réussite passe à 0 et se fige au passage Manuel → Auto. Contournement Schneider : réduire la fréquence du bit d'activation (enable). C'est une **limitation connue à intégrer à l'essai E5/E6**.
- Tâche (MAST ou FAST), période et cycle : **NV**. Le dépôt indique seulement que dt est mesuré par le bloc (D1m).

## 5. Disponibilité par gamme

| Gamme | État |
|---|---|
| M580 | Bloc employé sur M580 avec Control Expert V14.0 dans une FAQ officielle (FA377339). **Aucune table de compatibilité lue** : aucune version minimale. |
| M340, Momentum, Premium, Quantum | D1s seulement (extrait du catalogue). **NV.** |
| PL7 → Control Expert | **NV.** Aucune source lue ne dit si un bloc PL7 est converti en PIDFF. |

## 6. Écarts avec le dépôt

Le détail est dans [ecarts-depot.md](ecarts-depot.md) :
- **E2 (grave).** « Statut 080 : kp négatif, pas de sortie ». La FAQ dit autre chose : un kp négatif est **remplacé par 0 avec un avertissement**. Les sorties inchangées concernent les **erreurs**. Le lien entre statut 080 et kp négatif n'est pas établi par le texte.
- **E3.** `outrate`, `out_min`/`out_max`, `outbias` et `otff_*` sont bornés par le bloc (FA410692). Le catalogue ne le dit pas.
- **E4.** Les FAQ citées comme « non ouvertes » sont désormais lues : à requalifier en D2.
- **E5.** Des pages de 33002536 (FR) sont attribuées à 33002535 (EN) : la source `SE-PIDFF-167` cite « p. 167, 172–173 » de 33002535.
- **E6.** « Document non lu » est en contradiction avec les faits qui citent ses pages.
- **E7.** `aw_type = 0` signifie « sans anti-emballement » (FAQ). Le catalogue et la documentation doivent le dire explicitement.

## 7. Ce qui manque pour un adaptateur qualifiable

1. Relire les p. 171–183 dans **la même édition** que l'installation (EN 33002535 ou FR 33002536), avec la version de Control Expert.
2. Lever l'incohérence `mix_par = 1` (p. 176 / p. 183) ; d'ici là, la structure parallèle reste refusée.
3. Obtenir la sémantique exacte de l'anti-emballement (`aw_type = 1`) et du suivi, ainsi que la liste des cas du bit 4 (image de FA410692).
4. Version de Control Expert, CPU et micrologiciel de la cible, puis les essais de banc E0 à E6.
