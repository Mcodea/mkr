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

Pour explorer un réglage rapide (« tight control »), la règle SIMC publiée est τc = θ, où θ représente le retard effectif du modèle utilisé pour le réglage.

MKR applique cette règle à la **borne prudente du retard** (audit NC-03). L'échelon de MV a eu lieu entre la dernière mesure inchangée et la première mesure modifiée, donc le retard réel est compris entre θ estimé et θ + Δ, où Δ est l'intervalle entre ces deux mesures. L'identification publie cet intervalle (`delayIntervalSeconds`) et avertit (`delay_onset_uncertainty`) quand Δ dépasse 10 % de θ.

Sous-estimer le retard rend le réglage plus agressif. Exemple contrôlé : θ réel 3 s, Δ = 1 s, θ estimé 2,1 s. Avec τc = θ estimé, Kp est 43 % au-dessus de la règle appliquée au retard réel. Avec τc = θ + Δ, l'écart tombe à 15 %. Le calcul PI lui-même utilise le θ du modèle. Pour la comparaison PI, le calcul IMC reçoit la même valeur de λ afin de comparer les méthodes à cible égale. La recommandation de départ est propre à SIMC. [Skogestad, 2003](https://doi.org/10.1016/S0959-1524(02)00062-8)

Une cible plus longue peut être justifiée par des contraintes de robustesse ou d’effort de commande. Si le retard est nul ou signalé comme mal résolu, MKR laisse la cible automatique vide. Une exigence explicite peut être saisie en mode personnalisé. [Skogestad, 2006, équations 5 et 6](https://skoge.folk.ntnu.no/publications/2006/skogestad_iecr_smooth-pid/ie0602815.pdf)

## Essais physiques NAWI examinés

Les archives 2 et 4 du pilote d’osmose inverse comprennent des acquisitions en boucle ouverte. Dans l’archive 2, le protocole prévoit des commandes sinusoïdales sur une ou deux pompes ou une commande constante. Dans l’archive 4, les neuf essais en boucle ouverte excitent simultanément les deux pompes. Le dossier « Set Point Step Change Experiment » contient bien des échelons, mais les régulateurs de débit restent actifs et les changements portent sur les consignes. Ces protocoles ne fournissent pas la paire d’échelons de commande en boucle ouverte requise pour valider ici le modèle pompe–procédé. Les mesures ne constituent donc pas une preuve de précision du réglage PI proposé par MKR. [Villez et al., DOI 10.7481/1963162](https://doi.org/10.7481/1963162)

La vérification « Inventaire des essais NAWI » contrôle les empreintes SHA-256 des archives et affiche leurs notices expérimentales. Ce contrôle documentaire ne calcule aucun paramètre. [Inventaire des fichiers et protocoles](https://github.com/Mcodea/mkr/actions/runs/45059205381)

## Marges de stabilité du modèle nominal (audit NC-17)

Chaque réglage (IMC/Lambda, SIMC, et le PID SIMC du second ordre) est livré avec les marges de la boucle ouverte nominale L(s) = C(s) G(s), erreur vers PV, où G est le modèle identifié (retard exact, sans approximation de Padé) et C la forme idéale. Le calcul est dans `engine/stability_margins.py`.

| Grandeur | Définition |
|---|---|
| Marge de phase | 180° + phase de L à la pulsation où \|L\| = 1 (la plus faible si elle est multiple) |
| Marge de gain | 1 / \|L\| à la pulsation où la phase vaut −180° (la plus faible si elle est multiple) |
| Marge de retard | marge de phase en radians divisée par la pulsation de passage à 0 dB |
| Pic de sensibilité Ms | maximum de \|1 / (1 + L)\| sur toutes les fréquences |

**Avertissements.** Ils suivent l'usage courant (Åström et Murray, *Feedback Systems*, ch. 12 ; Skogestad et Postlethwaite, *Multivariable Feedback Control*, ch. 2) et ne sont jamais des critères d'acceptation :
- marge de phase inférieure à 30° ;
- marge de gain inférieure à 2 ;
- Ms supérieur à 2 ;
- boucle nominale instable ;
- passages à 0 dB multiples ;
- absence de passage à 0 dB.

**Limites.**
- Les marges décrivent le **modèle nominal seul**. Elles ne bornent pas l'écart entre le modèle et le procédé réel : lisez-les avec l'analyse de sensibilité, qui fait varier gain, constante de temps et retard.
- Ni saturation, ni limitation de vitesse, ni filtre de dérivée du bloc réel ne sont modélisés.
- Les critères « phase > 0 et gain > 1 » supposent une boucle de type usuel ; un cas à plusieurs passages est signalé.

**Vérification.**
- *Formes fermées :* P sur premier ordre avec retard (ωc = √(k²−1)/τ, marge de phase π − ωcθ − arctan(ωcτ), marge de gain par bissection indépendante) ; PI avec Ti = τ ramené à L = k e^(−sθ)/s (ωc = k, marge de phase 90° − kθ, marge de gain π/(2kθ)) ; Lambda sans retard (90°, Ms = 1) ; PID série annulant les deux pôles d'un SOPDT.
- *Recoupement entre deux chemins de calcul :* les marges du PID SIMC sur SOPDT sont égales à celles du PI SIMC sur le premier ordre dont le pôle rapide est annulé.
- *Oracle dans le domaine temporel, indépendant :* en multipliant le gain du procédé par 0,9 fois la marge de gain, la boucle échantillonnée converge. Par 1,1 fois la marge, elle diverge.
