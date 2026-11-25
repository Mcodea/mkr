# Simulation comparative PI sur FOPDT

Depuis une étude FOPDT puis un réglage PI enregistré, ouvrir **Simuler**. Entrer le pas, la durée, l’échelon de consigne et les bornes de ΔMV entourant zéro. L’instant de perturbation et la durée doivent être des multiples du pas. Une perturbation optionnelle s’additionne à la MV appliquée à l’entrée du procédé, après saturation. Les valeurs sont des écarts autour du point initial. Les séries exportées en CSV comportent le temps en secondes, la consigne et les PV dans l’unité PV, puis les CV, MV et la perturbation dans l’unité MV. Une ligne terminale fournit la PV, avec les commandes vides faute d’intervalle suivant.

## Intégration du procédé FOPDT

Le modèle FOPDT initialement à l’équilibre est régi par `dy/dt = (K u(t−θ)−y)/τ`. Pour une entrée constante sur un sous-intervalle de durée d, la transition exacte est `y ← y + (1−exp(−d/τ))(K u−y)`. Le retard entier est traité par l’historique de MV et le retard fractionnaire par deux sous-intervalles au sein d’un même pas, sans interpolation de commande. L’historique avant t = 0 est nul en variables d’écart.

Pour chaque méthode, le PI numérique calcule `CV[k]=Kp(SP−PV[k])+I[k]` et applique la saturation `MV[k]=clip(CV[k])`. Avec Euler explicite, `I[k+1]=I[k]+h(Kp/Ti)e[k]`. Si CV est sur ou au-delà d’une limite et que cet incrément éloigne la commande de l’intervalle admissible, l’intégrale est gelée pour ce pas. L’incrément est exprimé en unité MV, ce qui préserve le sens d’action lorsque Kp est négatif. La saturation affichée compte les pas où les bornes d’amplitude modifient CV. Le gel de l’intégration est compté séparément.

Les IAE et ISE sont les sommes à gauche de `h|e[k]|` et `h e[k]²`. L’erreur finale est calculée sur la PV au dernier instant. Le pas comporte 2 à 5 000 intervalles. Un pas supérieur au dixième de τ ou un retard atteignant l’horizon sont signalés. Le résultat est nominal et exploratoire. La dérivée PID, les changements de mode et le transfert sans à-coup restent à traiter.

Le stockage conserve scénario, version, réglage d’origine, diagnostics et séries complètes. Les tests vérifient la convergence vers une réponse analytique sans retard, la réponse avec retard fractionnaire et saturation, la commande inverse, la perturbation et le gel de l’intégrale.

## Échelon en boucle ouverte

Depuis une étude FOPDT, ouvrir **Simuler en boucle ouverte**. Saisir le pas, la durée, l’instant et l’amplitude signée de l’échelon de ΔMV ainsi que les bornes autour de zéro. La durée et l’instant de l’échelon sont des multiples du pas. L’échelon est non nul et précède la fin de l’horizon, qui comporte 2 à 5 000 intervalles.

Sans limite de vitesse, la MV reste à zéro jusqu’à l’échelon, puis prend la valeur demandée bornée jusqu’à la fin. Après l’instant de l’échelon augmenté du retard θ, la PV suit la réponse exacte `K × ΔMV appliquée × (1 − exp(−temps écoulé / τ))`. Le retard conserve sa valeur fractionnaire. Le graphique présente la PV et le niveau initial. La durée de saturation compte les intervalles où la MV demandée diffère de la MV appliquée. Le CSV exporte le temps, la PV et les deux séries de MV. L’export est disponible pendant la session de simulation.


## Limite de vitesse et comparaison à la consigne (méthode 1.1.0)

La vitesse maximale facultative est exprimée en unité MV/s. Le moteur borne d’abord la commande en amplitude, puis borne sa variation à ±(vitesse × pas) par rapport à la MV précédente, initialisée à zéro. La première variation bornée est appliquée au premier pas actif. La MV est maintenue constante sur chaque intervalle. Les durées de saturation et de limitation de vitesse comptent respectivement les écarts de commande dus à chaque borne et peuvent se recouvrir. En boucle fermée, l’intégrale est gelée lorsque son incrément pousse la commande au-delà de la plage réalisable sur le pas. La perturbation s’ajoute ensuite à l’entrée du procédé.

En boucle fermée, le dépassement est l’écart maximal au-delà de la consigne dans le sens de l’échelon, en unité PV. Le temps de réponse est le premier instant échantillonné à partir duquel la PV reste dans la bande ±5 % de l’amplitude de la consigne jusqu’à la fin de la fenêtre, avec au moins deux points dans la bande. La fenêtre commence à 0 s et s’arrête au premier de l’horizon ou de l’instant de perturbation non nulle. Le point à l’instant de perturbation est inclus, sa PV étant lue avant l’application de la commande. Une consigne nulle ou une fenêtre sans intervalle rend ces indicateurs indisponibles. Si la bande n’est pas tenue, seul le temps de réponse est indisponible. Les IAE et ISE restent calculées sur l’horizon complet.
