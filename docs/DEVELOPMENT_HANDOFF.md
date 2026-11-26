# ORIAS / MKR — Reprise de développement (phase scientifique)

*Dernière mise à jour : 2026-11-26, fin du LOT 0 (audit présenté, en attente d'arbitrage).*

## Point de départ
- **Révision UI/UX réceptionnée :** `c7915cd` (dernier code : `482373a`). Tag local `ui-ux-recue-2026-11-26` ; son push a échoué deux fois (déconnexion réseau) : à retenter.
- **Preuve de réception :** CI « Vérification MKR » verte, [run 329](https://github.com/Mcodea/mkr/actions/runs/45901338505) et [run 330](https://github.com/Mcodea/mkr/actions/runs/45904584081) ; interface validée par le client.
- **Aucune modification de `engine/` depuis avant la phase UI.**

## Branches
- `claude/peaceful-cray-phk6q4` : branche UI/UX (figée, ne plus y développer).
- `claude/science-engine-dev` : **branche active** de la phase scientifique, créée depuis `c7915cd`. Aucune pull request ouverte. **Aucune fusion dans `main`.**

## Lots
| Lot | État |
|---|---|
| LOT 0 — audit, référentiel, plan | **Terminé, en attente de validation** : `docs/science/lot0/` |
| 0b — corrections P0 de validité | proposé, à arbitrer |
| LOT 1 à 11 | non commencés |

## Derniers résultats de tests
- CI Windows, runs 329 et 330 : tout vert (Python 318 tests, smoke .NET, SQLite, Modbus/S7, paquet autonome, 12 contrôles de graphiques).
- Aucun test exécuté depuis la création de la branche scientifique (aucun code modifié).

## Références scientifiques
`docs/science/lot0/05-referentiel.md` — **aucune référence bibliographique vérifiée à la source** (réseau bloqué). SIPPY est LGPL-3.0 : non intégré.

## Blocages
- Vérification bibliographique : accès aux éditeurs/DOI nécessaire, ou contrôle humain.
- Licences des jeux de données publics non confirmées.
- La spécification MKR-SPEC-001 et le cahier directeur ne sont pas dans le dépôt.
- Pas de données de compatibilité M580 ; pas de banc ni de simulateur Control Expert.

## Décisions prises
- Référence UI/UX figée ; phase scientifique sur branche dédiée ; commencer exclusivement par le LOT 0.
- Les corrections de validité (P0) ne sont pas commencées : elles attendent l'arbitrage (`03-perimetre-et-plan.md` §4).

## Prochaines tâches
1. Obtenir l'arbitrage du client sur les 7 décisions de `docs/science/lot0/03-perimetre-et-plan.md` §4.
2. Retenter le push du tag de référence.
3. Selon la décision : lot 0b (corrections P0 avec oracles indépendants) puis LOT 1.

## Règles à respecter à chaque session
Lire `AGENTS.md`. Ne pas refaire l'audit : partir de `docs/science/lot0/`. Ne jamais fusionner dans `main`. Ne jamais affaiblir un test. Lecture seule sur les automates. Pas de déclaration de qualification ni de supériorité sans preuve.
