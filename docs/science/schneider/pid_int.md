# PID_INT — fiche (Control Expert, bibliothèque standard)

**Statut : documentaire partiel. « Conversion non qualifiée ».** Je n'ai lu aucune page du manuel 33002520 : le PDF est refusé par le proxy. Ce nom remplace « PIFINT », erreur de nomenclature corrigée par le client le 27/11/2026.

## 1. Sources

| Source | Niveau | Contenu |
|---|---|---|
| 33002520K01000 (a) : page du document lue | D2 (métadonnées) | « EcoStruxure™ Control Expert - Standard, Bibliothèque de blocs », FR, version 20, 05 Sept 2020, 33002520_K01_000_20.pdf, 2,7 MB, checkSum ec6f7f27dbc57547eaca7d91142adda3 |
| 33002519K01000 (édition EN) | D2 (métadonnées, copie du 26/11) | Standard Block Library, v20, 06 Sept 2020 |
| Contenu de 33002520 transcrit dans le dépôt (chap. 30, p. 135–141) | D1m | `docs/catalogue/pidint-conversion.md`, `engine/catalogue/schneider.json:124-131, 353-438` |
| FA137704 « Quels sont les paramètres de la fonction PID sous PL7 Pro ? » | D2 (texte ; tableaux en image non lus) | « PID (TAG, UNIT, PV, OUT, AUTO, PARA) ». « Le paramètre PARA est une table de 43 mots ». « Les autres paramètres… ne doivent jamais être modifiés par l'application ». |
| FA226088 « …ARRAY[1..43] OF INT variable to the 'PID_INT' PARA pin? » | D1s (page en HTTP 404 sur deux sites régionaux) | D'après l'extrait : la broche PARA du PID_INT est de type ANY_ARRAY et prend ARRAY[1..43] OF INT. Une variable créée par défaut en ARRAY[1..2] OF INT empêche la liaison : il faut supprimer le bloc et le réinsérer. |
| FA16760, FA289143, FA138003, FA17369, FA17396 | D2 | Procédure générale de conversion PL7 → Unity Pro (détail au §5) |
| FA403904 | D2 | Intégrale d'un « PID » Unity/Control Expert : %MW en ms (1000 = 1 s), TIME en ms. **Le bloc n'est pas nommé** : ne pas l'appliquer au registre TI du PID_INT, que le dépôt donne en 0,1 s. |

## 2. Structure mathématique (D1m uniquement)

D'après le dépôt (p. 137 et 141, transcrites depuis des images) :
- ε = SP − PV, en comptes [0..10000].
- TI = 0 : OUT = KP·ε/100 + 5000.
- TI > 0 : ΔOUT = KP·[Δε + TS/(10·TI)·ε + ΔDt]/100, puis OUT = OUT + ΔOUT. C'est une forme incrémentale de type mixte.
- « Dt » est l'action dérivée, **sans équation** : elle est inexploitable.
- Le sens d'action est donné par le signe de KP. Les libellés « avant / arrière » ne sont pas définis.
- Le dépôt relève des incohérences internes du document : « TI < 0 », « série / parallèle » sans paramètre de choix, échelle [0-1000] contre [0..10000].

**Aucune de ces équations n'a été relue par moi.**

## 3. Paramètres et unités (D1m)

| Registre | Unité / plage (dépôt) | Remarque |
|---|---|---|
| KP (PARA[2]) | ×100, de −10000 à +10000 | Signé |
| TI (PARA[3]) | 0,1 s, de 0 à 20000 | 0 : pas d'intégrale, et décalage de 5000 |
| TD (PARA[4]) | 0,1 s, de 0 à 10000 | Équation absente |
| TS (PARA[5]) | 0,01 s, de 1 à 32000 | Ramené au multiple de la période de la tâche |
| PV_DEV (PARA[8].0) | 0 : dérivée sur la mesure, 1 : sur l'écart | — |

La taille de PARA (43 mots) est **suggérée** par FA226088 (D1s) et par l'analogie avec le PID de PL7 Pro (FA137704, D2). La correspondance mot à mot entre le PARA de PL7 et celui du PID_INT est **NV**.

## 4. Disponibilité par gamme et version

| Gamme / logiciel | État |
|---|---|
| M340 | **NV** |
| M580 | **NV**. Aucune source lue. Le scénario pression/pompe M580 ne doit pas supposer le PID_INT disponible. |
| Premium / Quantum | **NV** |
| Unity Pro / Control Expert | FA226088 (D1s) parle de Unity Pro. Aucune version minimale n'est lue. |

## 5. Migration PL7 → Control Expert

Lu (D2), procédure générale seulement :
- exporter l'application PL7 en .FEF, puis l'ouvrir dans Unity Pro, qui convertit et produit un rapport (FA16760) ;
- PL7 ≥ V4.3 est requis, et une application antérieure à V4.0 doit d'abord être mise à jour (FA289143) ;
- une application TSX37 s'importe vers M340, une application Premium PL7 vers Premium Unity. TSX37 vers Premium Unity n'est possible qu'après changement de processeur dans PL7 (FA138003) ;
- une application PL7-2 (TSX17) passe d'abord par PL7, puis par Unity Pro V4.0 ou plus (FA17369) ;
- la table de correspondance PL7 ↔ Unity se trouve dans l'aide en ligne, « Unity > Logiciel Unity Pro > Convertisseur PL7 > Correspondance entre PL7 et Unity Pro » (FA17396). **Elle n'est pas lue.**

**Non établi :**
- que le PID de PL7 soit converti en PID_INT ;
- que PARA soit recopié à l'identique ;
- que les unités (TI, TS) et le comportement (arrondis, mode sans à-coups) soient conservés.

Le PID de PL7 et le PID_INT ont l'air de partager un PARA de 43 mots, mais cela ne suffit pas. Sans la table de correspondance, la règle est **« Conversion non qualifiée »**, et une migration doit être vérifiée sur banc avec les valeurs relevées avant et après.

## 6. Écarts avec le dépôt

Le détail est dans [ecarts-depot.md](ecarts-depot.md) :
- **E1.** La fiche `PIFINT` doit fusionner avec `PID_INT`.
- **E8.** `blocs-observes.md:69` dit « 33002520… (non lu) », alors que `:84` dit « Oui ».
- **E9.** La disponibilité sur M340 et M580 est absente du catalogue : elle doit rester absente, et non être déduite.
- **E10.** Le lien avec le PID de PL7 (43 mots) n'est pas consigné.
- **E11.** Le registre TI est en 0,1 s d'après le dépôt, alors que FA403904 donne des ms pour un « PID » non nommé. Ne pas les confondre.

## 7. Ce qui manque pour un adaptateur qualifiable

1. Relire les p. 135–141 de 33002520 (FR) ou les pages correspondantes de 33002519 (EN) : tableau PARA complet, arithmétique entière et arrondis, bornes OUT_MAX/OUT_MIN, anti-emballement, sans à-coups, démarrage.
2. Disponibilité par CPU et version de Control Expert.
3. La table de correspondance du convertisseur PL7, pour les installations migrées.
4. Les essais de banc E0 à E6.

L'adaptateur actuel (`engine/block_adapters.py:93`) refuse à juste titre toute dérivée (`td > 0`).
