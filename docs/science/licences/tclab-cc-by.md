# Jeu TCLab réel sous CC BY 4.0 : preuves de licence et décision

- **Relevé** : 27/11/2026, vers 10 h 40 UTC, branche `claude/science-engine-dev`.
- **Nature** : constat factuel. **Ce n'est pas un avis juridique.**
- **Objet** : remplacer `tests/reference/tclab_dyn_data2_extract.csv` (extrait APMonitor sans licence) par des mesures TCLab réelles. Leur licence doit permettre l'utilisation, la modification et la redistribution commerciale.

## 1. Enregistrement retenu : Zenodo 24286966 (TCLab Take-Home Labs)

| Preuve | Source lue | Texte relevé |
|---|---|---|
| Métadonnées | `https://zenodo.org/api/records/24286966` | `"license": {"id": "cc-by-4.0"}`, version `1.1`, DOI `10.5281/zenodo.24286966`, publié le 2026-11-16, mis à jour le 2026-11-17 |
| Page publique | `https://zenodo.org/records/24286966` | « Creative Commons Attribution 4.0 International », lien vers `https://creativecommons.org/licenses/by/4.0/legalcode` |
| README de l'archive | `readme.md` de `tclab_takehome_labs_dataset_v1.0.zip` | « Unless stated otherwise, the data are provided under the Creative Commons Attribution 4.0 International license (CC BY 4.0). » |
| Prépublication | `https://arxiv.org/abs/2611.21200` | Mêmes auteurs (Goubej, Hedengren, Sukovatý), licence de l'article `creativecommons.org/licenses/by/4.0` |
| Résolution du DOI | `https://doi.org/10.5281/zenodo.24286966` | HTTP 302 vers `zenodo.org/doi/10.5281/zenodo.24286966` |

Auteurs : Martin Goubej (University of West Bohemia), John Hedengren (Brigham Young University), Daniel Sukovatý (University of West Bohemia in Pilsen).

Aucun fichier de l'archive ne contient de mention « stated otherwise ». L'archive comprend 9 CSV, 5 PDF de figures et `readme.md`. La somme MD5 de l'archive téléchargée (`143ac608…c447`) est égale à celle publiée par Zenodo.

**Conclusion : licence confirmée.** CC BY 4.0 permet de partager et d'adapter, y compris à des fins commerciales. Elle oblige à :

- attribuer l'œuvre ;
- donner l'URI de la licence ;
- indiquer les modifications ;
- conserver l'exclusion de garantie ;
- ne pas ajouter de restriction (§2(a)(5)(B), §3(a)).

Le §4 couvre les droits *sui generis* sur les bases de données (UE) aux mêmes conditions.

**Point d'attention** : l'entrée BibTeX du `readme.md` porte un DOI fictif `10.5281/zenodo.XXXXXXX`. Il faut citer le DOI réel `10.5281/zenodo.24286966`, ainsi que arXiv:2611.21200 comme le demandent les auteurs.

## 2. Fichier utilisé et extrait

- **Source** : `data/experiment_siso_model_validation.csv`.
  - SHA-256 `180d6bdb9632563783143ad8130f9e986269f3fa7fc758836c3efa159d28f288`.
  - Boucle ouverte (`controller=open_loop`), 1 s, 0 à 1 000 s.
  - H1 de 0 à 70 % à 20 s, puis H2 de 0 à 30 % à 300 s.
- **Fichiers MIMO écartés** : `experiment_mimo_robust_control_real*.csv` sont en **boucle fermée** (régulateur `robust_mu`). La variante `_filtered` est lissée par les auteurs.
- **Extrait** : `tests/reference/tclab_cc_by/tclab_takehome_siso_openloop_extract.csv`.
  - Lignes `time_s` 1 à 1 000, contiguës, valeurs copiées à l'identique.
  - Colonnes `timestamp,H1,H2,T1,T2`.
  - Horodatage reconstruit sur l'origine arbitraire 2000-01-01T00:00:00Z.
  - SHA-256 `eb24ddbd5b2f0a3c613c90cfb7941c4a6682d8ab15855698976cab5ecd101ad6`, 65 570 octets.
- **Documentation** : `tests/reference/tclab_cc_by/README.md` (modifications, protocole, limites) et `ATTRIBUTION.txt`.

## 3. Alternatives examinées

| Piste | Constat |
|---|---|
| InTaSet, Zenodo 17652851 | CC BY 4.0 (métadonnées Zenodo), mais il s'agit d'un **système à quatre réservoirs d'eau**, pas d'un banc thermique. Il n'est pas utile pour remplacer TCLab. |
| APMonitor (`tclab_dyn_data2`) | Pas de licence (constat déjà fait dans `jeux-de-donnees.md`). À retirer, comme le client l'a décidé. |

Accès réseau : `export.arxiv.org` a été **refusé par le proxy** (403). La page `arxiv.org/abs/…` a été utilisée à la place. `api.crossref.org` répond « Resource not found » pour ce DOI DataCite, ce qui est normal : un DOI Zenodo n'est pas enregistré chez Crossref.

## 4. Décision recommandée

1. **Adopter** l'extrait CC BY 4.0 comme jeu TCLab réel de référence, et retirer `tclab_dyn_data2_extract.csv`.
2. **Écrire un nouveau test** : les assertions actuelles de `engine/test_real_tclab_dynamic.py` (pas de 5 s, quatre changements de H1, valeurs exactes) ne s'appliquent pas. Il faut détecter les échelons à 20 s et 300 s, et identifier H1→T1 sur la fenêtre 20–299 s, avant que H2 ne bouge.
3. **Couverture moindre** : l'essai n'a que deux échelons montants (un par voie), sans échelon descendant. Si le test exige plusieurs changements de la même commande, il faut **chercher un second jeu** ou faire un **essai propre** sur un TCLab d'ORIAS (données entièrement maîtrisées).
4. **Notice tierce** : si l'extrait est livré dans un paquet ou redistribué, joindre `ATTRIBUTION.txt` (ou son contenu) à la notice tierce. Ajouter une ligne au tableau de `jeux-de-donnees.md`.
