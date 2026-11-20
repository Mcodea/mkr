# Instructions de développement — MKR

MKR est un logiciel Windows d'assistance à l'ingénierie de la régulation industrielle. Référence primaire : cahier des charges MKR et `docs/DEVELOPMENT_PLAN.md`. L'état factuel du code et des tests prime sur les résumés de conversation.

## Avant de travailler
1. Lire seulement les documents nécessaires : `README.md`, `docs/PROGRESS.md`, puis les spécifications du lot.
2. Examiner le code déjà présent et le statut des tests; éviter la réécriture d'un module fonctionnel.
3. Sélectionner la skill correspondante dans `.agents/skills/`; lire son `SKILL.md` avant la modification.
4. Travailler sur une branche dédiée et préserver la branche principale et les PR existantes.

## Architecture
- WPF / MVVM pour l'interface, Python pour les calculs scientifiques, SQLite pour la persistance.
- Contrat .NET–Python versionné et erreurs explicites.
- Préserver la séparation science / constructeur / interface.
- Ne pas ajouter de dépendance ou d'abstraction sans nécessité démontrée.

## Règles non négociables
- N'inventer aucune formule, aucun résultat expérimental, aucune référence constructeur ni compatibilité.
- Préserver les unités, conventions de signe, paramètres, provenance et versions.
- Les résultats non qualifiés doivent rester étiquetés « exploratoires ».
- Aucune écriture ni modification d'un automate; connecteurs en lecture seule, sur banc autorisé.
- N'exécuter aucune connexion à une installation industrielle sans autorisation explicite.
- Aucune donnée industrielle confidentielle, aucun mot de passe ni secret dans le dépôt.
- Ne pas prétendre avoir compilé WPF sous Windows sans preuve d'une exécution Windows réelle.

## Définition de terminé
Pour chaque lot : exigence et critères d'acceptation, code intégré, tests pertinents exécutés, limites documentées et `docs/PROGRESS.md` mis à jour. Une création de fichiers seule ne prouve pas le fonctionnement.

## Reprise et fin de session
Poursuivre les tâches autorisées par incréments vérifiables, mais ne pas lancer de boucle de corrections illimitée. En cas de blocage, conserver un état reprenable et expliquer précisément l'échec. Ne pas fusionner une PR sans décision explicite de l'utilisateur.
