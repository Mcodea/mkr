# Avancement MKR

- Dernier jalon : identification exploratoire FOPDT, SOPDT et intégrateur sur essais distincts, puis comparaison PI IMC/Lambda et SIMC attachée à une étude FOPDT.
- Parcours disponibles : projets locaux et description, CSV UTF-8, reprise de l’original et de son rapport, correspondance MV/PV pour l’identification, réglages PI théoriques et stockage SQLite v5.
- Contrôles locaux : vingt-deux tests Python, dont un cas analytique et un cas où l’évaluation indépendante se dégrade sans modifier les paramètres. La [CI Windows du premier incrément FOPDT](https://github.com/Mcodea/mkr/actions/runs/44745288939) a réussi la compilation WPF, l’audit NuGet et le parcours SQLite–Python. La [CI du jalon d’identification](https://github.com/Mcodea/mkr/actions/runs/44746322763) a réussi pour les trois familles. Le résultat du jalon PI est consigné dans la [PR](https://github.com/Mcodea/mkr/pull/1).
- Restent à vérifier : comportement graphique sur Windows, sensibilité aux données bruitées, domaine d’emploi sur données représentatives. Les erreurs-types locales conditionnelles n’ont pas de couverture statistique garantie.
- Prochaine tâche : simuler un FOPDT avec un PI choisi, un pas d’échantillonnage explicite et des limites d’actionneur. Vérifier d’abord la réponse analytique sans retard, puis la gestion des retards et de l’anti-windup.
- Blocage matériel : l’environnement local est Linux. L’examen graphique exige une session Windows. La CI utilise un agent Windows pour compiler et exécuter les tests automatisés.
