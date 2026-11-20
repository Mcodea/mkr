# Simulation comparative PI sur FOPDT

Depuis une étude FOPDT puis un réglage PI enregistré, ouvrir **Simuler**. Entrer le pas, la durée, l’échelon de consigne et les bornes de ΔMV entourant zéro. L’instant de perturbation et la durée doivent être des multiples du pas. Une perturbation optionnelle s’additionne à la MV appliquée à l’entrée du procédé, après saturation. Les valeurs sont des écarts autour du point initial. Les séries exportées en CSV comportent le temps en secondes, la consigne et les PV dans l’unité PV, puis les CV, MV et la perturbation dans l’unité MV. Une ligne terminale fournit la PV, avec les commandes vides faute d’intervalle suivant.

## Méthode 1.0.0

Le modèle FOPDT initialement à l’équilibre est régi par `dy/dt = (K u(t−θ)−y)/τ`. Pour une entrée constante sur un sous-intervalle de durée d, la transition exacte est `y ← y + (1−exp(−d/τ))(K u−y)`. Le retard entier est traité par l’historique de MV et le retard fractionnaire par deux sous-intervalles au sein d’un même pas, sans interpolation de commande. L’historique avant t = 0 est nul en variables d’écart.

Pour chaque méthode, le PI numérique calcule `CV[k]=Kp(SP−PV[k])+I[k]` et applique la saturation `MV[k]=clip(CV[k])`. Avec Euler explicite, `I[k+1]=I[k]+h(Kp/Ti)e[k]`. Si CV est sur ou au-delà d’une limite et que cet incrément éloigne la commande de l’intervalle admissible, l’intégrale est gelée pour ce pas. L’incrément est exprimé en unité MV, ce qui préserve le sens d’action lorsque Kp est négatif. La saturation affichée correspond strictement aux pas où CV diffère de MV, et se distingue des pas d’intégration gelée.

Les IAE et ISE sont les sommes à gauche de `h|e[k]|` et `h e[k]²`. L’erreur finale est calculée sur la PV au dernier instant. Le pas comporte 2 à 5 000 intervalles. Un pas supérieur au dixième de τ ou un retard atteignant l’horizon sont signalés. Le résultat est nominal et exploratoire. Il ne comprend ni dérivée PID, ni limitation de vitesse, ni changement de mode ou transfert sans à-coup. Il ne prouve pas la stabilité robuste ni la performance industrielle.

Le stockage conserve scénario, version, réglage d’origine, diagnostics et séries complètes. Les tests vérifient la convergence vers une réponse analytique sans retard, la réponse avec retard fractionnaire et saturation, la commande inverse, la perturbation et le gel de l’intégrale.
