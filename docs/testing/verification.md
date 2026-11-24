# MKR-TEST — Vérification du socle

| Vérification | Commande ou scénario | Preuve |
|---|---|---|
| Contrat Python et processus réel | `python3 -m unittest discover -s engine -v` | 4 tests exécutés localement sous Python 3.12 |
| Configuration des projets et vues | Lecture XML des fichiers de solution, des projets et des vues | Syntaxe XML uniquement |
| Compilation et audit NuGet | `dotnet restore MKR.slnx`, puis `dotnet build MKR.slnx -c Release --no-restore` | CI Windows attendue |
| Domaine, migrations, SQLite et .NET→Python | `dotnet run --project tests/Mkr.Smoke -c Release --no-build` | CI Windows attendue |
| Ergonomie et persistance WPF | Créer un projet, relancer et vérifier la liste. Tester le diagnostic avec et sans Python. Fermer pendant un diagnostic. Redimensionner à 100 %, 150 % et 200 % et parcourir au clavier. | Essai manuel Windows à effectuer |

Consigner le SHA, le système, la commande et le résultat pour chaque preuve. Les essais scientifiques suivent l'ajout des algorithmes. Les essais connectés suivent la phase de qualification du connecteur.

## Preuve CI Windows de la branche `claude/peaceful-cray-phk6q4` (24/11/2026)

| Élément | Valeur |
|---|---|
| Commit | `cc1c788bd88c98534cad56c38626eb321dbfa783` |
| Exécution | [Vérification MKR n° 45374160213](https://github.com/Mcodea/mkr/actions/runs/45374160213), `windows-2025`, **succès**, 19 étapes |
| Déclenchement | `push` sur la branche (le workflow ne dépendait plus d'une PR) |

**Ce que la CI a exécuté et vérifié** :
- analyse de chaque script PowerShell par l'analyseur PowerShell ;
- compilation de la solution WPF en Release, sur Windows ;
- tests Python du moteur et des imports ;
- test d'intégration .NET : SQLite et migrations, processus Python, historique, persistance ;
- 38 tests de communication, dont l'interopérabilité avec pymodbus et la bibliothèque Snap7 ;
- construction du paquet autonome, puis test graphique depuis ce paquet extrait :
  - transfert PI,
  - recherche d'essais dans un historique avec estimation et validation sur deux plages du même enregistrement,
  - acquisition S7 et Modbus,
  - enregistrement continu de 37 mesures en 5 segments,
  - catalogue.

**Défauts révélés par cette première exécution Windows complète, et corrigés** :
- Le workflow était un YAML invalide depuis le lot d'acquisition : aucune vérification Windows n'avait eu lieu pour les commits intermédiaires.
- Une apostrophe typographique fermait une chaîne PowerShell.
- Des noms d'automatisation statiques masquaient du texte dynamique, un défaut d'accessibilité.
- Une boîte de dialogue portait le même nom qu'une case à cocher.

**Ce que cette preuve n'est pas** : une revue humaine sur poste réel (affichage, DPI, ergonomie, compréhension des avertissements), un essai sur automate physique ou une qualification d'un bloc constructeur. Statut atteint : **TESTÉ AUTOMATIQUEMENT** (AGENTS.md §8).
