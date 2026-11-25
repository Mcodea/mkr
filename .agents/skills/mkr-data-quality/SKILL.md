---
name: mkr-data-quality
description: Implémenter ou vérifier les imports CSV/Excel, chronologies, mappings de variables, qualité de données et provenance des historiques MKR.
---

# Données de procédé — MKR

1. Inspecter `engine/csv_quality.py`, le modèle C# d'import et la persistance; préserver compatibilité avec les projets enregistrés.
2. Préserver les octets du fichier brut, son empreinte et les règles de transformation versionnées. Les données sources ne doivent jamais être écrasées par normalisation.
3. Déclarer explicitement temps/fuseau/unité, SP/PV/CV/MV, valeurs manquantes et qualité; ne pas inférer le rôle à partir d'un nom seul.
4. Signaler : dates manquantes ou dupliquées, désordre temporel, fréquence irrégulière, valeurs figées, aberrantes, saturation, trous, décalage de signaux et excitation insuffisante.
5. Toute interpolation, filtre ou rééchantillonnage doit être explicite, traçable et réversible par réimport.
6. Encadrer formats, tailles et encodages; fichiers invalides rejetés sans corruption SQLite ni perte de données.
7. Tester CSV réel/synthétique, décimales locales, décalages UTC, interruptions, entrées extrêmes et réouverture des données.

Livrable : mapping revérifiable, rapport qualité et tests de provenance.
