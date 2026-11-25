# ADR 0007 — Étude ARX / sortie-erreur enregistrée

Statut : implémenté, à valider (revue humaine puis recette Windows). **Décision du mainteneur du 25/11/2026 : « Ok je valide »**, en réponse à la demande d'accord explicite pour une évolution du schéma SQLite (interprétation consignée : la décision en attente était l'enregistrement de l'étude ARX).

## Contexte
L'identification ARX / sortie-erreur (`engine/arx.py`, ADR implicite dans `docs/science/arx.md`) est exposée par un bouton de l'écran d'identification, mais son résultat n'était qu'affiché : non enregistré, non tracé, non relisible. Une étude d'ingénierie doit conserver ses entrées (imports, colonnes, plages, unités, tolérances), la version du moteur et la décision, y compris un **refus**, comme pour les études FOPDT.

## Décision
1. **Migration SQLite 11, additive.** Nouvelle table `arx_studies` (id, project_id, training_id, validation_id, created_at, metadata JSON), mêmes références et même contrainte d'indépendance que `identification_studies` (migration 9) : sources distinctes, ou même source avec deux plages renseignées et différentes. Aucune table existante n'est modifiée ; aucune donnée n'est copiée.
2. **Un enregistrement par exécution**, décision acceptée **ou refusée**, avec le résultat complet du moteur (candidats, raisons, actions). Une étude refusée ne peut alimenter aucun réglage.
3. **Piste d'audit** : chaque enregistrement ajoute une entrée à la chaîne d'audit (`arx.saved`).
4. **Pas d'alimentation automatique du réglage guidé** dans ce lot : l'équivalent du premier ordre reste une information affichée, à reprendre dans un lot ultérieur avec sa propre décision (il faudrait créer un modèle `IdentificationStudy` avec porte de validation, ce que l'ARX ne fournit pas aujourd'hui).

## Alternatives écartées
- **Réutiliser `identification_studies`** : le contrat `IdentificationResult` (FOPDT/SOPDT/intégrateur) ne représente pas un ARX ; un détournement masquerait la nature du modèle et casserait la lecture par un MKR plus ancien.
- **Ne rien enregistrer** : perte de traçabilité, contraire à la règle de préservation des paramètres et résultats de validation (AGENTS.md §4).

## Conséquences et risques
- Un MKR antérieur refuse `user_version = 11` au lieu de le lire de travers (même convention que les migrations 9 et 10).
- Les études ARX sont des données d'étude, pas un réglage : aucune qualification n'est portée par l'enregistrement.

## Retour arrière
Restaurer la sauvegarde de la base projet avant la migration. La table est isolée : ses lignes ne sont référencées par aucune autre table. Aucune donnée d'origine (CSV, empreintes) n'est touchée.
