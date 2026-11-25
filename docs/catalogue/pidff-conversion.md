# PIDFF — conversion depuis le PID de MKR

**Statut : documentaire, NON QUALIFIÉ.** Source unique : Schneider Electric, *EcoStruxure Control Expert — Régulation — Bibliothèque de blocs*, document 33002536, édition 09/2020 (33002536.16), chapitre 17 « PIDFF », pages 167 à 187, fourni par le mainteneur le 24/11/2026. La version de Control Expert, le firmware et la CPU restent à identifier. Les équations sont des images dans le PDF : elles ont été transcrites en lisant les pages 173, 177, 182 et 183.

## Équation et correspondance

Fonction de transfert de la **structure mixte** (`mix_par = 0`, p. 173), avec IN = PV − SP en unités physiques :

> OUT = kp × [ 1 + 1/(ti·p) + td·p / (1 + (td/kd)·p) ] × IN

Le PID idéal de MKR est u = Kp (e + 1/Ti ∫e + Td de/dt) avec un filtre Tf sur la dérivée. La correspondance est exacte pour la fonction de transfert :

| MKR | PIDFF (structure mixte) |
|---|---|
| \|Kp\| en unités MV par unité PV | `kp` (valeur ≥ 0) |
| Ti en secondes | `ti` (type TIME) |
| Td en secondes | `td` (type TIME) |
| Tf, constante du filtre de dérivée | `kd = td / Tf`, avec kd ≥ 1 |
| dérivée sur la mesure | `pv_dev = 0` |
| signe de Kp | `rev_dir` (voir ci-dessous) |

**Sens d'action (p. 182).** `rev_dir = 1` : un écart positif PV − SP donne une sortie supérieure. `rev_dir = 0` : une sortie inférieure. MKR calcule u = Kp·(SP − PV) :
- Kp > 0 (gain du procédé positif) → **`rev_dir = 0`** ;
- Kp < 0 (gain du procédé négatif) → **`rev_dir = 1`**, avec `kp = |Kp|`. Le bloc refuse un kp négatif (statut 080, FAQ FA410692).

**Valeurs neutres à régler** pour que l'équation ci-dessus soit la seule en jeu :

| Paramètre | Valeur | Source |
|---|---|---|
| `ovs_att` | 0 : l'écart pondéré PV − (1 − ovs_att)·SP devient l'écart classique | p. 177 |
| `dband` | 0 : aucune zone morte | p. 178 |
| `ff_sup` = `ff_inf` | l'anticipation est ignorée | p. 179 |
| `outbias` | 0 | p. 182 |
| `en_rcpy` | 0 | p. 177 |

**À déterminer sur l'installation**, car le manuel ne donne pas de valeur neutre : `outrate`, `aw_type`, `bump`, les plages `pv_inf`, `pv_sup`, `out_inf`, `out_sup`, `out_min`, `out_max` et la période d'exécution `dt`. MKR n'en invente aucune.

## Ce que MKR refuse, faute de documentation cohérente

- **Structure parallèle (`mix_par = 1`).** La page 176 dit que `kp` ne s'applique pas aux actions intégrale et dérivée, mais l'équation détaillée de `TermI` (p. 183) contient `kp`. La documentation n'est pas cohérente : aucune conversion n'est offerte.
- **Filtre trop lent (kd < 1).** Le bloc remplace kd par 1 quand td ≠ 0 (FAQ FA410692) : il ne pourrait pas reproduire le Tf demandé.
- **Action dérivée sans filtre.** Le bloc filtre toujours sa dérivée (Tf = td/kd). Le PID SIMC de MKR est sans filtre : il faut choisir Tf avant de convertir.
- **Temps inférieur à 1 ms**, résolution du type TIME. Un arrondi qui change la valeur de plus de 0,1 % est signalé.

## Incohérences de la documentation, à lever sur banc

- La table des paramètres (p. 171) libelle `rev_dir = 0` « action inverse » et `rev_dir = 1` « action directe ». Le tableau de la page 182 libelle `rev_dir = 1` « inverse » et `rev_dir = 0` « directe ». **Les comportements décrits concordent**, et MKR s'appuie sur ces comportements, pas sur les étiquettes. L'essai E4 du protocole le confirme.
- Le bloc PI_B de la même bibliothèque numérote `rev_dir` à l'inverse de la table du PIDFF.

## Ce qui n'est pas reproduit

L'algorithme réel est incrémental (p. 183) : `TermI = sense × kp × (dt/ti) × dev` intègre l'écart **du cycle courant**, alors que la simulation de MKR intègre avec un cycle de retard. Les limiteurs, les deux types d'anti-enroulement, le suivi, le transfert manuel/automatique et la discrétisation exacte du bloc réel ne sont pas reproduits. **Ces comportements échantillonnés se qualifient sur banc.**

## Vérification effectuée

Un modèle de référence écrit depuis les équations par cycle du manuel (`engine/pidff_reference.py`, **oracle de test uniquement**) pilote le même procédé que la simulation de MKR :

| Cas | Écart maximal de PV pour dt = 0,1 / 0,05 / 0,025 s |
|---|---|
| PI, gain positif | 0,0064 / 0,0032 / 0,0016 |
| PID avec dérivée sur la mesure | 0,0051 / 0,0025 / 0,0013 |
| PI, gain négatif | 0,0064 / 0,0032 / 0,0016 |

L'écart est divisé par deux quand le pas est divisé par deux : les deux boucles convergent vers la même boucle continue. Un test de contrôle montre que `rev_dir` inversé donne une boucle divergente. Cette vérification établit l'équivalence **de la forme** ; elle ne qualifie pas le bloc.

L'interprétation du symbole `TermD`, que le manuel emploie pour la composante absolue et pour son incrément, est explicitée dans le modèle de référence. C'est la seule lecture cohérente avec la fonction de transfert de la page 173, et la convergence ci-dessus la confirme.
