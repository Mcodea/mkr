# Référence d'inspiration : outils de réglage PID du marché

Sources consultées (publiques, synthèses et pages éditeur ; éditions différentes, à relire sur la version courante) : synthèse fournie par l'utilisateur à partir du guide LOOP-PRO TUNER (2023/2025), guide V2.8 (Atlassian Control Station), page produit et articles de presse technique. Je n'ai pas pu ouvrir help.controlstation.com depuis l'environnement. **Aucun texte, aucune capture ni aucun nom de commande n'est repris : seuls les principes d'organisation sont retenus.**

## Principes observés → décision MKR

| Principe observé | Décision MKR | État |
|---|---|---|
| Parcours en 6 étapes (boucle, acquisition, identification, réglage, application, rapport), chaque étape produit ce qu'il faut à la suivante | Parcours MKR : Projet · Acquisition · Identification · Validation · Réglage · Simulation · Rapport | Texte d'accueil fait ; navigation par étapes à faire |
| Étapes dont les prérequis manquent sont désactivées | Entrées du menu grisées sans projet actif (fait) ; réglage grisé tant que la validation n'est pas faite (déjà imposé par le moteur) | Partiel |
| Ruban d'actions regroupées par étape | Barre d'actions par étape avec libellés courts | À faire |
| Deux pistes alignées sur le temps (mesure + consigne ; commande), zoom, lecture de valeurs, durée affichée réglable, annuler le zoom | Composant de graphique interne : pistes synchronisées, zoom/pan, curseurs, annuler le zoom, export PNG/CSV | À faire (phase 3) |
| Bornes de sélection déplaçables avec recalcul automatique du modèle | Sélection de fenêtre d'identification à la souris, recalcul à la demande | À faire |
| Plusieurs ajustements comparables ; alerte sur paramètres anormaux | Liste d'ajustements d'une étude avec qualité et alertes (existe : diagnostics) | Partiel |
| Excitation informative requise, essais encadrés par la sécurité du procédé | Contrôles de qualité d'excitation déjà en place ; rappel de sécurité à l'acquisition | Fait / à renforcer |
| Réglages P, PI, PID, variation d'agressivité, comparaison simulée avant/après (stabilité, établissement, dépassement, amortissement, mouvement de commande) | Curseur d'agressivité, tableau de critères Actuel / Proposé, simulation (SIMC/IMC existent) | Partiel |
| Panneau de valeurs actuelles, mesure/consigne/commande en barres | Inspecteur latéral repliable : valeurs, unités, mode manuel/auto lu | À faire |
| Tableau « Recommandé / Original » | Tableau « Actuel / Proposé » avec gain, Ti, Td | À faire |
| Application avec confirmation explicite et restauration | **Écart volontaire** : MKR est en lecture seule, aucun envoi à l'automate. Il prépare des valeurs « non qualifiées » à saisir par l'opérateur, avec rapport | Décidé |
| Rapport PDF de session, historique des modifications | Fiche d'étude PDF existante ; journal d'audit chaîné ; ajouter logo et historique des réglages | Partiel |
| Barre d'état avec connexion et version | Barre d'état faite (lecture seule, version) | Fait |
| Hors ligne possible : saisie de plages et de réglages existants | Import CSV et réglages saisis à la main | Partiel |

## Ce que MKR garde de différent
Indépendance vis-à-vis du constructeur (Schneider, Siemens), validation obligatoire avant réglage, incertitude du retard publiée, marges de stabilité, conversions de blocs documentaires non qualifiées, aucune écriture automate.

## Limite
Cette comparaison repose sur des synthèses, non sur l'usage du logiciel. Elle ne prouve ni équivalence ni supériorité.

## Logiciels du marché à étudier (liste fournie par l'utilisateur, 24/11/2026)
| Éditeur | Logiciel | Étudié ? |
|---|---|---|
| Control Station | LOOP-PRO Tuner | Partiellement (synthèse et captures fournies, guide V2.8 via recherche) |
| Valmet | ExperTune PID Loop Optimizer | Non |
| DotX Control Solutions | PID Tuner | Non |
| XLN Control | Advanced PID Loop Simulator | Non |

Ajoutés aux références à étudier (sources publiques seulement, aucun produit essayé) : MATLAB PID Tuner et System Identification Toolbox. Pour chacun, ne retenir que des principes de parcours et de méthode ; aucune interface ni code propriétaire. L'existence d'un marché ne prouve pas que MKR soit comparable : cela se démontre par des essais sur données réelles, pas encore faits.
