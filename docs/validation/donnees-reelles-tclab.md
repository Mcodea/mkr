> **Mise à jour 2026-11-27 — données retirées du dépôt courant** (licence de redistribution non établie ; décision du client). Les résultats ci-dessous sont l'**historique d'une régression** sur ces mesures ; le test correspondant est ignoré tant que la donnée est absente, et la couverture équivalente sans donnée réelle est `engine/test_two_heater_unmodelled_input.py` (synthétique, à ne pas présenter comme expérimental). Voir `tests/reference/README-tclab.md`.

# Premier essai de MKR sur des mesures réelles à excitation variée (TCLab)

Date : 25/11/2026. Données : [tests/reference/README-tclab.md](../../tests/reference/README-tclab.md) (extrait de 121 points, transcription fournie par le mainteneur, fidélité à la source non recoupée). Test : `engine/test_real_tclab_dynamic.py`.

## Ce qui a été fait
Entrée H1 → sortie T1, en boucle ouverte. H2 change quatre fois pendant l'essai (couplage thermique non modélisé en SISO). Aucune valeur n'a été ajustée pour obtenir un résultat.

## Résultats observés (moteur MKR au SHA indiqué dans l'historique du dépôt)
| Contrôle | Résultat |
|---|---|
| Recherche d'échelons (`find`) | **4 échelons de H1 rejetés** : `baseline_too_short_or_unstable` (la mesure n'est pas stabilisée avant chaque changement de commande) ; aucun essai exploitable en échelon isolé |
| ARX / OE, 4 découpages apprentissage / validation × limites d'erreur de 0,5, 1,0 et 2,0 °C | **Refus dans tous les cas** (résidus de validation non blancs et/ou erreur de simulation libre excessive ; premier ordre ou second ordre) |
| Contrôle indépendant de MKR (moindres carrés à un pas, SISO, simulation libre sur 320–520 s) | Le meilleur modèle explique **environ 20 %** de la variance de la température mise de côté (RMSE 1,7 °C pour un écart-type de 1,9 °C) |
| Même contrôle avec H2 comme seconde entrée (modèle linéaire naïf) | Pas mieux hors apprentissage (RMSE 3,3 à 11 °C selon les retards) |

## Ce que cela démontre — et ne démontre pas
- **Démontre** : sur ces mesures réelles, MKR **ne propose pas** de modèle que l'on ne puisse pas valider hors échantillon. Le refus n'est pas un artefact : un calcul indépendant arrive à la même conclusion (les données ne soutiennent pas un modèle linéaire SISO simple valide sur une période disjointe).
- **Ne démontre pas** : que MKR identifie correctement un procédé réel. C'est un cas de **refus justifié**, pas un cas d'**acceptation validée**. Aucun réglage n'est issu de ces données.
- **Limites** : extrait transcrit ; système MIMO couplé et non linéaire (pertes thermiques dépendantes de la température) ; segments courts ; pas de 5 s ; critère à 40 % fixé après observation de la valeur (≈ 20 %).

## Pour obtenir un cas d'acceptation réel
Un essai en boucle ouverte avec **une seule entrée qui bouge** (l'autre fixée), un **plateau stable avant chaque échelon** (plusieurs minutes) et un pas d'échantillonnage de l'ordre de la seconde, sur un banc dont on connaît les conditions (ex. TCLab piloté seul sur H1, H2 = 0). Le protocole du [kit de qualification](kit-qualification.md) le décrit.
