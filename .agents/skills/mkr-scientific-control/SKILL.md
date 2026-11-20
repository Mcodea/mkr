---
name: mkr-scientific-control
description: Développer ou auditer les modèles FOPDT/SOPDT/intégrateur, calculs PI/PID, IMC/SIMC, simulations et métriques scientifiques de MKR.
---

# Calcul scientifique et régulation — MKR

1. Lire les spécifications dans `docs/science/` et les algorithmes/tests correspondants dans `engine/`.
2. Documenter équations, hypothèses, conventions : SP, PV, CV calculée, MV appliquée; `e = SP - PV`; unités, gain signé, temps et pas.
3. Identifier la forme précise du modèle et du PID avant conversion; ne jamais transposer aveuglément des paramètres vers un bloc constructeur.
4. Identification : séparer calibration et validation, inspecter excitation, saturation, bruit, retard, résidus, identifiabilité et domaine de validité; conserver la provenance et les avertissements.
5. Simulation : gérer retard fractionnaire, maintien d'ordre zéro, bornes, rampe MV, modes, anti-windup et tracking si pertinents; tester les cas limites.
6. Établir au moins un oracle indépendant par fonctionnalité (solution analytique, limite physique, conservation ou comparaison de référence); mesurer convergence temporelle et comportement en gain négatif.
7. Tests : données nominales, bruyantes, insuffisantes, non finies, échantillonnage grossier, horizons trop courts, saturation, inversions de signe.
8. Ne pas utiliser un bon `R²` seul comme justification de validité. Ne pas qualifier la confiance statistique sans hypothèses et couverture documentées.
9. Retourner versions d'algorithme, diagnostics et raisons de rejet, y compris dans les réponses IPC.

Livrable : équations sourcées si nouvelle méthode, implémentation reproductible, tests exécutés et limites explicites.
