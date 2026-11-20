# Avancement MKR

- Dernier incrément : limite facultative de vitesse de MV dans les deux boucles FOPDT et comparaison des réponses PI à la consigne avec dépassement, temps de réponse à ±5 % et fenêtre affichée.
- Parcours disponibles : projets locaux et description, CSV UTF-8, reprise de l’original et de son rapport, correspondance MV/PV pour l’identification, réglages PI théoriques et simulation PI avec export des séries et stockage SQLite v6.
- Contrôles locaux : trente-trois tests Python, dont un cas analytique et un cas où l’évaluation indépendante se dégrade sans modifier les paramètres. La [CI Windows du premier incrément FOPDT](https://github.com/Mcodea/mkr/actions/runs/44745288939) a réussi la compilation WPF, l’audit NuGet et le parcours SQLite–Python. La [CI du jalon d’identification](https://github.com/Mcodea/mkr/actions/runs/44746322763) a réussi pour les trois familles. Le résultat du jalon réglage et simulation est consigné dans la [PR](https://github.com/Mcodea/mkr/pull/1).
- Restent à vérifier : comportement graphique sur Windows, sensibilité aux données bruitées, domaine d’emploi sur données représentatives. Les erreurs-types locales conditionnelles n’ont pas de couverture statistique garantie.
- Simulation en boucle ouverte disponible depuis une étude FOPDT. Le scénario applique un échelon de MV borné, affiche la réponse PV et exporte les séries en CSV. La simulation est calculée à la demande et l’export conserve les séries.
- Prochaines tâches du jalon simulation : modes de commande et transitions, scénarios comparatifs supplémentaires et revue graphique Windows.
- Blocage matériel : l’environnement local est Linux. L’examen graphique exige une session Windows. La CI utilise un agent Windows pour compiler et exécuter les tests automatisés.

## Lot SIM-02 — transition manuelle vers automatique (branche dédiée)

- Opération moteur expérimentale `simulate_pi_transition`, indépendante du parcours WPF actuel : suivi d’intégrale sur MV appliquée, bascule sans saut de CV calculée, limites d’amplitude et de vitesse, série de modes et diagnostics.
- Tests de non-régression ajoutés : transfert nominal, saturation, limitation de vitesse, procédé inverse à retard fractionnaire, entrées invalides. Résultat de la CI à vérifier avant validation.
- Spécification : `docs/science/pi-manual-auto.md`.
- À intégrer au lot suivant : modèles et contrat .NET, interface WPF, persistance SQLite, comparaison graphique, essais Windows. Aucun transfert PLC ; aucune qualification industrielle.
