# ADR 0006 — Identification en boucle fermée

Statut : **EXPLORATOIRE, moteur seul**. Le mainteneur n'a pas pu répondre aux questions ouvertes et a demandé un outil industriel universel, indépendant de l'automate. Le moteur Python est implémenté (`engine/closed_loop.py`, opération `identify_closed_loop`) sous l'étiquette « expérimental » ; il n'a **aucun écran** et n'est pas offert à l'utilisateur. Il ne passera pas en « implémenté » sans les données réelles exigées plus bas.

## Contexte

MKR identifie aujourd'hui en **boucle ouverte** : un échelon de MV réalisé avec le régulateur en manuel (acquisition avec le mode lu à l'adresse que l'utilisateur déclare, recherche d'essais qui écarte les lignes hors manuel). Beaucoup d'installations industrielles ne peuvent pas passer en manuel (sécurité, procédé instable, exigences de production). Pour elles, l'outil ne sert à rien. L'exigence du mainteneur est que MKR soit utilisable dans l'industrie en général, pas sur un seul banc.

## Le problème, en une phrase

En boucle fermée, MV est calculée à partir de PV : elle est **corrélée au bruit de mesure et aux perturbations**. Ajuster un modèle de MV vers PV comme en boucle ouverte donne un résultat **biaisé**, et le biais n'est pas signalé par la porte de validation actuelle, qui ne voit que l'erreur de prédiction.

## Preuve préalable (essai exploratoire, hors dépôt)

Procédé FOPDT K = 2, τ = 30 s, θ = 6 s ; PI Kp = 0,5, Ti = 25 s ; consigne à trois échelons (+1, −1, +0,5) ; bruit de mesure blanc (σ = 0,05) et perturbation de charge colorée non mesurée ; 20 tirages, 1500 échantillons à 1 s.

| Méthode | K moyen | τ moyen | θ moyen | Écart-type de τ |
|---|---|---|---|---|
| Vrai procédé | 2,00 | 30 | 6 | |
| Ajustement direct MV → PV, comme en boucle ouverte | 1,89 | 35,3 | 7,3 | 10,7 |
| Deux étages (ci-dessous) | 1,96 | 32,3 | 6,15 | 10,4 |

À lire avec prudence : **un seul scénario, 20 tirages, pas une preuve**. Il montre un biais réel de la méthode naïve (K −5,5 %, τ +17 %, θ +1,3 s) et une réduction avec la méthode à deux étages, mais aussi une **forte dispersion** : la boucle fermée rend l'identification moins précise, pas seulement biaisée. L'ADR en tient compte en refusant de promettre une exactitude.

## Décision proposée

1. **Excitation par la consigne uniquement.** La consigne est le signal externe, non corrélé au bruit. MKR ne l'écrit jamais dans l'automate : l'utilisateur fait lui-même les changements de consigne avec ses outils. Les perturbations ou ajustements manuels de MV pendant l'essai sont refusés.
2. **Méthode à deux étages** (Van den Hof & Schrama, 1993) : (a) projeter MV sur l'historique de la consigne (réponse impulsionnelle finie estimée par moindres carrés) pour ne garder que la part de MV due à la consigne ; (b) ajuster le modèle (FOPDT, SOPDT ou intégrateur) de cette MV projetée vers PV. Elle **ne demande ni la structure ni les paramètres du régulateur**, ce qui évite de supposer la forme du PID réel du constructeur.
3. **Entrées obligatoires :** SP, PV, MV enregistrées ensemble, mode automatique (colonne `MANUAL` à 0 sur la fenêtre, d'après l'adresse déclarée par l'utilisateur), limites de MV déclarées.
4. **Refus explicites, jamais d'ajustement silencieux :** sortie saturée dans la fenêtre ; consigne constante ou variation inférieure à un multiple du bruit (excitation insuffisante) ; fenêtre qui contient du manuel ; résidus corrélés (perturbation) ; moins de deux changements de consigne indépendants (estimation et validation distinctes, comme en boucle ouverte).
5. **Même porte de validation** que la boucle ouverte, sur une fenêtre indépendante, avec l'étiquette « identifié en boucle fermée » et un critère plus strict (dispersion publiée, voir ci-dessous).
6. **Dispersion publiée.** Les écarts-types d'estimation viennent d'un rééchantillonnage des résidus (bootstrap par blocs), affichés avec chaque paramètre. Un intervalle trop large pour régler est un refus, pas un avertissement.

## Alternatives écartées

| Alternative | Raison |
|---|---|
| Ajustement direct comme en boucle ouverte | Biais démontré ci-dessus. |
| Méthode indirecte avec régulateur connu | Exige le régulateur exact (structure, anti-windup, filtres du constructeur), non documenté de façon homogène. Contraire à la consigne « n'invente aucune règle non documentée ». |
| Relais (Åström–Hägglund) | Exige d'écrire dans l'automate. MKR est en lecture seule (AGENTS.md). |
| Modèle ARX/ARMAX d'erreur de prédiction | Cohérent en boucle fermée, mais le modèle de bruit à choisir est une hypothèse de plus et le passage au modèle de procédé continu est indirect. |

## Vérification exigée avant « implémenté »

1. **Simulations en boucle fermée** de procédés variés (échelles de temps, rapports θ/τ, gains des deux signes, intégrateur, perturbations colorées, bruit) dans la suite `test_process_diversity`, avec critères chiffrés fixés avant les résultats.
2. **Comparaison au naïf** : la méthode retenue doit réduire le biais de façon mesurée sur ces cas, sinon elle ne se justifie pas.
3. **Cas de refus** : saturation, consigne plate, perturbation, fenêtre en manuel.
4. **Données réelles** : au moins un enregistrement de boucle fermée d'un banc ou d'une installation, validé contre un essai en boucle ouverte du même procédé. **Non disponible aujourd'hui.**

## Risques

- **Dispersion** (voir ci-dessus) : l'utilisateur peut obtenir des intervalles trop larges pour régler. Le refus est préférable à une valeur trompeuse.
- **Non-linéarités** (vannes, saturation douce) : l'essai en consigne les excite autour d'un point de fonctionnement ; le modèle n'est valable que là.
- **Régulateurs qui modifient MV hors consigne** (anticipation, sélecteurs, cascade) : l'hypothèse d'une MV fonction de SP et PV seulement est fausse ; à déclarer par l'utilisateur et à signaler.
- **Fausse assurance :** une porte qui accepte à tort serait plus dangereuse qu'aucune. D'où les critères plus stricts et l'obligation de données réelles.

## Questions ouvertes : réponses prises par défaut

Le mainteneur ne sait pas y répondre ; les choix suivants découlent de l'exigence « universel et indépendant de l'automate » et sont **révisables** :

1. *Boucles visées :* tout régulateur PID en automatique, quelle que soit la marque. La méthode ne suppose ni structure ni paramètres du régulateur, donc rien de Schneider ou Siemens n'y entre.
2. *Données réelles :* non disponibles. La fonction reste exploratoire et n'est pas exposée dans l'interface tant qu'un enregistrement réel n'a pas été comparé à un essai en boucle ouverte.
3. *Intervalle trop large :* refus sans réglage. Retenu.

## État de l'implémentation et résultats mesurés (simulations uniquement)

- **Méthode :** deux étages (FIR de la consigne vers MV, puis ajustement FOPDT de la MV projetée vers PV), délai fractionnaire, 8 lignes de plateau au moins pour mesurer le bruit.
- **Refus :** échantillonnage non uniforme, saturation de MV, consigne plate ou noyée dans le bruit (écart < 20 fois le bruit mesuré), lignes en manuel (colonne `MANUAL`), fenêtres qui se recouvrent, absence de plateau initial, dispersion trop grande (K > 30 %, τ > 50 %).
- **Indépendance du régulateur :** deux réglages PI très différents donnent des paramètres voisins (tests).
- **Comparaison au naïf :** sur 6 tirages bruités, l'erreur moyenne sur τ est plus faible qu'avec l'ajustement direct de MV (test).
- **Résidus corrélés :** avec une perturbation de charge colorée non mesurée, les résidus sont corrélés **même pour un modèle correct**. Mon premier critère « autocorrélation de rang 1 » refusait à tort des modèles exacts (K = 2,00, τ = 29,9) ; il est devenu un avertissement `correlated_residuals`. Le refus repose sur l'erreur de prédiction de la fenêtre indépendante et sur la dispersion.
- **Dispersion publiée, calibrée par Monte Carlo :** le rééchantillonnage des résidus seul sous-estimait la dispersion d'un facteur 8 (SE de τ : 1,3 s publié pour 11,1 s réels). La dispersion publiée est maintenant le maximum de ce rééchantillonnage et de l'écart entre deux estimations indépendantes (fenêtres d'estimation et de validation). Mesuré sur 14 tirages : erreur-type de τ publiée 7,5 s pour 10,9 s réels (rapport d'environ 0,7), gain 0,195 pour 0,259. **Elle sous-estime encore d'environ 30 %.** Le test exige un rapport entre 0,5 et 2 : ce n'est pas une garantie.
- **Précision :** dispersion élevée sous bruit et perturbation fortes (écart-type de τ d'environ 11 s pour τ = 30 s dans ce scénario) : la boucle fermée donne une identification moins précise, et elle peut être refusée.

## Retour arrière

Le module n'est branché à aucun écran : le retirer (`closed_loop.py`, son test, l'opération `identify_closed_loop`) ne change rien au produit livré.
