# Dispositif de qualification indépendante (protocole directeur §6, §7, §21) — préparation

**Statut : DISPOSITIF PRÉPARÉ ; AUCUNE validation à l'aveugle n'a été réalisée ni n'est revendiquée.** L'exécution finale sera organisée séparément après le gel du moteur scientifique (arbitrage du client du 2026-11-28 : ce point ne bloque pas le développement).

## 1. Ce qui est fourni
`scripts/blind/blind_tool.py` (testé : `engine/test_blind_tool.py`, 4 tests) :
| Commande | Rôle |
|---|---|
| `freeze REPO MANIFEST` | fige le moteur : commit Git, SHA-256 de chaque fichier de `engine/`, `mkr_data_import/`, `contracts/` (hors tests), versions Python/numpy/scipy |
| `verify REPO MANIFEST` | échoue (code 2) si le moteur diffère du manifeste : **un seuil changé après le gel est détecté** |
| `seal RESULTS SEAL` | scelle les résultats d'ORIAS (empreintes, horodatage UTC) **avant** toute révélation des références |
| `check` / `reveal-allowed` | refusent la comparaison aux références si les résultats ont changé depuis le scellement ou si aucun scellement n'existe |

## 2. Ce que l'outil ne peut PAS garantir (à organiser)
Le scellement détecte une modification, il **ne prouve pas l'indépendance** (un autre agent IA ne la prouve pas non plus : protocole §7). Il faut :
1. **Un évaluateur humain ou une équipe distincte du développement**, sans accès au code de développement ni aux références avant le scellement ;
2. **Des données et références conservées hors du dépôt de développement et de cet environnement** (jamais accessibles à l'agent de développement) ;
3. **Des jeux non publiés** (ou dont les références ne sont pas publiques) : les jeux publics S01 à S06 **ne peuvent pas** servir d'ensemble D ;
4. Un **runner** exécutant ORIAS sur les seuls signaux (entrées/sorties, unités, période) et produisant les décisions, modèles et réglages dans `RESULTS_DIR`.

## 3. Procédure (à exécuter par l'évaluateur)
1. Récupérer le commit figé ; `blind_tool.py verify REPO manifest.json` doit renvoyer « aucun écart ».
2. Exécuter ORIAS sur les mesures du jeu D (sans références) ; écrire les résultats ; `seal`.
3. Publier l'empreinte du scellement (hors du dépôt de développement).
4. Seulement alors, `reveal-allowed` autorise de charger les références et de comparer.
5. Si une correction du moteur devient nécessaire, **les données D concernées deviennent des données de développement** ; une nouvelle validation finale exige d'autres données.

## 4. Gel à faire (étape 7)
Le gel n'est pas fait : il intervient après la consolidation du moteur. `freeze` sera exécuté sur le commit retenu et le manifeste publié avec la version.
