# MKR-ARCH — Architecture de référence

Desktop WPF (vues et ViewModels) dépend de Core (projets et ports). Infrastructure implémente les ports SQLite et Python et dépend de Core. App.xaml.cs assemble les composants. Le moteur Python fonctionne indépendamment et n'accède ni à WPF ni à SQLite. La base est créée dans le profil local Windows de l'utilisateur. La migration initiale est transactionnelle et utilise PRAGMA user_version. L'application refuse une version de base inconnue.

Flux : saisie → ViewModel → port projet → transaction SQLite → liste des projets. Le port moteur exécute un processus enfant à la demande. Les diagnostics d'erreurs sont journalisés localement via la trace .NET avec type d'erreur et opération, sans noms de projets ni messages exception potentiellement sensibles.

Ordre de construction : socle → import et normalisation → identification → réglage → simulation → catalogue et rapports → connecteur pilote → stabilisation. Les deux sources devront converger vers le même modèle métier. Les études référenceront les données et les versions d'algorithmes. Une conversion constructeur dépendra d'un réglage documenté et d'une entrée qualifiée.

| Risque | Condition de maîtrise |
|---|---|
| Étude fondée sur des données mal synchronisées ou faiblement excitantes | Rapport de qualité, revue du mapping et conservation de l'original |
| Confusion entre retard apparent et temps mort | Analyse physique et validation hors apprentissage, diagnostics des résidus |
| Réglage hors domaine, signe ou forme PID incompatibles | Hypothèses et unités explicites, références et tests numériques |
| Simulation trompeuse à cause du pas ou des limites d'actionneur | Études de convergence, tests des saturations/modes, validation humaine |
| Bloc, adresse ou firmware supposé compatible | Sources officielles et vérification versionnée |
| Écriture accidentelle ou surcharge PLC | Connecteur limité à la lecture, essais de trames et de charge avant site |
| Données industrielles divulguées | Aucune donnée réelle versionnée, accès au profil, permissions et politique de rétention à qualifier |
| Emballage et dépendances Windows | CI Windows, audit NuGet, test manuel, installateur après évaluation |
| Volume, migration et sauvegarde | Mesures sur corpus représentatifs et scénario de restauration avant usage industriel |
