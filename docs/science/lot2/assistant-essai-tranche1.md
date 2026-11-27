# LOT 2 — assistant d'essai en temps réel, tranche 1 (dossier AGENTS.md §5)

**Origine de l'exigence.** Cahier directeur, section 42.2 (assistant d'essai), 42.3 (intelligence scientifique, estimations provisoires), 42.4 (instructions simples, durée restante non estimable signalée), 42.5 (lecture seule, jamais d'autorisation de sécurité). Autorisation de poursuite du client du 2026-11-27 (lots suivants selon la feuille de route actualisée).

**Dans le périmètre de cette tranche.** `engine/trial_assistant.py`, opération IPC `assess_trial` (contrat révision 3) : procédé **autorégulant**, **boucle ouverte**, échelon sur MV. Entrée : tampon d'acquisition (temps, MV, PV, mode manuel éventuel), tolérances déclarées. Sortie : état de l'essai, estimation Broïda **provisoire**, passages 28 %/40 % (instants et niveaux de PV, pour le tracé et la traçabilité), durée restante conditionnelle, messages sous forme de codes.

**Hors périmètre (déclaré, pas deviné).** Boucle fermée (excitation SP), procédé intégrateur sans plateau, grand temps mort, oscillant, réponse inverse, plusieurs dynamiques : état `not_supported_yet`. Aucune interface (le tracé et les instructions en français restent à faire côté WPF : section 42.6 n'est **pas** satisfaite). Pas de sauvegarde ni de journal d'essai (LOT 1 : fiche d'étude, journal d'événements).

**Équations et critères.** Tous ramenés aux règles de `fopdt.read_step` et de `broida.estimate` (échelon : variation de MV > 2 × tolérance ; amplitude ≤ 10 × tolérance : insuffisante ; régime initial : ≥ 4 lignes à moins de la tolérance PV de leur moyenne) ; variation finale « établie » : reste prévu |réponse| exp(−(t−L)/τ) au début de la fenêtre de plateau ≤ 2 × tolérance PV, plancher L + 4τ conservé de la v1.1. Durée requise = L + τ ln(|réponse|/(2 tol)) + fenêtre ; durée restante = requise − observée. **Aucun seuil universel de durée.**

**Limites connues, à ne pas masquer.**
1. La durée restante est **conditionnelle** au modèle FOPDT supposé et **biaisée par défaut** tant que le plateau n'est pas atteint (le niveau final lu est trop bas) ; mesuré sur l'essai synthétique : sous-estime de 1,5 s sur 11,6 s à T = 45 s ; l'écart se resserre en fin d'essai (test dédié).
2. « Régime initial stable » = au moins 4 lignes, minimum de l'estimateur ; ce n'est pas une preuve de stationnarité au sens statistique. Un seuil plus exigeant exige des données réelles (non disponibles ici).
3. Aucun essai sur automate simulé ni réel n'a été fait ; l'assistant n'est testé que sur flux synthétiques tronqués (FOPDT exact, forme close).
4. Un `ready_for_step` ne vaut **jamais** autorisation : le champ `safetyAuthorization` est constant à `false` et testé.

**Tests (oracle indépendant : forme close du FOPDT exact).** `engine/test_trial_assistant.py` (23 tests) : états successifs sur un flux tronqué à 2 s, 20 s, 45 s, 70 s ; accord avec l'identification définitive (`broida.identify` refuse à 45 s, accepte à 70 s) ; refus (rampe MV, échelon trop petit, saturation déclarée, base de temps non strictement croissante, charges utiles invalides) ; durée maximale ; boucle fermée et intégrateur déclarés non traités ; aller-retour IPC.

**Preuves impossibles ici.** Poste Windows (affichage), automate simulé Modbus/S7 avec perte de communication pendant un essai, essai réel sur banc : à faire aux tranches suivantes.
