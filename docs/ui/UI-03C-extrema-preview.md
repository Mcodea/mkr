# UI-03C — Réduction de l'affichage préservant les extrema et les changements brusques

## Justification
La version antérieure du tracé des essais MV/PV conservait 600 indices uniformément espacés. Ce procédé pouvait écarter un échelon de MV ou un pic de PV d'une seule ligne, alors même que l'ingénieur cherchait précisément ces événements pour préparer l'identification.

Le workflow industriel visé (analyse des tendances avant identification, à l'image des logiciels d'identification et réglage PID étudiés pour MKR) exige que l'aperçu ne fasse pas disparaître arbitrairement les principaux événements visibles. Le sous-lot ne reprend aucune implémentation propriétaire.

## Algorithme d'affichage 1.1.0
- Toutes les données sources sont analysées et validées au préalable, sans modifier les CSV originaux.
- Jusqu'à 600 lignes, les lignes sources sont toutes affichées, dans leur ordre d'origine.
- Au-delà de 600 lignes, le moteur sépare les lignes en **60 portions chronologiques** et conserve, dans chaque portion, au plus dix indices distincts :
  - première et dernière ligne ;
  - indices des minimum et maximum MV ;
  - indices des minimum et maximum PV ;
  - les deux côtés de la plus forte variation adjacente de MV ;
  - les deux côtés de la plus forte variation adjacente de PV.
- Les doublons d'indices sont supprimés, puis les résultats sont triés chronologiquement.
- Chaque point garde son indice original, ses secondes depuis l'origine UTC et les valeurs MV/PV réellement lues. **Il n'y a ni interpolation, ni moyenne, ni modification des données utilisées pour identifier le procédé.**
- L'affichage comporte au plus 600 points ; un nombre inférieur à 600 est normal lorsque des extrema coïncident.

## Sûreté et traçabilité
- Les extrema globaux de MV et PV sont préservés par construction, car l'extremum de chaque portion est conservé.
- Les plus grandes transitions adjacentes de chaque portion sont représentées par leur point antérieur et leur point postérieur.
- La sélection graphique s'appuie sur les indices des points affichés mais la fenêtre d'identification utilise **toutes les lignes sources comprises entre ces indices**, y compris celles qui n'étaient pas représentées à l'écran.
- Le rendu reste une réduction : plusieurs événements courts dans une même portion peuvent rester masqués. Une validation industrielle exige toujours l'examen du CSV original et des contrôles scientifiques.

## Tests ajoutés
- Pic MV d'un échantillon, pic PV isolé et extrema signés.
- Front de marche MV conservant les deux côtés de la transition.
- Maintien de la première et de la dernière ligne, tri strict et plafond de 600.
- Passage complet par l'opération Python IPC `preview_trial` avec anomalies insérées dans les CSV.
- Préservation des données complètes sur les séries de 600 lignes ou moins.
- Non-régression des sélections de fenêtres UI-03B2 et du contrat .NET `TrialPreview`.

## Validation
Exécuter les tests Python, compiler WPF sur Windows et lancer le smoke test .NET–Python–SQLite. Vérifier également sur Windows le comportement du tracé pour des historiques à échelons multiples, à pics étroits et à instants non régulièrement espacés. **La réussite CI ne remplace pas la recette visuelle réelle.**

## Suite
UI-04 : panneau comparatif de simulation PI (IMC/SIMC, SP/PV, CV calculée/MV appliquée, saturation, anti-windup), conservant les mêmes principes de provenance et de précision d'affichage.
