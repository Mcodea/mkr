# SIM-05A — Sensibilité des réglages PI aux paramètres du procédé

## Hypothèses et périmètre
L'analyse est une étude **locale de sensibilité**, déterministe, réalisée sur un procédé nominal de type FOPDT

\[
G(s)=\frac{K e^{-\theta s}}{\tau s+1}
\]

et sur les deux réglages PI IMC/Lambda et SIMC déjà calculés par MKR.

Pour une variation relative `v` (%) sélectionnée par l'utilisateur (valeur par défaut 20 %, limite : `0 < v <= 50`), le moteur simule sept configurations : nominale, `K(1-v/100)`, `K(1+v/100)`, `τ(1-v/100)`, `τ(1+v/100)`, `θ(1-v/100)`, `θ(1+v/100)`. Un seul paramètre change à la fois. **Les coefficients Kp/Ti des deux PI restent exactement identiques** dans toutes les simulations.

Le gain conserve son signe même s'il est négatif; `τ` reste strictement positive. Un modèle nominal sans retard (θ = 0) conserve θ = 0 dans les deux cas de variation de retard.

## Méthode numérique
L'opération Python `simulate_pi_robustness` appelle à sept reprises le moteur `pi_simulation.calculate` déjà testé, plutôt que de dupliquer sa dynamique. Le scénario utilisateur (consigne, période, pas, limitations MV, limitation de vitesse, perturbation) reste fixe.

Chaque combinaison restitue l'IAE, l'ISE, l'erreur finale, le temps en saturation, le temps à vitesse limitée, les pas de blocage de l'intégration, le dépassement et le temps de réponse si défini. Les valeurs sont enregistrées avec la simulation nominale, le réglage PI sélectionné et ses références d'étude dans `PiSimulationStudy.Robustness` (champ JSON optionnel). La base SQLite v6 n'est pas migrée et les anciennes simulations restent lisibles.

L'interface contient un bouton « Comparer la robustesse », un pourcentage configurable et un tableau des sept scénarios. Un résumé des **IAE maximales rencontrées parmi ces seuls cas** est présenté pour IMC et SIMC. Il ne s'agit pas d'un maximum global sur l'espace de paramètres.

## Références et inspiration métier
- Les outils industriels de réglage et d'analyse de boucle comme Control Station LOOP-PRO Tuner et MATLAB PID Tuner mettent en avant la comparaison des réponses et les compromis de réglage. MKR suit ces objectifs de décision sans copier d'algorithme ni de présentation propriétaire.
- Le balayage un-paramètre-à-la-fois est volontairement distinct d'une analyse probabiliste de Monte-Carlo ou d'une recherche de robustesse multivariée.
- La convention reste `e = SP - PV`, avec CV calculée et MV appliquée après limitations.

## Limitations scientifiques explicites
1. **Ce calcul n'est pas une preuve de stabilité**, ni un calcul de marge de phase ou de gain, ni un intervalle de confiance statistique.
2. Les interactions simultanées entre K, τ et θ ne sont pas couvertes dans ces sept scénarios.
3. Les variations choisies sont des hypothèses de l'utilisateur et **ne représentent pas** l'incertitude statistique du modèle identifié.
4. Un résultat fini sur l'horizon de simulation ne garantit pas un comportement acceptable sur un horizon plus long.
5. La saturation et le blocage de l'intégration restent ceux du modèle simulé ; la compatibilité avec un bloc constructeur réel n'est pas garantie.
6. La simulation utilise des écarts autour d'un point de fonctionnement, non la dynamique d'un procédé hydraulique complet.
7. Aucune connexion/écriture automate n'est présente.

## Tests automatisés ajoutés
- Les indicateurs du cas nominal correspondent exactement au simulateur PI existant.
- Les six perturbations appliquent les facteurs attendus et **ne recalculent jamais les coefficients PI**.
- Gain inverse négatif et retard nominal nul.
- Paramètres hors domaine, indicateurs et contrat d'échange IPC.
- Test .NET/Python/SQLite : persistance puis relecture des sept cas et de leurs valeurs.

## Critères de validation
- Compilation WPF Release, tests scientifiques Python et tests de parcours SQLite–Python sur GitHub Actions Windows.
- Inspection réelle WPF à 100 %, 150 %, 200 % DPI, fenêtre minimale et maximisée.
- Comparaison des valeurs nominales avec la simulation initiale et vérification des colonnes du tableau.
- Réouverture d'une simulation de robustesse enregistrée, contrôle de la provenance et vérification des valeurs.
- Aucun changement des paramètres PI ou des rapports d'identification.
- Les scénarios aux valeurs extrêmes peuvent être rejetés avec une erreur explicite : jamais de résultat fictif.

## Suite SIM-05B
Balayages combinés avec contrôle de cardinalité, enveloppes des réponses temporelles, comparaison de scénarios nominaux/perturbés et analyse de critères de performance adaptés ; ne pas qualifier des « marges de stabilité » sans méthode démontrée et cas de référence.
