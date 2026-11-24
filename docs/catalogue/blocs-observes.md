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

## 2. PID_Compact (Siemens, TIA Portal)

Instance `PID_Compact_2` dans le DB `%DB2`. Broches visibles : `Setpoint`, `Input`, `Input_PER`, `ManualEnable`, `ManualValue` ; sorties `Output`, `Output_PER`, `Output_PWM`, `State`, `Error`, `ErrorBits`. La liste est tronquée (flèche d'extension) et la version du bloc n'est pas affichée.

**Pour l'acquisition S7comm :** dans ce programme, la mesure, la sortie et la consigne sont dans la **mémoire de marqueurs**, donc lisibles sans toucher au DB d'instance (optimisé, illisible par S7comm). Exemple de cartographie, à confirmer sur l'automate :

| Signal | Adresse vue | Zone | Octet | Type |
|---|---|---|---|---|
| PV (`Input`, « ANALOG OUT PH ») | `%MD200` | Mémentos | 200 | REAL32 |
| MV (`Output`, « PUMP RATE ALKALI ») | `%MD228` | Mémentos | 228 | REAL32 |
| SP (`Setpoint`) | `%MW222` | Mémentos | 222 | INT16 à mettre à l'échelle (l'étendue du mot est à confirmer) |

Pour un essai en boucle ouverte, l'entrée `ManualEnable` (`%M10.3`) passe le bloc en manuel, et la sortie `Output` suit alors `ManualValue` (`%MW220`). MKR enregistre l'`Output` appliqué, pas l'ordre de l'opérateur.

## 3. Bloc « PID » (étiqueté EB_PID, MODE/PARA/YMAN)

Champs vus dans les affectations : `MODE.en_p`, `en_i`, `en_d` (mises à 1), `MODE.man` ; `PARA.gain`, `ti` et `td` (type TIME, après `INT_TO_TIME`), `ymin`, `ymax`.

**Identité non établie** : le nom du type de bloc, la bibliothèque et la version ne sont pas visibles, et ce n'est pas certain que ce soit « PIDINT ». Il n'a donc pas de fiche.

**Point à vérifier sur le programme :** `PID_I` est un entier converti par `INT_TO_TIME`. L'unité de cet entier (millisecondes, secondes, dixièmes de seconde) change le Ti d'un facteur 10 à 1000. MKR calcule Ti en secondes : la saisie doit être convertie explicitement.

## 4. Questions pour le mainteneur
1. Quel est le nom exact, la bibliothèque et la version du bloc de la capture « EB_PID » ? Est-ce le bloc que vous appelez PIDINT ?
2. Quel est le document d'origine et l'édition de l'aide PIDFF (numéro de document, version de Control Expert) ?
3. Pour PID_Compact : V2.x ou V3.x ? (Visible dans les propriétés du bloc ou le projet.)
4. Le procédé « ANALOG OUT PH » est-il un pH ? La courbe de titrage est très non linéaire : un seul modèle linéaire ne vaut qu'autour d'un point de fonctionnement, et il faut identifier à plusieurs points.
