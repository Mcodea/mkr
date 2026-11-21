# Réglage SIMC PID continu sur SOPDT

La commande IPC `calculate_simc_pid` prend les champs de `calculate_pi` et
`secondTauSeconds`. Le procédé doit être stable avec deux pôles réels de
constantes τ1 ≥ τ2 > 0, un gain signé K non nul et un retard θ ≥ 0.
La cible de réponse τc est strictement positive. Tous les temps sont en secondes.

La méthode de Skogestad (§ 2.2, équations 2 à 5) donne une forme série de gain
τ1/[K(θ+τc)], de temps intégral min(τ1, 4(θ+τc)) et de temps dérivé τ2.
Le moteur développe ce produit pour fournir également les coefficients idéaux
continus sur l’erreur. Une dérivée sur mesure ou un filtre ajouté change
la fonction de transfert. Ces résultats servent à une étude théorique ;
la mise au point demande le choix explicite du filtre, du cycle et du bloc.

Source : https://skoge.folk.ntnu.no/publications/2001/skogestad_reno/tuningpaper.pdf
