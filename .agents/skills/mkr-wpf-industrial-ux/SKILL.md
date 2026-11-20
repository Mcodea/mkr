---
name: mkr-wpf-industrial-ux
description: Concevoir ou améliorer l'interface WPF/MVVM industrielle MKR, ses graphiques, tableaux, navigation, accessibilité et validation visuelle.
---

# Interface WPF industrielle — MKR

1. Lire la vue, ViewModel, contrat métier et parcours utilisateur existants avant tout redesign.
2. Construire une hiérarchie constante : navigation projet/étude, zone de travail, propriétés contextuelles, résultats/diagnostics; privilégier l'information technique à la décoration.
3. Favoriser styles et contrôles réutilisables, `Grid` adaptatif, dimensions minimales, libellés clairs, navigation clavier, localisation française et unités visibles.
4. Éviter chevauchements et contrôles inactifs; gérer les états vide, chargement, erreur, validé, non validé et données manquantes.
5. Graphiques : axes et unités, légendes, séries mesurées/modélisées séparées, zoom/selection si utile, visibilité des périodes de validation, export reproductible; aucune courbe fictive présentée comme réelle.
6. Valider entrées numériques avec culture et unité, domaine physique, messages compréhensibles; jamais de correction silencieuse.
7. Les calculs longs et acquisitions ne doivent pas bloquer le thread UI; prévoir annulation sûre et état après interruption.
8. Tester redimensionnement, DPI 100/150/200 %, clavier, rafraîchissement et fermeture pendant opérations; tests graphiques manuels sur Windows obligatoires avant qualification ergonomique.
9. Si environnement Linux uniquement, distinguer revue XAML, CI Windows et validation visuelle non effectuée.

Livrable : parcours réel, tests pertinentes, captures Windows uniquement si réellement obtenues et bilan des défauts UX.
