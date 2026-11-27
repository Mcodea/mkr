# Extrait TCLab réel sous CC BY 4.0 (boucle ouverte, deux chauffages)

Fichier : `tclab_takehome_siso_openloop_extract.csv` (1 000 lignes de données, 65 570 octets).
Remplaçant proposé de `tests/reference/tclab_dyn_data2_extract.csv` (extrait APMonitor sans licence claire). Les tests existants ne sont **pas** branchés sur ce fichier (voir « Limites »).

## Provenance

| Élément | Valeur |
|---|---|
| Jeu de données | *Dataset for: Hands-On Beyond Classrooms: Elevating Control Engineering Education with Take-Home Labs*, version 1.1 |
| Auteurs | Martin Goubej (University of West Bohemia), John Hedengren (Brigham Young University), Daniel Sukovatý (University of West Bohemia in Pilsen) |
| Éditeur | Zenodo, publié le 16/11/2026, mis à jour le 17/11/2026 |
| DOI | [10.5281/zenodo.24286966](https://doi.org/10.5281/zenodo.24286966) (page : https://zenodo.org/records/24286966) |
| Publication associée | arXiv:2611.21200, https://arxiv.org/abs/2611.21200 (relation Zenodo `isSupplementTo`) |
| Archive téléchargée | `tclab_takehome_labs_dataset_v1.0.zip`, 834 181 octets, MD5 `143ac608400a06d2ffc26535d507c447` (identique à la somme publiée par Zenodo), SHA-256 `a12f6fe10ca4491fb1728293d50b18e9e0bd88ff760393a76dd9938d52da47e4` |
| Fichier source | `data/experiment_siso_model_validation.csv`, 113 618 octets, 1 001 lignes de données, SHA-256 `180d6bdb9632563783143ad8130f9e986269f3fa7fc758836c3efa159d28f288` |
| Extrait | `tclab_takehome_siso_openloop_extract.csv`, SHA-256 `eb24ddbd5b2f0a3c613c90cfb7941c4a6682d8ab15855698976cab5ecd101ad6` |
| Date de téléchargement | 27/11/2026 |

## Licence et obligations

Creative Commons Attribution 4.0 International (CC BY 4.0), lue à trois endroits : métadonnées Zenodo (`"license": {"id": "cc-by-4.0"}`), page de l'enregistrement (« Creative Commons Attribution 4.0 International »), et `readme.md` de l'archive : « Unless stated otherwise, the data are provided under the Creative Commons Attribution 4.0 International license (CC BY 4.0). » Aucun fichier de l'archive ne déclare d'autre licence. Détail des preuves : `docs/science/licences/tclab-cc-by.md`.

CC BY 4.0 autorise l'utilisation, la modification et la redistribution, y compris commerciale. Obligations lors de toute redistribution (§3(a) du texte légal) :

1. citer les auteurs et la source (DOI) ;
2. nommer la licence et donner son URI ;
3. conserver la mention d'absence de garantie ;
4. **indiquer les modifications** ;
5. ne pas ajouter de restriction juridique ou technique (DRM) aux droits accordés (§2(a)(5)(B)) ;
6. ne pas laisser entendre un soutien des auteurs.

Le texte prêt à citer est dans `ATTRIBUTION.txt`. Il doit accompagner le fichier s'il est livré ou redistribué (paquet, notice tierce). Les auteurs demandent aussi de citer la prépublication (`readme.md` de l'archive). Note : l'entrée BibTeX de ce `readme.md` contient un DOI fictif `10.5281/zenodo.XXXXXXX`. Le DOI réel est `10.5281/zenodo.24286966`.

## Ce qui a été changé

1. **Extraction de lignes** : sous-ensemble contigu `time_s` = 1 à 1 000 (1 000 lignes). La ligne `time_s = 0` est écartée : elle vaut `T1 = T2 = 23` exactement. C'est la valeur initiale par défaut du modèle TCLab, et non une mesure (les lignes suivantes valent 18,91 / 19,40 °C, sur la grille de quantification du capteur).
2. **Colonnes** : seules `time_s, Q1_pct, Q2_pct, T1_meas_C, T2_meas_C` sont gardées, renommées `timestamp, H1, H2, T1, T2` (même format que l'ancien extrait). Les colonnes constantes `experiment_id=siso_model_validation`, `run_id=run_001`, `kit_id=TCLab_001`, `controller=open_loop` et `parameter_set=initial_parameters` sont supprimées.
3. **Horodatage reconstruit** : `timestamp` = `2000-01-01T00:00:00+00:00` + `time_s` secondes. L'origine est **arbitraire** (la vraie date de l'essai n'est pas publiée). Elle correspond à `time_s = 0` de la source, donc la première ligne est `00:00:01`.
4. **Valeurs** : H1, H2, T1, T2 sont **copiées caractère pour caractère** (par exemple `18.9149560117302`). Il n'y a ni lissage, ni rééchantillonnage, ni arrondi. Le script d'extraction vérifie l'identité des chaînes ligne à ligne.

## Protocole de l'essai (tel que publié)

- Matériel : TCLab Arduino, deux chauffages à transistor, deux capteurs TMP36 (`readme.md` de l'archive). Kit `TCLab_001`, essai `run_001`, mode `open_loop`.
- Période d'échantillonnage : 1 s, régulière, de 0 à 1 000 s.
- Commandes, lues dans les données :
  - de 0 à 19 s : H1 = 0 %, H2 = 0 % ;
  - à 20 s : **H1 passe de 0 à 70 %**, H2 reste à 0 % ;
  - à 300 s : **H2 passe de 0 à 30 %**, H1 reste à 70 % ;
  - jusqu'à 1 000 s : pas d'autre changement.
- Températures : T1 part d'environ 18,9 °C et culmine à 83,9 °C. T2 monte avec H1 seul, par couplage thermique entre les deux chauffages, puis avec H2, jusqu'à 61,4 °C à 1 000 s.
- Usage dans l'article : validation de modèle (figure 3), comparée à deux simulations (`simulation_siso_model_validation_par1/par2.csv`, non reprises ici).

## Limites

- **Deux échelons seulement**, un par chauffage. Ils sont montants, sans retour à 0 et sans échelon descendant. L'ancien extrait APMonitor avait quatre changements de H1 (10, 200, 280 et 400 s) au pas de 5 s. Les assertions de `engine/test_real_tclab_dynamic.py` (lignes de changement 2, 40, 56, 80, première valeur 19.29, etc.) **ne s'appliquent pas** à ce fichier. Il faudra un nouveau test, pas un simple remplacement de chemin.
- **Couplage MIMO** : après 300 s, T1 et T2 dépendent des deux chauffages. Une identification SISO H1→T1 propre se limite à la fenêtre 20–299 s. Sur cette fenêtre, T1 n'a pas atteint son régime permanent (65,8 °C à 299 s, encore en hausse).
- **Quantification** : les températures sont sur la grille du convertisseur 10 bits, avec un pas d'environ 0,49 °C (par exemple 18,915 / 19,404). Le bruit apparent est donc en paliers.
- **Une seule réalisation**, un seul kit. Pas de répétition, pas de mesure de la température ambiante, pas d'incertitude publiée.
- Les colonnes `parameter_set=initial_parameters` et la valeur initiale 23/23 laissent penser que le fichier a été mis en forme avec le code de simulation des auteurs. Le `readme.md` le présente néanmoins comme une mesure brute (« Raw experimental data »).
