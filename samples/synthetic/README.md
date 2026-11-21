# Séries synthétiques de référence

Exécuter `python samples/synthetic/generate.py` depuis la racine pour régénérer
les CSV et `truth.json`. Les valeurs sont issues d'une réponse analytique FOPDT
à un échelon de MV, avec conditions initiales nulles. Les fichiers couvrent
un cas nominal, du bruit gaussien reproductible, un gain négatif, un temps
mort long, une mesure plafonnée, une cadence irrégulière et une entrée sans
excitation. `truth.json` fixe les paramètres et unités pour chaque essai.
Les instants sont en UTC et les paramètres temporels en secondes.
