# MKR

MKR est un logiciel Windows d’assistance à l’ingénierie de la régulation industrielle. Le socle permet de créer et rouvrir des projets locaux, de décrire un procédé et d’importer un CSV avec conservation de l’original et rapport de qualité descriptive. Il permet aussi d’enregistrer une étude FOPDT, SOPDT ou intégrateur exploratoire avec deux essais historiques distincts et de comparer les réglages PI IMC/Lambda et SIMC sur FOPDT, y compris en simulation fermée.

## Essayer la version portable sous Windows x64

Dans GitHub Actions, ouvrez une exécution réussie de **Vérification MKR** sur la branche souhaitée. Dans **Artifacts**, téléchargez **MKR-Windows-x64**. Extrayez le téléchargement GitHub pour obtenir `MKR-Windows-x64.zip` et son fichier `.sha256`. Extrayez ensuite l’archive MKR dans un dossier et lancez `Mkr.Desktop.exe`. Gardez ensemble tous les fichiers extraits. Le paquet embarque .NET, Python et les dépendances scientifiques.

Pour contrôler le téléchargement, comparez le SHA-256 de l’archive MKR à celui inscrit dans le fichier voisin. Le parcours de test Windows ouvre cette même distribution après extraction. Le fichier `build-info.json` identifie la révision et les versions utilisées pour la construire.

## Construire et exécuter depuis les sources

Prérequis : Windows x64, SDK .NET 10 et Python 3.12. Python doit être disponible sous la commande `python`, ou configuré avec le chemin complet de l’interpréteur via `MKR_PYTHON`. L'accès à NuGet est nécessaire à la première restauration.

```powershell
python -m pip install -r engine/requirements.txt
dotnet restore MKR.slnx
dotnet build MKR.slnx -c Release --no-restore
dotnet run --project src/Mkr.Desktop -c Release --no-build
```

Le dossier `%LOCALAPPDATA%\MKR` contient `projects.db` et `mkr.log`. La migration SQLite s’exécute au démarrage. La compilation dépendante du runtime requiert Windows Desktop .NET 10 et le dossier `engine` à côté des binaires. Aucun installateur dans cette livraison.

## Vérifier

```powershell
python -m unittest discover -s engine -v
dotnet run --project tests/Mkr.Smoke -c Release --no-build
```

Sous Linux, le moteur et ses tests peuvent être exécutés avec `python3`. Avec le SDK .NET 10, les tests d’intégration peuvent être exécutés directement avec `MKR_PYTHON=python3 dotnet run --project tests/Mkr.Smoke -c Release`.

- [Plan de développement](docs/DEVELOPMENT_PLAN.md)
- [Avancement](docs/PROGRESS.md)
- [Identification FOPDT](docs/science/fopdt-echelon.md) , [SOPDT](docs/science/sopdt-echelon.md) et [intégrateur](docs/science/integrateur-echelon.md)
- [Réglages PI](docs/science/pi-fopdt.md)
- [Simulation PI](docs/science/simulation-pi.md)
- [Périmètre et traçabilité](docs/requirements/traceabilite.md)
- [Architecture et risques](docs/architecture/socle.md)
- [Décisions du socle](docs/adr/0001-socle.md)
- [Contrat du moteur](docs/communication/ipc-v1.md)
- [Import et qualité des CSV](docs/science/qualite-csv.md)
- [Guide de l'utilisateur : de l'essai au réglage](docs/methode/guide-utilisateur.md)
- [Acquisition Schneider (Modbus TCP) et Siemens (S7comm)](docs/communication/acquisition.md)
- [Qualification d'un bloc sur banc](docs/validation/qualification-bloc-banc.md)
- [Plan de vérification](docs/testing/verification.md)
