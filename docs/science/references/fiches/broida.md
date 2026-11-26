# Fiche — Méthode de Broïda (FOPDT à deux points, 28 % et 40 %)

**État de vérification :**
- **Notice : V.** Crossref, le 26/11/2026, confirme le DOI (jusque-là déduit du pii), le volume, le numéro, les pages et l'auteur.
- **Contenu (seuils et coefficients) : NV.** Je n'ai lu ni l'article ni sa version française.

## Référence
- V. Broida, « The determination of large time-constants by step-response extrapolation », *Automatica* **5**(5), 677–683, septembre 1969. **DOI 10.1016/0005-1098(69)90033-8** (Crossref, V). L'auteur est enregistré sous la forme « V. Broida », sans tréma.
- Version française présumée : « L'extrapolation des réponses indicielles apériodiques », *Automatisme* XIV(3), mars 1969, p. 105–114. **NV** : aucune notice primaire trouvée.

## Tentatives d'accès (sans contournement)
- La page ScienceDirect (https://www.sciencedirect.com/science/article/abs/pii/0005109869900338) répond HTTP 403 à curl (page anti-robot) ; WebFetch ne résout pas l'hôte.
- OpenAlex classe l'article « closed », sans dépôt en libre accès (https://api.openalex.org/works/doi:10.1016/0005-1098(69)90033-8, V).
- Les cours en ligne (universités de M'Sila, de Tlemcen, de Strasbourg) apparaissent dans les résultats de recherche : ce sont des sources secondaires, non lues et hors des hôtes autorisés.
- **Il reste à obtenir l'article par un abonnement institutionnel légitime ou par un prêt entre bibliothèques.**

## Équations : ce qui est un calcul, pas une citation
Pour un premier ordre à retard y(t) = K Δu [1 − e^(−(t−θ)/τ)] avec un plateau initial connu :
- t28 = θ + τ ln(1/0,72) et t40 = θ + τ ln(1/0,60)
- d'où τ = (t40 − t28)/ln(0,72/0,60) ≈ 5,485 (t40 − t28)
- et θ = t28 − 0,3285τ ≈ 2,802 t28 − 1,802 t40

Ce calcul est refait ici ; il était déjà dans l'annexe E du LOT 0. Les coefficients 5,5, 2,8 et 1,8 du code en sont des arrondis. **La source primaire n'a pas confirmé qu'ils sont ceux de Broïda.** Le titre suggère d'ailleurs un autre objet : l'extrapolation des grandes constantes de temps.

## Hypothèses (du calcul)
- Réponse apériodique du premier ordre exact, échelon franc à l'origine des temps, plateaux initial et final mesurés.
- Unités : secondes ; gain en unité de PV par unité de MV.
- Pour un procédé d'ordre supérieur, les valeurs obtenues sont un FOPDT **approché**, et la qualité de l'approximation dépend des seuils.

## Rapport au code
`engine/broida.py` (version 1.1.0) :
- `tau = 5.5*(t40 - t28)`, `delay = 2.8*t28 - 1.8*t40`, conformes au calcul ci-dessus ;
- refus si le retard est négatif, si le plateau est instable ou si l'horizon est insuffisant (moins de 4τ après le retard).

Erreur d'arrondi : l'arrondi 5,5 et 2,8/1,8 introduit un biais relatif de **+0,28 % sur τ** par rapport au modèle exact (5,5/5,4848). Sur θ, le biais vaut +0,0018(t40 − t28) (calcul : θ exact = 2,8018 t28 − 1,8018 t40).

## Critères de validation proposés
1. Oracle analytique : un FOPDT exact sans bruit doit restituer τ et θ avec un biais au plus égal au biais d'arrondi calculé, plus l'erreur d'interpolation liée à l'échantillonnage.
2. Ne pas appeler la méthode « Broïda (1969) » dans un rapport de qualification tant que le texte primaire n'a pas confirmé les seuils. D'ici là : « méthode à deux points 28 %/40 % (dite de Broïda, source primaire non lue) ».
