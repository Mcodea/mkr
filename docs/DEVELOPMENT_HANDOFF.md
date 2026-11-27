# ORIAS / MKR — Reprise de développement (phase scientifique)

*Dernière mise à jour : 2026-11-27, LOT 0b en cours.*

## Point de départ
- **Révision UI/UX réceptionnée :** `c7915cd` (dernier code : `482373a`). Tag local `ui-ux-recue-2026-11-26` (push du tag échoué à plusieurs reprises : à retenter). CI « Vérification MKR » verte : [run 329](https://github.com/Mcodea/mkr/actions/runs/45901338505), [run 330](https://github.com/Mcodea/mkr/actions/runs/45904584081).
- **Arbitrage du LOT 0 reçu** (2026-11-26) : LOT 0b autorisé ; accès réseau scientifique autorisé (environnement « ORIAS-MKR recherche scientifique », 20 hôtes) ; SIPPY exclu provisoirement ; licences des données à vérifier avant tout embarquement ; Broïda reclassé en méthode d'estimation du FOPDT ; scénario pression/pompe/M580 à construire (données synthétiques) ; rebaselining de la feuille de route autorisé. Cahier directeur déposé : `docs/requirements/cahier-directeur-orias.md`.

## Branches
- `claude/peaceful-cray-phk6q4` : branche UI/UX figée.
- `claude/science-engine-dev` : **branche active**. Aucune pull request. **Aucune fusion dans `main`.**

## LOT 0b — état (fiches dans `docs/science/lot0b/`)
| Correction | État |
|---|---|
| P0-1 erreur-type du gain FOPDT | **faite** (commit 58cf304), test `test_fopdt_error_coverage.py` |
| P0-2 ARX / intégrateur | **faite**, test `test_arx_integrator_guard.py` |
| P0-3a robustesse sur le SOPDT identifié | **faite**, test `test_recommend_half_rule_robustness.py` |
| P0-3b Ms de la boucle échantillonnée | **faite** (`sampled_loop.py`, `recommend.py` 1.2.0), tests `test_sampled_loop.py`, `test_recommend_sampled_loop.py` |
| P0-3c période du régulateur | moteur fait (`controllerPeriodSeconds` facultatif) ; **à relier côté C#/interface/SQLite (LOT 1)** |
| P0-4 refus moteur du modèle non validé | à faire |
| P0-6 `loop="open"` forcé / MANUAL ignoré | à faire |
| P0-7 compatibilité catalogue (champ vide = compatible ; cible vide en C#) | à faire |
| Reclassement de Broïda (méthode, pas famille) + migration | à faire |

## Constats de vérification bibliographique (à traiter)
- **E-1 (élevée)** : `pi_tuning.imcPublished` n'est pas un PI publié par Rivera, Morari & Skogestad 1986 (c'est le PID de Padé sans sa dérivée) ; l'attribution du code et de `docs/**/pi-fopdt.md` est à corriger (renommage = migration de stockage possible) — à traiter au LOT 5.
- E-2 bornes publiées d'IMC non appliquées ; E-3 « Lambda (Dahlin) » non vérifié ; E-4 plage Ms attribuée à Åström & Hägglund alors qu'elle est lue chez Skogestad 2003. Détails : `docs/science/references/referentiel-verifie.md`.
- Non lus (ouvrages non accessibles légalement) : Ljung, Söderström & Stoica, Åström & Hägglund, Åström & Wittenmark, Franklin-Powell ; Broïda 1969 (contenu : accès fermé).

## Travaux de recherche interrompus (limite d'usage atteinte, reprise après 03h50 UTC)
- `docs/science/licences/` : inventaire des dépendances et licences des jeux de données (brouillons, non relus ; sections bibliothèques candidates et README manquantes).
- `docs/science/scenario-pression-pompe-m580/` et `tests/reference/scenario_pression_pompe/` : scénario pression/pompe/M580 (brouillon, non relu) ; manuels Schneider (PIDFF, « PIFINT » à vérifier) non encore lus.

## Derniers résultats de tests
- Suite Python locale (`python3 -I -m pytest -q engine`) : 320 tests verts avant les modifications P0-3b ; relancée avec P0-3b (voir commit).
- Aucune CI Windows lancée depuis `c7915cd` (modifications moteur Python seulement jusqu'ici).

## Règles à respecter à chaque session
Lire `AGENTS.md` et `docs/science/lot0/`. Ne pas refaire l'audit. Ne jamais fusionner dans `main`. Ne jamais affaiblir un test. Lecture seule sur les automates. Aucune déclaration de qualification ou de supériorité sans preuve. Opus pour les travaux scientifiques complexes, Sonnet pour le courant.
