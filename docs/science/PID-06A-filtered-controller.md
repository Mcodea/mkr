# PID-06A — Prototype scientifique du PID filtré sur FOPDT

## Objet et statut
Prototype **hors ligne, exploratoire**, implémenté séparément des réglages PI validés dans MKR. L'utilisateur ne peut pas encore sélectionner ce régulateur depuis les écrans Windows : le périmètre de ce premier lot est **moteur Python, contrat .NET typé et tests**. Aucun coefficient PID n'est généré automatiquement, et aucune correspondance avec un bloc PLC constructeur n'est revendiquée.

## Procédé et conventions
Procédé identifié FOPDT en variables d'écart :

\[
G(s)=\frac{K\, e^{-\theta s}}{\tau s+1}
\]

Erreur \(e=SP-PV\). Le régulateur est de forme **idéale / dépendante**, avec P et I agissant sur l'erreur et la dérivée agissant **uniquement sur la PV** pour éviter un pic de commande lors d'un échelon de consigne :

\[
CV(t)=K_p\,e(t)+I(t)-\frac{K_p T_d s}{T_f s+1}PV(t).
\]

\(\dot I(t)=(K_p/T_i)e(t)\). Un filtre de dérivée d'ordre un est imposé si \(T_d>0\), avec \(T_f>0\). \(T_d=0\) supprime exactement l'action dérivée et autorise \(T_f=0\).

## Convention temporelle et algorithme
- Pas d'échantillonnage constant \(h>0\), consigne constante, données de procédé initialisées à zéro en écart.
- Intégration I : Euler explicite conditionnel, comme le simulateur PI MKR existant.
- Dérivée : **Euler implicite** pour le filtre dérivé, évaluée sur les mesures échantillonnées \(PV_k\) :

\[
D_k=\frac{T_f}{T_f+h}D_{k-1}
-\frac{K_p T_d}{T_f+h}(PV_k-PV_{k-1}),\quad D_0=0.
\]

- \(CV_k=K_p(SP-PV_k)+I_k+D_k\).
- La MV est écrêtée aux bornes physiques et limitée en variation si un \(dMV/dt\) maximal est fourni. La perturbation entre **à l'entrée du procédé**, après la limitation de la commande actionneur.
- Anti-windup par **intégration conditionnelle**, incluant la limitation de vitesse. Lorsqu'une borne effective est atteinte et que le nouvel incrément intégral pousserait dans le sens de la limitation, l'intégration est gelée pour ce pas.
- Simulation de la dynamique FOPDT avec conservation du retard fractionnaire et réponse exacte par morceaux sous maintien d'ordre zéro de l'entrée, en réutilisant `fopdt_simulation.plant_step`.
- Le résultat comprend CV avant limites, MV après limites, contribution D, PV, perturbation, IAE, ISE, écart final, saturation, limitation de vitesse, gel I, fenêtre de réponse et avertissements.
- Tous les points sont conservés jusqu'à 5 000 intervalles ; au dernier instant, les commandes CV/MV et D sont nulles au sens **absentes**, car il n'y a pas de nouvel intervalle à commander.

## Références publiques
- MathWorks, *Proportional-Integral-Derivative (PID) Controllers*, documentation des formes de régulation, parallèle et dépendante : https://www.mathworks.com/help/control/ug/proportional-integral-derivative-pid-controllers.html
- MathWorks, *Discrete-Time PID Controllers*, conventions d'intégration et filtrage de dérivée : https://www.mathworks.com/help/control/ug/discrete-time-proportional-integral-derivative-pid-controller.html
- MathWorks, *PID Controller*, anti-windup conditionnel (« clamping ») et back-calculation, qui sont **deux algorithmes distincts** : https://www.mathworks.com/help/simulink/slref/pidcontroller.html
- Rockwell Automation Studio 5000, configuration PID, dérivée sur PV ou erreur selon le bloc et ses paramètres : https://www.rockwellautomation.com/en-us/docs/studio-5000-logix-designer/37-00/contents-ditamap/studio-5000-logix-designer/pid-configuration-dialog-box---configuration-tab-o.html

Ces sources inspirent les conventions et l'explicitation du filtre. Elles ne prouvent **pas** que ce prototype soit équivalent numériquement à MATLAB, Schneider, Siemens ou Rockwell.

## Hypothèses, limites et interdits
- Réglage **manuel seulement** : `kp`, `tiSeconds`, `tdSeconds`, `filterSeconds`. Aucune méthode propriétaire ou règle empirique de tuning PID générée.
- Le gain de commande \(K_p\) doit avoir le signe du gain du procédé \(K\) pour la convention \(e=SP-PV\).
- Pas de modes manuel/auto, transfert sans à-coups, suivi de sortie, mesure bruitée, dérivée sur erreur ni gain de filtre constructeur, pour ce lot.
- La méthode intégrale et la dérivée sont explicites et testables. Un meilleur résultat numérique sur un scénario ne garantit pas la stabilité ou la robustesse globale.
- Le choix \(T_f\) est un paramètre d'essai ; pas de conversion automatique en coefficient de filtre `N` d'un PLC.
- Si \(h>T_f\), un avertissement `coarse_derivative_filter` signale que le filtre est grossièrement échantillonné.
- Aucun raccordement à une installation industrielle ou écriture automate. Ce modèle n'est **pas** une qualification fabricant.

## Tests
1. Avec \(T_d=0\), le PID doit reproduire **exactement** les séries PI MKR (réponses PV, commandes, métriques, saturation, anti-windup).
2. La dérivée débute à zéro et sa récurrence est contrôlée par un oracle numérique indépendant ; elle n'agit pas sur un changement isolé de consigne.
3. Limite \(T_d\to0\), gain procédé négatif, retard fractionnaire, perturbation, saturation, limitation de vitesse.
4. Rejet des domaines invalides, constantes de filtre nulles pour une action D non nulle, temps incompatibles et NaN.
5. Contrat IPC `simulate_pid_experimental` testé via le client `PythonDiagnostics` et test smoke .NET–Python.
6. CI WPF Release, Python unittest et smoke SQLite. **Validation graphique et industrielle distinctes et non couvertes par PID-06A.**

## Suite
PID-06B : écran d'essai explicite pour paramètres manuels, superposition PI/PID, comparaison d'indicateurs, export des séries et sélection du filtre. Puis une phase distincte pour réglages PID calculés (avec méthode publiée, domaine de validité, signes et tests) et une phase ultérieure pour les mappings constructeurs vérifiés.
