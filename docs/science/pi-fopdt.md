# Comparaison PI sur modèle FOPDT

Après avoir enregistré une étude FOPDT, la sélectionner dans la fenêtre d’identification. Le panneau **Réglages PI théoriques** compare IMC/Lambda et SIMC avec le même temps de réponse choisi, en secondes. Les comparaisons et leurs versions restent attachées à l’étude du modèle et peuvent être rouvertes.

## Méthode 1.0.0

Pour un procédé autorégulant stable `G(s) = K exp(−θs)/(τs+1)`, avec K non nul, τ strictement positif et θ positif ou nul, saisir une cible positive. Cette cible représente λ pour IMC/Lambda et τc pour SIMC. Ce sont des paramètres de calcul, sans mesure du temps de réponse réel.

| Méthode | Kp | Ti |
|---|---|---|
| IMC/Lambda selon MKR-SPEC-001 §8 | τ / [K(λ+θ)] | τ |
| SIMC PI selon MKR-SPEC-001 §8 | τ / [K(τc+θ)] | min(τ, 4(τc+θ)) |

La forme est idéale continue, `u = u₀ + Kp(e + ∫e dt / Ti)` avec `e = SP − PV`. Le signe de Kp suit celui de K. Kp s’exprime en unité MV par unité PV et Ti en secondes. Le coefficient parallèle `Ki = Kp/Ti` s’exprime en unité MV par unité PV et par seconde, pour les mêmes conventions. Cette conversion n’est pas une correspondance avec un bloc constructeur.

Les réglages héritent du statut exploratoire de l’étude. Lorsque la cible est plus courte que le retard estimé, le logiciel demande d’examiner la robustesse. Les limitations d’actionneur, l’anti-windup, la discrétisation et le comportement en présence de perturbations nécessitent une simulation ou des essais appropriés avant toute application.

Vérification : cas de gain négatif, activation et inactivation de la borne intégrale SIMC, rejet des entrées hors domaine. Dans le cas IMC sans retard, un test évalue la boucle fermée à plusieurs fréquences et retrouve la réponse analytique `1/(λs+1)` du modèle nominal continu. [Skogestad, 2003](https://doi.org/10.1016/S0959-1524(02)00062-8)

## Choix guidé de la cible en identification

Pour explorer un réglage rapide (« tight control »), la règle SIMC publiée est τc = θ, où θ représente le retard effectif du modèle utilisé pour le réglage. MKR utilise le retard du modèle identifié sélectionné. Pour la comparaison PI, le calcul IMC reçoit la même valeur de λ afin de comparer les méthodes à cible égale. La recommandation de départ est propre à SIMC. [Skogestad, 2003](https://doi.org/10.1016/S0959-1524(02)00062-8)

Une cible plus longue peut être justifiée par des contraintes de robustesse ou d’effort de commande. Si le retard est nul ou signalé comme mal résolu, MKR laisse la cible automatique vide. Une exigence explicite peut être saisie en mode personnalisé. [Skogestad, 2006, équations 5 et 6](https://skoge.folk.ntnu.no/publications/2006/skogestad_iecr_smooth-pid/ie0602815.pdf)

## Essais physiques NAWI examinés

Les archives 2 et 4 du pilote d’osmose inverse comprennent des acquisitions en boucle ouverte. Dans l’archive 2, le protocole prévoit des commandes sinusoïdales sur une ou deux pompes ou une commande constante. Dans l’archive 4, les neuf essais en boucle ouverte excitent simultanément les deux pompes. Le dossier « Set Point Step Change Experiment » contient bien des échelons, mais les régulateurs de débit restent actifs et les changements portent sur les consignes. Ces protocoles ne fournissent pas la paire d’échelons de commande en boucle ouverte requise pour valider ici le modèle pompe–procédé. Les mesures ne constituent donc pas une preuve de précision du réglage PI proposé par MKR. [Villez et al., DOI 10.7481/1963162](https://doi.org/10.7481/1963162)

La vérification « Inventaire des essais NAWI » contrôle les empreintes SHA-256 des archives et affiche leurs notices expérimentales. Ce contrôle documentaire ne calcule aucun paramètre. [Inventaire des fichiers et protocoles](https://github.com/Mcodea/mkr/actions/runs/45059205381)
