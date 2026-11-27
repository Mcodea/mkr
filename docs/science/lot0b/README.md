# LOT 0b — Corrections P0 de validité scientifique

Chaque fiche documente : anomalie, preuve, impact, référence scientifique, correction, tests indépendants, résultats avant/après. **Aucun critère de validation n'a été affaibli** : les tests existants sont conservés tels quels ; les nouveaux tests ont des critères fixés par la statistique ou la théorie avant l'observation du résultat.

| Fiche | Anomalie | État |
|---|---|---|
| [P0-1](P0-1-erreur-type-gain-fopdt.md) | Erreur-type du gain FOPDT sous-estimée ≈ ×2 | corrigée |
| [P0-2](P0-2-arx-integrateur.md) | ARX publie un intégrateur comme premier ordre | corrigée |
| [P0-3](P0-3-robustesse-reglage-guide.md) | Robustesse optimiste (règle de la moitié, Ms échantillonné, période du régulateur) | 3a, 3b corrigées ; 3c moteur corrigé, interface à relier (LOT 1) |
| [P0-4](P0-4-refus-moteur-modele-non-valide.md) | Refus du modèle non validé uniquement côté C# | corrigée |
| [P0-6](P0-6-boucle-et-mode-manuel.md) | `loop="open"` forcé, MANUAL ignoré | **reportée au LOT 2** (justifiée) |
| [P0-7](P0-7-compatibilite-constructeur.md) | Compatibilité constructeur non contrôlée | corrigée côté moteur ; interface (saisie de la cible) au LOT 7 |
