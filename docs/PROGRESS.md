# Avancement MKR

- Dernier jalon : estimation FOPDT et SOPDT exploratoire à partir de deux échelons historiques distincts, courbe de validation et réouverture de l’étude.
- Parcours disponibles : projets locaux et description, CSV UTF-8, reprise de l’original et de son rapport, correspondance MV/PV pour l’identification, stockage SQLite v4.
- Contrôles locaux : seize tests Python, dont un cas analytique et un cas où l’évaluation indépendante se dégrade sans modifier les paramètres. La [CI Windows du premier incrément FOPDT](https://github.com/Mcodea/mkr/actions/runs/44745288939) a réussi la compilation WPF, l’audit NuGet et le parcours SQLite–Python. Le résultat de la migration v3 → v4 et des tests des deux familles est consigné dans la [PR](https://github.com/Mcodea/mkr/pull/1).
- Restent à vérifier : comportement graphique sur Windows, sensibilité aux données bruitées, domaine d’emploi sur données représentatives. Les erreurs-types locales conditionnelles n’ont pas de couverture statistique garantie.
- Prochaine tâche : ajouter le modèle intégrateur, avec refus explicite des réponses incompatibles et prédiction sur un essai distinct.
- Blocage matériel : l’environnement local est Linux. L’examen graphique exige une session Windows. La CI utilise un agent Windows pour compiler et exécuter les tests automatisés.
