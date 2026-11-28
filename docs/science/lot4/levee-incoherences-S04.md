# Levée des incohérences du jeu S04 (Coupled-Tank, CEFET-MG, v1.0.0) — 2026-11-28

Fichiers contrôlés (MD5 officiel = calculé). **Aucune donnée modifiée.** Mesures faites sur `complete_data.csv`, `h1.csv`, `h2.csv`, `U.csv`.

| Point relevé | Constat mesuré | Décision |
|---|---|---|
| Période d'échantillonnage : 4 s annoncée, ~3,85 s vue | 4 501 pas : **médiane 3,99999 s, tous dans [3,9986 ; 4,0017] s sauf le premier (3,852 s)**. Un seul intervalle anormal, au début ; dans `h1.csv` les pas valent 3,9986–4,0001 s | **Ts = 4 s levée.** Le premier intervalle de `complete_data.csv` est signalé comme anomalie d'acquisition et conservé tel quel |
| `h1.csv`, `h2.csv`, `U.csv` = données indépendantes ? | Les 10 « expériences » de 81 échantillons **sont des fenêtres de `complete_data.csv`** (début aux lignes 476, 901, 1326, 1751, 2176…, pas de 425) : `h1/h2/U.csv` en sont des extraits, avec les mêmes valeurs | **Ne jamais compter les deux** : on utilise `complete_data.csv` comme source unique, découpé par fenêtres ; la répartition A/B/C par répétition porte sur ces fenêtres |
| README : séquence de « 10 niveaux » | **16 niveaux distincts** dans `U.csv` (5,4 à 93,4 %) et 42 dans `complete_data.csv` ; la commande est en % absolu de pompe, pas en écart de ±50 % | écart README documenté ; on utilise les valeurs mesurées |
| Unités : README « cm », `operational_parameters.json` « mm » et clés `h3_op`/`h4_op` | `h1` varie de 33,8 à 60,1 et le point d'opération donné est 47,4008 : **même échelle que les données**. Les clés `h3`/`h4` ne correspondent à aucun fichier du jeu. **L'unité réelle (cm ou mm) n'est pas démontrable par les données** | **Non levée.** Hypothèse déclarée : « unité du README (cm) ». Impact : constantes de temps et temps mort sont indépendants de l'unité ; un **gain absolu** est exprimé en « unité du jeu par % de pompe » ; les décisions de validation relatives (erreur normalisée) n'en dépendent pas. Aucun réglage ne sera présenté comme physique tant que l'unité n'est pas confirmée par les auteurs |
| Répétitions : indépendance | Corrélation des répétitions 1–2 : 0,999 ; écart maximal 0,91 (unité du jeu). **La réponse déterministe domine ; seules les réalisations de bruit diffèrent** | confirmé : l'indépendance porte sur le bruit, **pas sur l'excitation** (déjà déclaré au pré-enregistrement) |

**Bilan** : S04 est exploitable en identification en boucle ouverte sous réserve de l'unité du gain. Une question à poser aux auteurs : unité de `h1`/`h2` et signification de `h3_op`/`h4_op`.
