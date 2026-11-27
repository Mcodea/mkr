# PID_INT — fiche (Control Expert, bibliothèque standard, famille CLC_INT)

**Statut : documentaire, lu dans le manuel. « Conversion non qualifiée ».** Ce nom remplace « PIFINT », erreur de nomenclature corrigée par le client.

Sources de cette fiche :
- **M20** : 33002520.20, 09/2020, *EcoStruxure™ Control Expert — Standard, Bibliothèque de blocs* :
  - partie III « CLC_INT » (p. 127) ;
  - chap. 29 « Introduction aux fonctions de régulation d'entiers » (p. 129–134) ;
  - chap. 30 « PID_INT : automate PID » (p. 135–141) ;
  - disponibilité, chap. 2 (p. 34).
- **FAQ** : FA137704 (PID de PL7 Pro).

## 1. Disponibilité, version, exécution

| Fait | Source |
|---|---|
| Famille CLC_INT, type « Procédure » : M340 +, M580 +, Quantum +, Momentum +, Premium + ; non défini par la CEI 61131-3 | M20 p. 34 |
| Appelé « fonction élémentaire (EF) » à la p. 136 : le libellé n'est pas cohérent avec « Procédure » (p. 34) | M20 p. 34, 136 |
| Manuel applicable à Control Expert ≥ 15.0 ; CPU et micrologiciel non donnés | M20 p. 19 |
| Programmation obligatoire dans une tâche **MAST ou FAST périodique**. La fonction est inopérante en MAST cyclique. La section ne doit pas être conditionnée. | M20 p. 133, 136 |
| Pas de limite au nombre de PID_INT : les modules d'E/S limitent en pratique | M20 p. 130 |
| Si la longueur du tableau de paramètres est insuffisante, la fonction n'est pas exécutée | M20 p. 133 |

## 2. Interface et paramètres

- **Appel** (p. 138–139) : PID_INT(Input_Tag STRING[8], Input_Unit STRING[6], Input_PV INT [0..10000], In_Out_Auto EBOOL, In_Out_Para ARRAY[n..m] OF INT, PID_Out INT [0..10000]).
  - In_Out_Auto : 0 = manuel, 1 = automatique.
  - In_Out_Para : « Tableau de 43 entiers » ; « Les 16 premières valeurs sont décrites ».
  - Si TI = 0, un décalage de 5000 est ajouté à OUT en automatique.

**Table PARA** (p. 140) :

| Rang | Paramètre | Unité / plage lue |
|---|---|---|
| PARA[0] | SP | Consigne interne, 0–10 000 |
| PARA[1] | OUT_MAN | Sortie manuelle, 0–10 000 |
| PARA[2] | KP | ×100, signé, sans unité, **−10 000 < KP < +10 000** (bornes strictes). Le signe fixe le sens : « négatif : avant, positif : arrière ». |
| PARA[3] | TI | Dixièmes de seconde, 0–20 000 |
| PARA[4] | TD | Dixièmes de seconde, 0–10 000 |
| PARA[5] | TS | Centièmes de seconde, 1–32 000. La période réelle est le multiple de la période de tâche le plus proche de TS. |
| PARA[6], [7] | OUT_MAX, OUT_MIN | Limites de sortie **en automatique**, 0–10 000 |
| PARA[8].0 | PV_DEV | Dérivée : 0 sur la mesure, 1 sur l'écart |
| PARA[8].4 | NO_BUMP | 0 : par à-coups, 1 : sans à-coups |
| PARA[9], [11–16] | PV_SUP, PV_INF, PV_MMI, SP_MMI | Valeurs ×100 pour le terminal (DINT sur deux mots). **PARA[10] n'est pas décrit** (lacune du tableau). |

## 3. Équations lues (p. 137 et 141 ; la p. 141 mêle texte et images)

- **Écart** : ε = SP − PV. Le schéma p. 137 montre la consigne bornée [0..10000] en entrée + et la mesure en entrée −.
- **Structure** (schéma p. 137) : intégrale (TI) et dérivée (TD·d/dt, sur la mesure ou sur l'écart selon PV_DEV) s'ajoutent à l'écart, puis le tout est multiplié par **KP** (forme mixte). Ensuite viennent le limiteur OUT_MAX/OUT_MIN et le commutateur AUTO/OUT_MAN.
- **TI = 0** : OUT = KP·[ε(t) + Dt]/100 + 5000, avec Dt = action dérivée.
- **TI > 0** : ΔOUT = KP·[Δε(t) + (TS/10·TI)·ε(t) + ΔDt]/100, puis OUT = OUT + ΔOUT. Le texte écrit « TI < 0 » : **coquille**, puisque TI = 0 est le cas sans intégrale.
- **Dérivée** : « Dt = action dérivée », **sans équation ni filtre**.
- **Unités cohérentes** : TS est en 0,01 s et TI en 0,1 s, donc TS/(10·TI) = Ts/Ti, sans dimension.

## 4. Modes, démarrage, limites

- **Démarrage à froid** : mode manuel, sorties à 0 (p. 134, 141). L'initialisation voulue se programme **après** l'appel (p. 141).
- **Reprise à chaud** : la fonction repart dans l'état d'avant la coupure (p. 134).
- **Ajout en ligne** : un ajout est vu comme nouveau seulement avec une **nouvelle** table de paramètres (p. 134).
- **Conserver les réglages au démarrage à froid** : supprimer la RAZ des %MWi (p. 133).
- **Mode manuel** : pas d'alignement de la consigne interne sur la mesure (p. 141). Suivi sans à-coup de la commande au passage Auto → Manu (schéma p. 137).
- **Fonctions annoncées sans équation** : anti-saturation de l'intégrale, mode manuel/auto sans à-coup, mode intégrateur pour KP = TD = 0 (p. 136).
- **Échelle** : la p. 136 dit que le bon fonctionnement exige de rester dans l'échelle [0-1000] pour les mesures et les consignes, alors que le format est [0..10000] partout ailleurs (p. 133, 136, 139). C'est une **incohérence du document**.

## 5. Migration PL7 → Control Expert

- **Manuel (M20 p. 141)** : « Le paramètre PL7 parameter DEVAL_MMI n'est pas disponible dans Control Expert ». **C'est la seule mention du PL7 au sujet du PID_INT.**
- **FAQ FA137704** : le PID de PL7 Pro a la même interface (TAG, UNIT, PV, OUT, AUTO, PARA de 43 mots).
- **Ce n'est pas une règle de conversion.** La table de correspondance du convertisseur PL7 (aide en ligne, citée par FA17396) n'est pas lue. Pour une application migrée, il faut relever PARA avant et après la conversion, et retenir **« Conversion non qualifiée »**.

## 6. Comparaison ligne à ligne avec le dépôt (lecture seule)

| Élément du dépôt | Manuel | Verdict |
|---|---|---|
| `engine/block_adapters.py:96` : ΔOUT = KP[Δe + TS/(10 TI) e]/100, e = SP − PV | p. 137, 141 | **Conforme** (ΔDt = 0 quand TD = 0) |
| `block_adapters.py:99,111` : KP = Kp·pv_span/mv_span, signe conservé, KP > 0 si Kp > 0 | p. 140–141 | **Conforme** à l'équation. KP > 0 augmente la sortie quand PV baisse : c'est « arrière ». |
| `block_adapters.py:109-110` : refus si td > 0 | p. 141 (Dt sans équation) | **Justifié** |
| `block_adapters.py:114-116` : registres KP×100, TI×10, TS×100 ; bornes \|KP\| ≤ 10000, TI 1–20000, TS 1–32000 | p. 140 | Unités et plages **conformes**. **Écart mineur** : le manuel exclut ±10000 (« −10 000 < KP < +10 000 »), que l'adaptateur accepte. |
| `block_adapters.py:117,128` : avertissement de période multiple de la tâche ; NO_BUMP, OUT_MAX/OUT_MIN à déterminer | p. 140 | **Conforme.** Ajouter que la tâche doit être périodique (p. 133). |
| `block_adapters.py:133` : « page=140, units » ; `docs/catalogue/pidint-conversion.md:3` : ch. 30, p. 135–141 | M20 | **Exact** |
| `pidint-conversion.md:7-9` (format, ε, équations) et `:19-23` (incohérences « TI < 0 », « série / parallèle », [0-1000]) | p. 136, 137, 141 | **Confirmé** |
| `engine/catalogue/schneider.json:356` (bibliothèque « Standard ») | M20 p. 34, 127 | À compléter avec la famille **CLC_INT** et le type « Procédure » |

## 7. Ce qui manque pour qualifier

1. L'arithmétique entière exacte, les arrondis et la saturation : non décrits.
2. La forme de l'anti-saturation : non décrite.
3. La dérivée : non décrite.
4. La cible : CPU, micrologiciel, version ≥ 15.0.
5. La table de correspondance PL7 pour les installations migrées.
6. Les essais de banc E0 à E6.
