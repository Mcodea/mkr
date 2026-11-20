# SIM-02 — Transfert manuel → automatique d’un PI (étude exploratoire)

## Objet et périmètre
Le moteur Python expose l’opération IPC v1 `simulate_pi_transition`. Il s’agit d’un **scénario autonome de vérification scientifique** ; l’interface WPF, la persistance et la comparaison graphique de ce scénario ne sont **pas encore intégrées**. Ce scénario n’agit sur aucun automate.

Le procédé est FOPDT en variables d’écart, initialement à l’équilibre : `dy/dt = (K u(t−θ) − y)/τ`. La réponse utilise le moteur partagé `plant_step`, conservant le traitement du retard fractionnaire. Le PI suit la forme parallèle échantillonnée `CV[k] = Kp (SP − PV[k]) + I[k]`.

## Contrat d’entrée
Propriétés obligatoires :
- `gain`, `tauSeconds`, `delaySeconds` ;
- `sampleSeconds`, `durationSeconds` ;
- `setpoint` ;
- `minimumMv`, `maximumMv` (doivent entourer strictement zéro) ;
- `manualMv` (cible constante avant transfert) ;
- `automaticAtSeconds` (strictement dans l’horizon, multiple du pas) ;
- `kp` (même signe que le gain du procédé), `tiSeconds` (> 0).

Option : `maximumMvRate` (> 0), en unité MV/s.

L’horizon comporte entre 2 et 5 000 intervalles. Le mode manuel doit occuper au moins un intervalle, et le mode automatique également. Les paramètres hors domaine sont rejetés avec les codes `sim_input`, `sim_domain` ou `sim_grid`.

## Séquence de commande
1. En manuel, la cible `manualMv` est limitée en amplitude puis en vitesse par rapport à la MV appliquée à l’intervalle précédent. Le PI n’est pas aux commandes : `cv = null`.
2. L’intégrale est suivie explicitement : `I[k] = MV_appliquée[k] − Kp·e[k]` pendant le mode manuel.
3. Au premier pas automatique, avec la **mesure courante**, l’intégrale est réajustée à `I = MV_précédente − Kp·e`. Donc la commande calculée à l’instant du transfert égale exactement la MV réellement appliquée juste avant : aucun saut demandé par le PI.
4. Ensuite, le PI reprend son intégration Euler explicite. Le gel conditionnel empêche l’intégrale de pousser la commande au-delà de la plage réalisable sur le pas, conformément au schéma simplifié du moteur existant.
5. La commande reste maintenue sur chaque intervalle, le procédé intègre puis avance au pas suivant.

Les séries conservent `seconds`, `mode`, `setpoint`, `pv`, `cv`, `requestedMv` et `mv`. Le dernier point ne contient pas de commande, puisqu’il ne débute aucun nouvel intervalle.

## Indicateurs et limites
Résultat : version d’algorithme, avertissements, instant de transfert, écart instantané de commande `transferBumpMv`, IAE, ISE, erreur finale, durées de saturation et de limitation de vitesse, nombre de pas où l’intégrale est gelée.

**Attention :** `transferBumpMv = 0` démontre la continuité de la commande **calculée au moment du basculement dans ce modèle**, pas une garantie de transition sans à-coup sur automate réel. Le régulateur constructeur peut différer (tracking, état intégral, action directe/inverse, sensibilité au temps d’échantillonnage, filtres, cascade, sélection de commandes, disponibilité de MV réelle, sécurités).

Ce lot ne traite pas :
- le retour automatique → manuel ;
- des changements multiples de mode ;
- les transitions en cascade, split-range ou override ;
- la consigne variable dans le temps ;
- les perturbations optionnelles ;
- la correspondance exacte avec un bloc constructeur ;
- l’intégration WPF et SQLite.

## Validation et reprise
Tests automatisés : transfert nominal, commande manuelle saturée, limitation de vitesse, procédé à gain négatif avec retard fractionnaire, erreurs de domaine. Exécution prévue par `python -m unittest discover -s engine -v` dans la CI Windows.

**Statut : implémenté et soumis aux contrôles CI ; non qualifié industriellement.**

Lot suivant proposé : intégration .NET/WPF/SQLite du scénario, puis essais graphiques Windows, avant toute extension des modes.
