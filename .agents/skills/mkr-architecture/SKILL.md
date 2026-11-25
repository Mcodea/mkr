---
name: mkr-architecture
description: Concevoir ou modifier l'architecture C# WPF, moteur Python, IPC versionné ou stockage SQLite de MKR. À utiliser pour toute évolution structurelle, migration ou nouvelle dépendance.
---

# Architecture MKR

1. Lire `docs/architecture/socle.md`, `docs/adr/`, `docs/DEVELOPMENT_PLAN.md` et uniquement les modules impactés.
2. Identifier le scénario utilisateur, les contrats traversés et les exigences de traçabilité.
3. Garder WPF/ViewModels indépendants des algorithmes; `Core` expose les types métiers et ports; `Infrastructure` implémente persistence et IPC; Python demeure autonome.
4. Préférer une modification étroite, réversible et compatible. Si rupture de contrat, documenter le schéma et la migration avec tests ancienne/nouvelle version.
5. SQLite : migrations transactionnelles, versions explicites, refus des schémas futurs inconnus, sauvegarde/restauration vérifiables. Ne pas modifier les historiques sans stratégie de migration.
6. Contrats : noms, unités, nullabilité, bornes et codes d'erreur explicites, tests de requêtes invalides; jamais de chargement de code depuis les entrées.
7. Ajouter un ADR pour tout arbitrage structurant (options, conséquences, réversibilité).
8. Vérifier la compilation Windows via CI disponible et les tests Python/.NET pertinents. Documenter les essais non exécutés.

Livrable : code, tests, description des décisions et statut réel de validation.
