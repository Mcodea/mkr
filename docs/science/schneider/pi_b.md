# PI_B — fiche (Control Expert, bibliothèque de régulation)

**Statut : NON DOCUMENTÉ au niveau requis. Spécification insuffisante pour un adaptateur. « Conversion non qualifiée ».** MKR refuse toute conversion vers PI_B (`engine/test_pidff_conversion.py:120-123`). Ce refus est correct et doit rester tel quel.

## 1. Sources

| Source | Niveau | Contenu |
|---|---|---|
| 33002535K01000 (EN) et 33002536K01000 (FR) : pages des documents | D2 (métadonnées) | Manuels de la bibliothèque de régulation. **Contenu non lu** : PDF refusé par le proxy. |
| Dépôt, `engine/catalogue/schneider.json:297` | D1m, **mal cité** | « Le PI_B (p. 4805 du texte) numérote l'inverse de la table du PIDFF ». « p. 4805 du texte » est un numéro de ligne d'extraction, pas une page (déjà relevé dans `docs/science/lot0/annexes/D-constructeurs-rapports.md:69`). |
| Dépôt, `schneider.json:331-334` (catalogue Modicon Premium 2012, p. 6/14) | Non relu par moi (`iportal.se.com` refusé) | « Régulateur PI de base à structure mixte » |
| Catalogue Control Expert (`iportal.se.com`), extrait d'un moteur de recherche | D1s | PI_B = algorithme PI à structure mixte (série/parallèle). AUTOTUNE peut régler PIDFF ou PI_B. La bibliothèque CONT_CTL est compatible M340, M580, M580 Safety, Momentum, Premium et Quantum. |
| FAQ FA377379 « Why do I get a value of 128 in the status word of my PI_B function block? » | D1s : page en HTTP 503 aux 4 essais | D'après l'extrait : statut 128 lorsque `pv_inf` = `pv_sup` dans PARA ; concerne Control Expert et Unity Pro. |
| Forums `community.se.com` (type `Para_PI_B`, liaison AUTOTUNE.PARA_C → PI_B.PARA) | **Écartés** (non officiels) | — |

## 2. Ce qui est établi, et ce qui ne l'est pas

| Aspect | État |
|---|---|
| Existence et bibliothèque | Probable (D1s concordants avec le dépôt), non lue au manuel |
| Disponibilité M340/M580/Premium/Quantum | **NV** (D1s seulement) |
| Version de Control Expert / Unity | **NV** |
| Équation, forme mixte exacte, signe de l'écart | **NV**. Rien n'indique la forme de l'entrée (PV − SP ou SP − PV). |
| `rev_dir` | Le dépôt annonce une numérotation inverse de celle du PIDFF (D1m, sans page). **NV.** |
| Unités de kp, type de ti (TIME ou REAL), plages | **NV** |
| Anti-emballement, suivi, manuel/auto, bornes | **NV** |
| Période d'exécution, tâche | **NV** |
| Statut et erreurs | Statut 128 lorsque `pv_inf` = `pv_sup` (D1s) |
| Migration PL7 | **NV** |

## 3. Différences avec PIDFF et PID_INT

Ne pas réutiliser l'adaptateur `pidff` en lui donnant le nom PI_B :
- la numérotation de `rev_dir` serait inverse (D1m), avec un risque de **boucle divergente** ;
- la structure de PARA est différente : un PARA de type `Para_PI_B` est cité par des forums seulement, `Para_PIDFF` existe pour le PIDFF (FA273873) ;
- le bloc n'a ni dérivée ni anticipation (D1s), et ses bornes et son anti-emballement ne sont pas lus.

PI_B n'est pas non plus un PID_INT sans dérivée : le PID_INT travaille sur des entiers normalisés (D1m), alors que le type des grandeurs de PI_B n'est pas lu.

## 4. Ce qui manque pour écrire un adaptateur

1. **Le chapitre PI_B du manuel** de régulation, dans l'édition de l'installation. Il en faut :
   - l'équation, avec la forme de l'écart et la place de kp ;
   - la table de PARA : noms, types et unités de kp et ti, plages ;
   - la définition de `rev_dir` ;
   - l'algorithme par cycle (incrémental ou absolu), les bornes et l'anti-emballement ;
   - les modes (manuel, automatique, suivi) et l'initialisation.
2. Le texte de FA377379 (statut 128) et la table complète des bits de statut.
3. Les versions de Control Expert, de CPU et de bibliothèque de la cible.
4. Un oracle écrit depuis ces équations, puis les essais de banc E0 à E6 sur l'automate réel.

Tant que ces éléments manquent, la fiche `PI_B` du catalogue doit rester `to_verify`, sans équation, et la conversion doit rester refusée.
