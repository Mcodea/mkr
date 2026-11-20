# Avancement MKR

- Dernier jalon : estimation FOPDT exploratoire à partir de deux échelons historiques distincts, courbe de validation et réouverture de l’étude.
- Parcours disponibles : projets locaux et description, CSV UTF-8, reprise de l’original et de son rapport, correspondance MV/PV pour FOPDT, stockage SQLite v3.
- Contrôles locaux : douze tests Python, dont un cas analytique et un cas où l’évaluation indépendante se dégrade sans modifier les paramètres. Le résultat de compilation et d’intégration Windows du jalon est consigné dans la [PR](https://github.com/Mcodea/mkr/pull/1).
- Restent à vérifier : comportement graphique sur Windows, sensibilité aux données bruitées, domaine d’emploi sur données représentatives. La méthode ne produit pas d’intervalle de confiance.
- Prochaine tâche : étendre le banc FOPDT aux cas bruités et aux dérives, mesurer l’incertitude des paramètres, puis ajouter un candidat SOPDT vérifié sur des jeux distincts.
- Blocage matériel : l’environnement local est Linux. L’examen graphique exige une session Windows. La CI utilise un agent Windows pour compiler et exécuter les tests automatisés.
