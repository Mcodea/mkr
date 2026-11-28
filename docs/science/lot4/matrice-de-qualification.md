# Matrice de qualification (protocole §23) — état au 2026-11-28

Niveaux : 0 non développé · 1 développé non vérifié · 2 vérifié mathématiquement · 3 validé numériquement (simulation/synthétique) · 4 validé sur données expérimentales · 5 validé indépendamment à l'aveugle · 6 qualifié sur automate · 7 qualifié sur procédé industriel réel. **Aucun niveau n'est attribué sans preuve ; en cas de doute, le niveau inférieur.**

| Fonctionnalité | Niveau | Preuve / limite |
|---|---|---|
| Import CSV, qualité des données (comptages descriptifs) | 3 | tests synthétiques ; **ne détecte pas** les pertes de capteur codées par une valeur, ni la saturation, ni l'incohérence horodatage/cadence (constats F-S01-1 à 4) |
| Diagnostic automatique : saturation, perte de capteur possible, cadence (`signal_diagnosis.py`) | 4 (un banc) | rappel 100 % sur 9 812 pertes annotées (B+C), précision ≈ 99 %, bornes de l'actionneur retrouvées sans limite saisie ; **un seul banc, une seule convention de codage ; non intégré à l'application** |
| Identification FOPDT / SOPDT / intégrateur | 3 | campagnes synthétiques à vérité connue ; données réelles : TCLab (extrait) en régression seulement |
| Méthode de Broïda (28 %/40 %) | 3 | équations vérifiées, critère de plateau justifié ; capture réelle de l'interface (essai synthétique) |
| Critère d'erreur ARX automatique (bruit sans modèle, 3σ) | 1 (étudié, non adopté) | domine les limites fixes à bruit faible/modéré, ne maîtrise pas le risque à fort bruit ; critère d'adoption pré-enregistré non satisfait ; non appelé par le moteur |
| ARX (excitation arbitraire, boucle ouverte) | 3 ; 4 (deux bancs, comportement de refus) | refus justifié sur S03 (non linéaire) ; décision **dépendante d'une limite d'erreur saisie** sur S04 (F-ARX-1) ; fenêtres courtes ; pas d'aptitude à la régulation évaluée |
| Identification en boucle fermée (2 étapes) | 3, **expérimentale** | simulations seulement (ADR 0006) |
| Modèles d'ordre supérieur / non linéaires / multivariables | 0 | aucun module trouvé |
| Porte de validation (RMSE + lag-1) | 3 | étude synthétique tranche 1 ; non conclusive pour la comparer à d'autres critères |
| Nouveau critère (erreur de structure relative au bruit, E, C7) | 1 | étudié, **non adopté** (rapport tranche 1) |
| Réglage SIMC avec Ms sur coins d'incertitude | 3 (formules : 2) | références Skogestad 2003 ; coins = estimation, pas garantie |
| Stabilité (Nyquist + temps) | 2–3 | accord 120/120 ; durée finie ≠ preuve |
| Lambda, Ziegler-Nichols, Cohen-Coon | non audité | à vérifier dans `tuning_comparison.py` |
| Stratégies : cascade, feedforward, ratio, split-range, override, Smith, gain scheduling, MPC | 0 | aucun module de réglage trouvé |
| Sélection intelligente du modèle (aptitude à la régulation) | 1–3 partielle | comparaison sur la prédiction existante ; critère de régulation à consolider |
| Assistant d'essais | 3 | synthétique ; pas d'essai réel |
| Blocs PI_B, PIDFF, PID_INT, PID_Compact | 2 (conversion) / 3 (simulation du modèle) | documentation officielle ; **non testés chez le constructeur** ; **niveaux 6–7 : non atteints** |
| Obsolescence par dépendances, preuve de validation scellée | 3 | 37 contrôles + CI Windows ; câblage de bout en bout inachevé |
| Persistance SQLite (migration 12) | 3 | Smoke Windows vert (run 387) |
| Données réelles du protocole (S01, S03, S04) | inventoriées | téléchargées et contrôlées ; **aucune exploitation encore** ; D (aveugle) non réalisable |
| Interface Windows (WPF) | 3–4 visuel | captures réelles CI (identification, Broïda, simulation lues) ; pas de poste Windows utilisateur |
| Validation à l'aveugle | **non réalisée** | aucune séparation effective des accès |
| Qualification sur automate / procédé industriel | **non réalisée** | moyens matériels à définir par le client |
