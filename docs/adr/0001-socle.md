# ADR 0001 — Socle Windows local

Statut : proposé pour revue.

- .NET 10 LTS pour WPF et les couches C#, Python 3.12 maintenu pour le moteur. Sa bibliothèque standard suffit au diagnostic initial. Le choix NumPy/SciPy et les tolérances attendront les algorithmes et tests de référence.
- SQLite via Microsoft.Data.Sqlite. Une base locale et des migrations transactionnelles évitent un service séparé. L'hypothèse initiale est un seul utilisateur par poste et des projets de faible volume.
- JSON via stdin/stdout dans un processus par opération. Comparé à HTTP local, ce choix évite port et serveur. Comparé à Python embarqué, il isole les runtimes et facilite les tests. Le démarrage par appel convient aux opérations courtes. Le débit des grands jeux de données et l'éventuel recours à un fichier local explicite restent à mesurer.
- Le chemin de l'interpréteur est un réglage local de confiance. Les requêtes peuvent uniquement invoquer des opérations déclarées. Aucun shell ni réseau n'est nécessaire.
- MVVM sans dépendance supplémentaire. La vue gère la présentation, le ViewModel orchestre les ports et l'infrastructure contient les dépendances techniques.
