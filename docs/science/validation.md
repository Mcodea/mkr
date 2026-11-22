# MKR-SCI — Entrée en développement scientifique

FOPDT/SOPDT et intégrateurs nécessitent un protocole de validation indépendant incluant excitation, séparations temporelles, résidus, incertitudes et limites. Le retard apparent peut combiner transport, cinétique, mélange, analyseur et filtrage. Avant livraison des méthodes IMC/Lambda et SIMC, sélectionner les références de chaque variante et figer forme PID, unités, signes, conventions numériques et domaine. La simulation devra tester retards, périodes, saturation, anti-windup, changements de mode et convergence. Le diagnostic du moteur v1 ne produit aucun calcul scientifique.

## Indépendance des essais par échelon

L’écran distingue une sélection répétée, deux imports de contenu binaire identique et des intervalles qui se recouvrent. Le moteur compare en UTC les bornes inclusives des fenêtres choisies. Le test de réexport passe par le contrat JSON des quatre méthodes et de la comparaison : ordre et noms des colonnes, séparateur, fins de ligne et fuseau sont modifiés en conservant les mêmes instants. Le contrôle chronologique ne détecte pas une copie dont les dates ont été artificiellement décalées. L’opérateur confirme que les essais ont effectivement été réalisés séparément.
