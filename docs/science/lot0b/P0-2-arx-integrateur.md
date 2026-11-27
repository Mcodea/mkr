# P0-2 — L'ARX publie un intégrateur comme un premier ordre

## Anomalie
`engine/arx.py::first_order_equivalent` convertissait tout ARX(1,1) de pôle discret p ∈ (0,1) en premier ordre (τ = −Ts/ln p, K = b/(1−p)), y compris quand p est indiscernable de 1 : c'est alors un intégrateur, pour lequel τ et K divergent et ne sont pas identifiables (les données ne fixent que K/τ, la pente).

## Preuve
`engine/test_arx_integrator_guard.py` : intégrateur avec retard simulé par récurrence exacte (cumul), excitation PRBS, bruit gaussien, 8 graines fixes, oracle indépendant de l'estimateur. **Avant correction, graine 4 :** τ = 82 677 s, K = 2 073 publiés (pôle 0,999976). Témoin positif : un vrai premier ordre (τ = 20 s) reste publié (≥ 7 graines sur 8) et proche de la vérité à 5 erreurs-types.

## Impact
Un intégrateur pouvait être présenté comme un procédé du premier ordre à constante de temps colossale, avec des erreurs-types parfois petites (estimateur de sortie, covariance linéarisée) : un réglage guidé aurait pu partir d'un modèle sans signification physique. Seul un refus en aval (« incertitude excluant le modèle ») l'empêchait dans le cas relevé.

## Référence
Ljung (1999) et Söderström & Stoica (1989) pour l'identifiabilité et la covariance linéarisée — **ouvrages non accessibles légalement depuis les hôtes autorisés : non vérifiés** (voir `docs/science/references/fiches/identifiabilite-arx-integrateur.md`). Les deux conditions retenues sont des conditions de bon sens documentées, pas une règle publiée vérifiée : (1) pôle distinct de 1 au niveau 99 % déjà employé par le fichier ; (2) τ ≤ durée enregistrée (une constante de temps plus longue que l'enregistrement ne se voit pas comme une courbure).

## Correction
`first_order_equivalent(..., horizon)` retourne `None` si `p + 2,576·σ(a1) ≥ 1` ou si `τ > horizon` (durée de la série d'estimation). Le modèle ARX reste un prédicteur valide ; seul l'équivalent « premier ordre » n'est plus publié. Le C# gère déjà `FirstOrder = null` (refus du réglage guidé). Aucun critère d'acceptation du modèle ARX n'est modifié.

## Résultats avant / après
| Cas | Avant | Après |
|---|---|---|
| Intégrateur PRBS, graine 4 | premier ordre τ = 82 677 s, K = 2 073 | aucun équivalent premier ordre |
| Intégrateur, 8 graines | 1 publication aberrante sur 8 | 0 sur 8 |
| Premier ordre τ = 20 s, 8 graines | publié | publié (≥ 7/8), conforme à la vérité |

## Limites
Domaine : ARX(1,1), bruit gaussien, enregistrement stationnaire. Reste ouvert (LOT 3) : le moteur ARX ne détecte pas la boucle fermée malgré sa docstring, et l'ARX n'est toujours pas confronté à un oracle externe (SIPPY exclu, python-control sans identification).
