# PID-06C1 — Qualification numérique du bruit de mesure

## Objet
Poursuivre la qualification scientifique du prototype PID, **sans prétendre à une qualification industrielle**. Ce sous-lot intègre un mode de simulation du bruit de capteur déterministe, des tests de la dérivée filtrée et un contrat de vérification indépendant Python–.NET. L'essai est pour l'instant accessible au moteur/port scientifique, **pas directement dans l'écran Windows**.

## Justification et sources consultées
- ISA-TR5.9-2023, cadre des algorithmes PID, filtrage, mesure, performances et difficultés d'application : https://webstore.ansi.org/standards/isa/ISATR52023
- MathWorks, PID continu : https://www.mathworks.com/help/control/ug/proportional-integral-derivative-pid-controllers.html
- MathWorks, discrétisation et filtre D : https://www.mathworks.com/help/control/ug/discrete-time-proportional-integral-derivative-pid-controller.html
- ISA, « Is Data Noise in Process Control Degrading Performance? » : https://www-prod.isa.org/web/acom/article/is-data-noise-in-process-control-degrading-performance

Ces références fondent **les risques à tester et les conventions**, sans constituer une preuve d'équivalence logicielle avec leurs produits. Aucun code propriétaire n'est recopié.

## Sémantique mathématique
Le procédé produit la **PV physique** `pv[k]`. Un vecteur de bruit **fourni explicitement** par le banc de test, en unité PV, définit la valeur reçue par le régulateur :

\[
PV_{mes,k}=PV_{phys,k}+n_k.
\]

P et I utilisent `SP - PV_mes`. La dérivée filtrée agit sur la seule mesure :

\[
D_k=\frac{T_f}{T_f+h}D_{k-1}
-\frac{K_pT_d}{T_f+h}(PV_{mes,k}-PV_{mes,k-1}).
\]

Le procédé physique est toujours alimenté par la **MV appliquée** (plus la perturbation physique définie séparément) et **jamais directement par le bruit capteur**. Les métriques de poursuite IAE/ISE utilisent la **PV physique**, non l'erreur mesurée bruitée : cela évite de prendre un capteur dégradé pour une augmentation réelle d'erreur procédé. Cette convention est assumée et traçable ; elle n'est pas une métrique disponible en ligne lorsque la PV physique est inconnue.

## Reproductibilité et sécurité du contrat
- Le champ optionnel `measurementNoise` doit être un tableau JSON de **n échantillons exactement**, où n = durée / période. Les valeurs sont finies, numériques et dans le domaine numérique d'entrée existant.
- Si le champ est absent, on conserve l'intégralité du comportement nominal. Un tableau de zéros rend **exactement** les mêmes PV, CV, MV, D et métriques.
- Aucun tirage aléatoire implicite : chaque essai peut être reproduit à partir du vecteur exact. Le vecteur n'est pas persisté automatiquement dans une étude ni généré depuis un historique industriel.
- Les points exposent `measuredPv` pour distinguer la mesure bruitée et la variable physique du procédé, laquelle demeure `pv`.
- Nouvelle version du résultat `0.2.0` : contrat .NET mis à jour, compatibilité des appels sans bruit conservée, absence d'export de coefficients automate.
- Contrôles .NET en plus de Python : validation des échantillons de bruit, domaine de la grille, valeurs finies, borne et vitesse MV, présence de `measuredPv` pour chaque pas.

## Tests et preuve visée
1. **Référence non bruitée :** le prototype PID sans bruit et le même prototype avec des zéros reproduisent exactement les trajectoires et métriques attendues.
2. **Oracle récursif indépendant :** contrôler D à chaque pas à partir des `measuredPv` retournées, de Kp, Td, Tf et h.
3. **Bruit impulsionnel :** montrer que le filtre de durée supérieure réduit l'amplitude initiale de l'action dérivée pour le même échelon de bruit, **sans** conclure une meilleure performance sur toutes fréquences et tous procédés.
4. **Séparation physique/capteur :** vérifier `pv[0]=0`, `measuredPv[0]=n[0]` et l'IAE physique non nulle après réaction du régulateur à une erreur capteur.
5. **Erreurs de contrat :** refuser NaN, Inf, valeurs non numériques, cardinalité incorrecte, vecteur absent/présent incohérent.
6. **IPC et smoke .NET–Python :** confirmer les champs, la version 0.2.0, le calcul de D et la continuité numérique du nominal.

## Ce qui n'est PAS validé
- Bruit de capteur réel : spectre, densité, filtrage électronique, étalonnage et délais de mesure.
- Saturation, résolution, temps de rafraîchissement, états qualité et défauts d'un instrument réel.
- Reprise manuel/automatique, dérivée au changement de mode, bumpless transfer ou comportement d'un bloc constructeur.
- Continuité du procédé hors horizon, stabilité garantie, équivalence Schneider PIDFF, Siemens PID_Compact ou autres.
- Essai visuel Windows sur poste réel et essai sur banc automate autorisé.

## Verrou avant exploitation
Le statut **prototype scientifique hors ligne uniquement** reste appliqué. Toute promotion vers une fonction de réglage industriel nécessite des références de calcul indépendantes, validation de mesures réelles et un banc constructeur identifié avec procédure de test, journal d'écarts, réversibilité et autorisation d'exploitation.
