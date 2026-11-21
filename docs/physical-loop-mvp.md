# Description de boucle physique — MVP

Origine : demande utilisateur « 11. Modèle de la boucle physique ». Base cible : PR #18, commit b5b6d2e. Ce lot décrit les signaux, équipements et contraintes ; il ne fait ni identification, ni calcul de régulation, ni interface, ni persistance.

Hypothèses : chaque plage est exprimée dans l'unité du signal ; les limites de contrainte sont inclusives et contenues dans la plage physique déclarée. La vitesse facultative est exprimée en unité du signal par seconde et strictement positive. La consigne utilise l'unité de la grandeur régulée et appartient à sa plage et à ses contraintes. Les perturbations peuvent être absentes (liste vide), chacune possède sa propre unité et sa plage. L'unité est une chaîne explicite, y compris pour une grandeur sans dimension (par exemple « 1 »). Aucun catalogue d'unités ou conversion n'est supposé.

La validation explicite rejette les champs manquants, les plages inversées ou dégénérées, les nombres non finis, les contraintes hors plage et les consignes hors limites. Les bornes déclarées sont contrôlées pour leur cohérence ; elles ne constituent pas une vérification indépendante des limites d'un équipement réel. Les DTO peuvent être construits sans validation : l'appelant doit invoquer `PhysicalLoopValidator.Validate` avant utilisation.

Vérification prévue : `dotnet run --project tests/Mkr.PhysicalLoop.Tests` pour les cas nominaux, limites, champs absents, unités, valeurs non finies et contraintes incohérentes. La compilation, la CI Windows, la validation métier des unités et plages et les équipements réels restent à vérifier. Aucune migration ni modification IPC. Retour arrière : retirer les fichiers ajoutés.
