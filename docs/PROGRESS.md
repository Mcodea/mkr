# Avancement MKR

- Dernier jalon : estimation FOPDT exploratoire à partir de deux échelons historiques distincts, courbe de validation et réouverture de l’étude.
- Parcours disponibles : projets locaux et description, CSV UTF-8, reprise de l’original et de son rapport, correspondance MV/PV pour FOPDT, stockage SQLite v3.
- Contrôles locaux : quatorze tests Python, dont un cas analytique et un cas où l’évaluation indépendante se dégrade sans modifier les paramètres. La [CI Windows du premier incrément FOPDT](https://github.com/Mcodea/mkr/actions/runs/44745288939) a réussi la compilation WPF, l’audit NuGet et le parcours SQLite–Python. La preuve du dernier commit est consignée dans la [PR](https://github.com/Mcodea/mkr/pull/1).
- Restent à vérifier : comportement graphique sur Windows, sensibilité aux données bruitées, domaine d’emploi sur données représentatives. Les erreurs-types locales conditionnelles n’ont pas de couverture statistique garantie.
- Prochaine tâche : ajouter un candidat SOPDT vérifié sur des jeux distincts et comparer les domaines des deux modèles.
- Blocage matériel : l’environnement local est Linux. L’examen graphique exige une session Windows. La CI utilise un agent Windows pour compiler et exécuter les tests automatisés.
