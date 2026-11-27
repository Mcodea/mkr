# TCLab — présence du fichier dans l'historique Git

- **Fichier** : `tests/reference/tclab_dyn_data2_extract.csv` (5 463 octets).
- **Commit d'introduction** : `0a7407e` (« Real TCLab data (two heaters, 121 points)… »), seul commit à le toucher.
- **Branches distantes qui le contiennent** : `claude/peaceful-cray-phk6q4` et `claude/science-engine-dev` (publiées sur GitHub, dépôt `Mcodea/mkr`). Le retrait du 2026-11-27 supprime le fichier de l'arbre de travail courant, **pas de l'historique**.
- **Visibilité du dépôt** : non vérifiée depuis cette session (`AGENTS.md` signale un conflit de confidentialité privé/public non tranché).

## Mesures supplémentaires nécessaires pour un effacement complet (NON réalisées — autorisation requise)
1. Réécriture de l'historique (par ex. `git filter-repo --path tests/reference/tclab_dyn_data2_extract.csv --invert-paths`) sur **toutes** les branches publiées, avec **force-push** : opération destructive qui change tous les identifiants de commits (le tag `ui-ux-recue-2026-11-26`, les liens vers les commits, les runs CI cités dans les rapports).
2. Invalidation des clones, forks et caches GitHub existants (demande au support GitHub pour purger les objets non référencés et les vues en cache) ; toute copie déjà téléchargée n'est pas récupérable.
3. Décision préalable du client : l'effacement est-il nécessaire (selon la réponse des détenteurs des droits et la visibilité du dépôt) ou la suppression de l'arbre courant suffit-elle ?
