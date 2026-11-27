# DONNÉES SYNTHÉTIQUES — scénario de pression par pompe et variateur

Ce dossier ne contient que des données produites par simulation, jamais des mesures industrielles.

- Régénérer : `python tests/reference/scenario_pression_pompe/generate.py`
- Vérifier que deux exécutions donnent les mêmes octets : ajouter l'option `--check`

Le générateur est autonome : il n'importe rien de `engine/`. Les graines, les paramètres vrais, les unités et l'intention de chaque jeu sont dans `data/*.manifest.json`. Les empreintes sont dans `data/SHA256SUMS`.

Documentation : `docs/science/scenario-pression-pompe-m580/`.
