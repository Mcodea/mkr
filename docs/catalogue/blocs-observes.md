# Blocs observés dans les programmes du mainteneur (24/11/2026)

Source : quatre captures d'écran fournies par le mainteneur. **Elles ne sont pas redistribuées** dans ce dépôt public (aides protégées). Seuls les noms de broches et l'ordre des blocs sont transcrits. Aucune équation, aucune valeur par défaut, aucune unité n'en est déduite. Les faits sont dans `engine/catalogue/schneider.json` (sources `CAPTURE-*`, édition « non identifiée »).

## 1. PIDFF (Schneider, Control Expert) — structure parallèle

Ce que le schéma montre, dans l'ordre du signal :
1. **Consigne :** une atténuation de dépassement (`ovs_att`) produit un écart pondéré `DEV_WGH`, qui alimente l'action **proportionnelle** (`kp`).
2. **Écart :** `dev` alimente l'action **intégrale** (`ti`, `K`).
3. **Dérivée :** un sélecteur `pv_dev` précède l'action **dérivée** (`td`, `kd`, `K`).
4. **Zone morte :** un bloc à zone morte (`dband`, `gain_kp`) précède les actions P et I.
5. **Somme :** les trois actions sont additionnées, puis `rev_dir` donne le sens d'action.
6. **Après la somme :** l'anticipation `FF` (bornes `ff_sup`, `ff_inf`, `otff_sup`, `otff_inf`) et le décalage `outbias` sont ajoutés. Viennent ensuite le limiteur de variation (`outrate`) et le limiteur `out_min`/`out_max`.
7. **Sortie :** la commutation `MAN_AUTO` et le suivi `TR_I`/`TR_S` précèdent un limiteur `out_sup`/`out_inf`, puis `OUT`.

Broches de l'instance vue : entrées `PV`, `SP`, `FF`, `RCPY`, `MAN_AUTO`, `PARA`, `TR_I`, `TR_S` ; sorties `OUT`, `OUTD`, `MA_O`, `INFO`, `STATUS`.

### Conséquences pour MKR (aucune ne suppose une équation)

| Élément | MKR (générique) | PIDFF d'après le schéma | Conséquence |
|---|---|---|---|
| Action P | sur l'erreur e = SP − PV | sur l'écart pondéré `DEV_WGH` (atténuation `ovs_att`) | Équivalence seulement si `ovs_att` est à sa valeur neutre, à établir dans la documentation |
| Action I | sur l'erreur | sur `dev`, après la zone morte | Équivalence seulement avec `dband` nulle et `gain_kp` neutre |
| Action D | sur la mesure, filtre Euler implicite | sélecteur `pv_dev`, facteur `kd` | La place de D, le filtre et `kd` sont à documenter ; MKR ne propose le PID qu'en simulation |
| `K` | absent | intervient dans I et D | Facteur lié aux plages (cf. fait `SE-PIDFF-167`) : sa définition exacte est à lire |
| Anticipation, décalage, taux de variation | absents du réglage | présents | À neutraliser pour l'essai d'équivalence |
| Suivi, manuel/automatique | simulés (transfert PI) | `MAN_AUTO`, `TR_I`, `TR_S` | Le comportement au retour en automatique est un essai du protocole |

**Conclusion : la conversion reste « non qualifiée ».** La structure confirme qu'elle ne se réduit pas à recopier Kp, Ti et Td : des fonctions annexes (pondération, zone morte, anticipation, limiteurs) doivent d'abord être neutralisées. La liste est maintenant dans l'essai E0 du protocole.

### Règles officielles relevées par recherche (le 24/11/2026)

Les recherches sur les sites Schneider et Siemens ont renvoyé des **résumés de pages**, pas les pages elles-mêmes : l'hôte était hors de la liste réseau de la session. Les règles ci-dessous sont donc consignées avec leur source et le niveau de preuve « relayé par un moteur de recherche, à relire à la source » (`engine/catalogue/schneider.json`).

| Règle | Source |
|---|---|
| `kp`, `dband` ou `gain_kp` négatif : statut 080, valeur remplacée par 0, **pas de sortie**. **Un Kp négatif est refusé.** | [FAQ FA410692](https://www.se.com/us/en/faqs/FA410692/) |
| `kd` < 1 avec `td` non nul : le bloc utilise 1. `ovs_att` hors de [0, 1] est ramené à 0 ou à 1. | [FAQ FA410692](https://www.se.com/us/en/faqs/FA410692/) |
| `ovs_att` va de **0 (aucune atténuation)** à 1 (maximale). La valeur neutre pour l'essai E0 est donc 0. | [FAQ FAQ000279181](https://www.se.com/us/en/faqs/FAQ000279181/) |
| `pv_inf` = `pv_sup` ou entrée non flottante : erreur. | [FAQ FA410692](https://www.se.com/us/en/faqs/FA410692/) |
| Les équations détaillées de la structure mixte sont dans l'aide en ligne. Le support signale le schéma mixte manquant. | [FAQ FAQ000256113](https://www.se.com/us/en/faqs/FAQ000256113/) |

**Conséquence directe pour MKR.** MKR calcule un Kp **signé** (négatif pour un procédé à gain négatif). Le PIDFF refuse un Kp négatif : le sens d'action doit passer par `rev_dir`, avec |Kp|. La correspondance exacte `rev_dir` ↔ signe n'est pas lue dans une source ; elle est donc un essai du protocole (E4).

## 2. PID_Compact (Siemens, TIA Portal)

**Règles officielles relevées par recherche** (même niveau de preuve) :
- **Gain négatif non pris en charge :** pour qu'une sortie plus forte réduise la PV, la configuration coche « Invert control logic » et le gain est la valeur absolue du gain signé. Un ancien fil de forum (TIA V11, bloc V1.0) dit l'inverse : il date d'une version antérieure aux manuels actuels, à ne pas suivre.
- **Temps en secondes :** Ti et Td s'expriment en secondes (V2.x), alors que le PID du S7-200 les exprimait en minutes.
- **Manuel :** une valeur hors limites est limitée dans la CPU aux limites configurées.
- **Équations :** ce sont des images dans les manuels. Documents de référence par version : [V2.0 et V3.0 (TIA Portal V17)](https://cache.industry.siemens.com/dl/files/401/100746401/att_1124946/v2/100746401_S71x00_PidCompact_DOC_V3.0_en.pdf), [V2.2](https://cache.industry.siemens.com/dl/files/707/79047707/att_915339/v2/79047707_PidCompactV2_2_DOC_V2_0_1_en.pdf) et le [manuel de régulation S7-1200/S7-1500](https://support.industry.siemens.com/cs/attachments/108210036/s71500_pid_control_function_manual_en-US_en-US.pdf).

**Observation sur l'instance vue.** Instance `PID_Compact_2` dans le DB `%DB2`. Broches visibles : `Setpoint`, `Input`, `Input_PER`, `ManualEnable`, `ManualValue` ; sorties `Output`, `Output_PER`, `Output_PWM`, `State`, `Error`, `ErrorBits`. La liste est tronquée (flèche d'extension) et la version du bloc n'est pas affichée.

**Pour l'acquisition S7comm :** dans ce programme, la mesure, la sortie et la consigne sont dans la **mémoire de marqueurs**, donc lisibles sans toucher au DB d'instance (optimisé, illisible par S7comm). Exemple de cartographie, à confirmer sur l'automate :

| Signal | Adresse vue | Zone | Octet | Type |
|---|---|---|---|---|
| PV (`Input`, « ANALOG OUT PH ») | `%MD200` | Mémentos | 200 | REAL32 |
| MV (`Output`, « PUMP RATE ALKALI ») | `%MD228` | Mémentos | 228 | REAL32 |
| SP (`Setpoint`) | `%MW222` | Mémentos | 222 | INT16 à mettre à l'échelle (l'étendue du mot est à confirmer) |

Pour un essai en boucle ouverte, l'entrée `ManualEnable` (`%M10.3`) passe le bloc en manuel, et la sortie `Output` suit alors `ManualValue` (`%MW220`). MKR enregistre l'`Output` appliqué, pas l'ordre de l'opérateur.

## 3. Bloc « PID » (étiqueté EB_PID, MODE/PARA/YMAN)

Champs vus dans les affectations : `MODE.en_p`, `en_i`, `en_d` (mises à 1), `MODE.man` ; `PARA.gain`, `ti` et `td` (type TIME, après `INT_TO_TIME`), `ymin`, `ymax`.

**Identité non établie.** Le nom du type de bloc, la bibliothèque et la version ne sont pas visibles, et ce n'est pas certain que ce soit PID_INT. Les recherches sur les sites Schneider n'ont trouvé ni « PIDINT » ni « PID_INT » dans la documentation de Control Expert ; le mainteneur désigne le document 33002520K01000 pour PID_INT (non lu). Les seuls PID à entrées entières trouvés sont dans une autre famille (Machine Expert, `FB_PID` de la bibliothèque TwidoEmulationSupport, entrées `i_iKp`, `i_iTi`, `i_iTd`), qui n'est pas confirmée comme étant ce bloc. Il n'a donc pas de fiche.

**Point à vérifier sur le programme :** `PID_I` est un entier converti par `INT_TO_TIME`. L'unité de cet entier (millisecondes, secondes, dixièmes de seconde) change le Ti d'un facteur 10 à 1000. MKR calcule Ti en secondes : la saisie doit être convertie explicitement.

## 4. Questions pour le mainteneur
1. **Toujours ouverte :** quel est le nom exact, la bibliothèque et la version du bloc de la capture « EB_PID » ? Dans Control Expert, l'Explorateur de bibliothèques, ou un clic droit sur le bloc puis ses propriétés, l'indique. Le mainteneur a précisé le 24/11/2026 : bloc non identifié, à laisser tel quel ; il ne faut établir aucune correspondance non documentée.
2. **Réduite :** l'aide PIDFF est le document 33002535 (Control Block Library). L'édition et la version de Control Expert restent à relever.
3. **Toujours ouverte :** PID_Compact V2.x ou V3.x ? (Visible dans le projet ou les propriétés du bloc.)
4. Le procédé « ANALOG OUT PH » est-il un pH ? La courbe de titrage est très non linéaire : un seul modèle linéaire ne vaut qu'autour d'un point de fonctionnement, et il faut identifier à plusieurs points.

## 5. Documents désignés par le mainteneur (24/11/2026) et état de lecture

| Bloc | Document | Lu ? |
|---|---|---|
| PIDFF | Schneider 33002536 (09/2020, .16), chapitre 17 | **Oui**, le 24/11/2026 (PDF fourni). Voir [pidff-conversion.md](pidff-conversion.md) |
| PID_INT | Schneider 33002520 (09/2020, .20), chapitre 30 | **Oui**, le 24/11/2026. Voir [pidint-conversion.md](pidint-conversion.md) (PI seulement ; dérivée sans équation) |
| PID_Compact V2.x et V3.x | Siemens, *PID control* (S7-1200, S7-1500) | PDF fourni, lecture en cours |
| PIDFF, schémas internes | FAQ000256113 | Non (la structure est dans le document 33002536) |
