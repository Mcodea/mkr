# MKR-TEST — Vérification du socle

| Vérification | Commande ou scénario | Preuve |
|---|---|---|
| Contrat Python et processus réel | `python3 -m unittest discover -s engine -v` | 4 tests exécutés localement sous Python 3.12 |
| Configuration des projets et vues | Lecture XML des fichiers de solution, des projets et des vues | Syntaxe XML uniquement |
| Compilation et audit NuGet | `dotnet restore MKR.slnx`, puis `dotnet build MKR.slnx -c Release --no-restore` | CI Windows attendue |
| Domaine, migrations, SQLite et .NET→Python | `dotnet run --project tests/Mkr.Smoke -c Release --no-build` | CI Windows attendue |
| Ergonomie et persistance WPF | Créer un projet, relancer et vérifier la liste. Tester le diagnostic avec et sans Python. Fermer pendant un diagnostic. Redimensionner à 100 %, 150 % et 200 % et parcourir au clavier. | Essai manuel Windows à effectuer |

Consigner le SHA, le système, la commande et le résultat pour chaque preuve. Les essais scientifiques suivent l'ajout des algorithmes. Les essais connectés suivent la phase de qualification du connecteur.
