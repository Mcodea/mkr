# PID-06B — Laboratoire PID manuel et exigences de fiabilité

## Niveau de preuve

| Niveau | Exigence | Statut au dépôt de la PR |
|---|---|---|
| S0 | Formules, conventions, unités, limitations et origine du modèle explicites | Documenté |
| S1 | Oracles numériques indépendants et essais adversariaux | Tests Python et .NET ajoutés ; CI à confirmer |
| S2 | Reproductibilité des scénarios et intégrité des valeurs à l'interface | Comparaison à scénario identique, contrôles des versions et des points |
| S3 | Validation visuelle sur vraie machine Windows, accès clavier, DPI, erreurs | Non réalisée |
| S4 | Qualification avec données industrielles et blocs PID d'une plateforme précise, sur banc autorisé | Non réalisée |
| S5 | Autorisation d'emploi sur installation et procédure de changement/réversibilité | Hors périmètre |

**Aucune conclusion « fiable industriellement » ne peut être déduite du niveau S1 seul.** Les étapes S3–S5 demeurent obligatoires avant toute recommandation de paramètres exploitables sur automate.

## Fonctions livrées

Depuis l'écran Simulation PI, l'utilisateur ouvre « Essai PID manuel ». Le logiciel reprend les caractéristiques du modèle FOPDT et les paramètres du scénario courant puis fournit quatre champs **Kp, Ti, Td, Tf** ; les valeurs initiales Kp/Ti correspondent aux paramètres PI IMC de référence, avec Td et Tf explicitement éditables.

Un clic déclenche deux calculs indépendants sur le même modèle, scénario, pas, limites, perturbation et durée :
1. Référence **PI IMC**, avec moteur existant ;
2. Prototype **PID idéal/dépendant** (P/I sur erreur, D filtrée sur PV) avec coefficients manuels.

Les versions des moteurs et les axes des temps sont confrontés avant tout affichage. Un tableau présente IAE, ISE, erreur finale, saturation, limitation de vitesse et nombres de pas à intégration gelée. Les graphiques sont distincts par grandeur physique : PV et SP dans les unités PV, MV appliquée dans les unités MV, contribution D dans les unités MV. Ils représentent les points calculés sans interpolation.

Le laboratoire est **hors ligne, éphémère et non persistant** : aucun nouveau réglage, aucune simulation PID et aucun coefficient de bloc constructeur n'est enregistré. L'état du projet reste inchangé. La fermeture libère les requêtes en cours.

## Contrôles d'entrée et de sortie

- Kp non nul, de même signe que K du modèle pour `e=SP-PV`.
- Ti > 0, Td ≥ 0, Tf ≥ 0 ; si Td > 0, alors Tf > 0.
- Rejet des NaN et infinis dans les champs numériques.
- Contrôle Python de la validité du modèle, de la grille de temps (2 à 5 000 intervalles), des bornes MV, du pas et de la perturbation.
- Contrôle indépendant côté .NET : cardinalité des points, grille de temps, fin d'horizon, valeurs numériques finies, bornes MV, limite de vitesse, dernier point sans CV/MV/D.
- Avertissement explicite quand le pas d'échantillonnage est supérieur à la constante de filtre Tf.
- Comparaison bloquée si les versions moteur ou les instants de simulation PI/PID divergent.

## Validation scientifique exigée

- **Oracle PI** : en prenant Td = 0, les séries PID doivent correspondre aux séries PI du moteur précédent à la précision numérique des flottants.
- **Filtrage dérivé** : vérifier par récurrence indépendante `D[k] = Tf/(Tf+h)*D[k-1] - Kp*Td/(Tf+h)*(PV[k]-PV[k-1])`.
- **Pas de derivative kick** : une variation initiale de SP ne doit pas déclencher un saut dérivé tant que la PV ne change pas.
- **Convergence** : pour un cas défini et les mêmes hypothèses, l'erreur de sortie doit se réduire lorsque le pas h diminue par rapport à un pas de référence plus fin.
- **Saturation, limitation de vitesse, anti-windup** : bornes vérifiées à chaque point, y compris pour des processus de gain négatif.
- **Retard fractionnaire, perturbations, conditions limites** : tests explicites.

### Références utilisées pour les conventions
- ISA, *ISA-TR5.9-2023: Realizing and Achieving Best PID*, https://www.isa.org/intech-home/2023/june-2023/features/isa-tr5-9-2023-realizing-and-achieving-best-pid
- MathWorks, *Proportional-Integral-Derivative (PID) Controllers*, https://www.mathworks.com/help/control/ug/proportional-integral-derivative-pid-controllers.html
- MathWorks, *Discrete-Time PID Controllers*, https://www.mathworks.com/help/control/ug/discrete-time-proportional-integral-derivative-pid-controller.html
- MathWorks, *Anti-Windup Control Using PID Controller Block*, https://www.mathworks.com/help/simulink/slref/anti-windup-control-using-a-pid-controller.html

Ces références documentent les principes, pas l'équivalence de MKR avec leurs implémentations. Le régulateur MKR ne peut **pas** être directement assimilé à Schneider PIDFF, Siemens PID_Compact, Rockwell PIDE, etc. Leur configuration réelle devra être vérifiée selon le modèle, la version, les unités, les modes et les conventions de commande avant toute qualification.

## Revue Windows à réaliser

- Vérifier à 100, 150 et 200 % DPI et sur petite fenêtre.
- Vérifier l'ordre clavier, les libellés, l'affichage d'erreur et la possibilité d'annuler/fermer sans corruption du projet.
- Comparer les valeurs du tableau et des tracés aux séries Python sur plusieurs cas représentatifs.
- Vérifier le comportement si le moteur Python est absent ou indisponible.
- Aucun défaut visuel ou test de compilation ne constitue une preuve de sûreté sur une installation industrielle.

## Étapes suivantes

PID-06C : campagne scientifique indépendante, cas de référence documentés, jeu de données représentatif, étude de bruit mesure/dérivée, stratégie de transfert manuel→auto et équivalence éventuelle à un bloc constructeur **seulement après accès à la documentation et à un banc autorisé**.
