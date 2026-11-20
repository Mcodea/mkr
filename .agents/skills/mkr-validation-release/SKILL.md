---
name: mkr-validation-release
description: Tester, auditer et préparer les livraisons MKR : Python, WPF Windows, IPC, SQLite, sécurité, performance, installation et non-régression.
---

# Vérification, qualification et livraison — MKR

1. Lire `docs/testing/verification.md`, `docs/PROGRESS.md` et les critères d'acceptation du lot.
2. Distinguer quatre preuves : implémentation, compilation/exécution, validation scientifique et qualification industrielle. Ne jamais les confondre.
3. Tester Python : `python -m unittest discover -s engine -v`; exécuter sur versions supportées et consigner les échecs.
4. Sur Windows, vérifier `dotnet restore MKR.slnx`, `dotnet build MKR.slnx -c Release --no-restore` et `dotnet run --project tests/Mkr.Smoke -c Release --no-build`; sans environnement Windows, consulter CI et indiquer les limites.
5. Vérifier migrations SQLite, réouverture, conservation des sources, résultats reproductibles, timeouts et erreurs IPC.
6. Qualifier robustesse avec données bruitées, données volumineuses, comportements numériques extrêmes, interruptions, dépendances absentes et fermeture pendant traitement.
7. Avant livraison utilisateur : essais WPF manuels Windows (DPI, redimensionnement, clavier), procédure d'installation, sauvegarde/restauration, antivirus/signature si pertinents.
8. Toute connexion PLC requiert tests de protocole, mesure de charge, non-écriture et habilitation; aucun usage site sans qualification.
9. Ne pas masquer les tests non exécutés ni promettre un niveau de sûreté non démontré.

Livrable : rapport d'essais reproductible (SHA, OS, commandes, résultats), anomalies et statut de release.
