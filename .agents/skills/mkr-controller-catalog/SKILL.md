---
name: mkr-controller-catalog
description: Étudier et documenter les blocs PID réels Schneider Control Expert, Siemens et Rockwell, leurs paramètres, versions, équations et conversions MKR.
---

# Catalogue des régulateurs industriels — MKR

1. Lire `docs/catalogue/qualification.md` et les conventions scientifiques associées.
2. Définir la fiche par constructeur, logiciel, version, famille CPU, firmware, bibliothèque, nom exact du bloc, forme PID, paramètres, unités, pas, modes, anti-windup, filtres, limites et références.
3. N'ajouter aucune compatibilité issue d'une supposition. S'appuyer d'abord sur documents officiels fabricant, puis sur essais réels documentés.
4. Distinguer : candidat non étudié, documenté, vérifié en environnement, qualifié sur matériel; n'améliorer un statut qu'avec preuves.
5. Convertir les réglages uniquement après vérification de l'équation, de la structure (idéal/parallèle/série), du sens d'action, des unités et de l'échantillonnage.
6. Tester conversions aller-retour quand mathématiquement pertinent, limites, paramètres invalides et différences de comportement.
7. Le catalogue adapte les résultats scientifiques aux paramètres du constructeur; il ne décide pas de la logique fonctionnelle automate.
8. Ne pas intégrer de programmes propriétaires non autorisés ni de documentation sous licence en copie intégrale.

Livrable : fiches versionnées, références et matrice de qualification; pas de paramètres fictifs présentés comme vérifiés.
