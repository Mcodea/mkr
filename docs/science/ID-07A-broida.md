# ID-07A — Identification Broïda FOPDT, méthode des 28 % / 40 %

## Références et formulation
- Étude académique « Teaching experience for process identification using first-order-plus-time-delay models », tableau des méthodes à deux points (Broïda : seuils 28 %/40 %, temps et retard) : https://onlinelibrary.wiley.com/doi/full/10.1002/cae.22794
- L. Kamenova, publication universitaire présentant la relation de Broïda et ses approximations : https://ecad.tu-sofia.bg/et/2004/Papers/Circuits%20and%20Systems/Paper-L_Kamenova.pdf
- Sujet de BTS CIRA, épreuve d'instrumentation, rappels de la méthode (28 % et 40 %) : https://sites.ac-nancy-metz.fr/physique/SiteAcademiqueOld/ancien_site/PHYS/Bts-Cira/annales/2006/U41-2006-instrum.pdf

Le procédé est modélisé en boucle ouverte, en variables d'écart :
`PV(t) = PV0 + K × ΔMV × (1 - exp(-(t-θ)/τ))` pour t > θ, et `PV0` auparavant.

Définir les temps **à partir du premier échantillon après l'échelon MV détecté** :
`t28` lorsque la variation PV vaut 28 % de la variation finale mesurée ;
`t40` à 40 %. Les relations **approchées**, volontairement non ajustées par moindres carrés, sont :

- `τ = 5,5 × (t40 - t28)` ;
- `θ = 2,8 × t28 - 1,8 × t40` ;
- `K = (PVfinal - PV0) / ΔMV`.

Les constantes 5,5 ; 2,8 ; 1,8 sont la version classique arrondie, qui n'est pas algébriquement identique à la résolution exacte des exponentielles aux seuils de 28 % / 40 %. Les validations numériques autorisent la petite erreur d'approximation inhérente à la méthode et à l'échantillonnage. **Aucun affinage FOPDT par optimiseur** n'est pratiqué sous le nom Broïda.

## Entrées, détection et refus
MKR réutilise l'extraction d'échelon du moteur FOPDT existant : horodatages réels, tolérances définies, plateau MV avant/après, PV stable avant échelon, indépendance stricte des deux essais et plages originales choisies. L'estimation se fait **uniquement** sur l'essai d'estimation ; l'autre essai sert exclusivement aux diagnostics de validation.

Pour obtenir `PVfinal`, la méthode utilise la moyenne des six dernières mesures **du premier essai**. Elle impose :
- Au moins 18 valeurs PV après échelon.
- Amplitude PV finale strictement supérieure à la résolution tolérée ; PV finale de signe quelconque.
- Dispersion des six dernières mesures ≤ deux fois la tolérance PV.
- Courbe de réponse globalement monotone, sans inversion supérieure à deux fois la tolérance PV et sans dépassement excédant cette résolution.
- Passages t28 et t40 présents et strictement ordonnés ; instants interpolés **linéairement entre deux échantillons réels**, sans créer de nouveaux échantillons scientifiques.
- `τ > 0` et `θ ≥ 0` ; **jamais de remise forcée à zéro d'un retard négatif**.
- Horizon depuis l'échelon couvrant au moins **4τ après le retard identifié**.

Un plateau local ne garantit pas un vrai régime permanent si un procédé est très lent : cette limite subsiste. Les rejets explicites `broida_plateau`, `broida_shape`, `broida_response`, `broida_crossing`, `broida_domain`, `broida_horizon` signalent qu'aucune identification fiable n'a été produite.

## Résultats et traçabilité
- Nouvelle option `BroidaFOPDT` dans la liste de familles d'identification ; algorithme identifié `1.0.0`.
- Les paramètres K, τ, θ, avertissements et diagnostics RMSE/MAE/R²/résidus sur les deux essais sont enregistrés avec les références CSV, colonnes, indices de fenêtre et version moteur.
- Métadonnées `BroidaEvidence` persistées : t28, t40, PV terminale, nombre de points, seuils [.28, .40] et origine temporelle. L'interface les restitue dans le rapport d'étude.
- Le champ historique `delayUpperSeconds` du contrat d'identification indique ici **l'horizon temporel de l'essai**, pas une borne d'optimisation ni une limite de confiance. Les champs `tauLowerSeconds`, `tauUpperSeconds` et `standardErrors` restent absents, faute de justification d'une incertitude probabiliste fiable pour cette méthode à deux points.
- Le calcul PI IMC/SIMC reste réservé, pour ce sous-lot, aux études FOPDT antérieures : pas de transfert automatique Broïda vers un réglage d'exploitation.
- Aucun fichier CSV original n'est modifié et aucune communication PLC n'est ajoutée.

## Validation
- Réponses analytiques FOPDT de gain positif et négatif, avec échantillonnages différents pour les deux essais.
- Invariance stricte de K, τ, θ et des seuils lors du changement de l'essai de validation ; diagnostic de validation correctement impacté.
- Plateau terminal non stabilisé, réponse non monotone, horizon trop court, absence d'échelon, chevauchement temporel.
- Comparaison avec FOPDT existant, qui ne doit pas changer d'algorithme ni être réétiqueté Broïda.
- Vérification IPC de l'opération `identify_broida` et persistance SQLite de la preuve de passages.
- Compilation Windows WPF, tests Python et smoke .NET/Python/SQLite avant acceptation ; la recette graphique sur Windows réel et la qualification sur essais industriels restent des phases supplémentaires.

## Suite
**ID-07B** : étude contradictoire sur plusieurs jeux de données de référence avec incertitudes documentées, comparaison Broïda / FOPDT par moindres carrés et examen des cas où le plateau ne peut pas être établi.

**TUNE-08** : variantes de Ziegler–Nichols (réponse indicielle / gain ultime), implémentées séparément avec leurs domaines d'applicabilité et risques d'agressivité documentés, sans provoquer une oscillation sur un site industriel réel.
