# Données réelles TCLab (extrait de 121 points)

`tclab_dyn_data2_extract.csv` : essai en **boucle ouverte** sur un banc TCLab (deux chauffages H1, H2 en % de puissance ; deux températures T1, T2 en °C), un point publié toutes les 5 s sur 600 s, sans interpolation ni lissage. Colonnes : `timestamp` (origine arbitraire 2000-01-01 UTC, temps écoulé en secondes), `H1`, `H2`, `T1`, `T2`.

- **Provenance indiquée** : APMonitor, « TCLab D — Empirical Model Estimation » (protocole : https://apmonitor.com/do/index.php/Main/TCLabD ; mesures : https://apmonitor.com/do/uploads/Main/tclab_dyn_data2.txt).
- **Réserve de fidélité** : le fichier a été fourni par le mainteneur dans un classeur (« MKR_TCLab_Donnees_reelles.xlsx ») qui précise que le téléchargement direct était indisponible et que les valeurs sont une **transcription de l'extrait publié** ; MKR n'a pas pu les comparer au fichier source (hôte bloqué depuis l'environnement de développement). À recouper avec la source avant toute conclusion forte.
- **Licence** : non vérifiée ; il s'agit de mesures physiques de laboratoire, conservées avec leur attribution. À retirer si la source l'exige.
- **Commandes** : H1 0→80 % à 10 s, H2 0→100 % à 120 s, H1 80→20 % à 200 s, H1 20→70 % à 280 s, H2 100→10 % à 320 s, H1 70→50 % à 400 s, H2 10→80 % à 520 s.
- **Limites** : banc de laboratoire, pas une installation ; système à deux entrées couplées ; segments courts ; pas de 5 s ; aucune consigne (boucle ouverte).

Résultats de MKR sur ces données : [docs/validation/donnees-reelles-tclab.md](../../docs/validation/donnees-reelles-tclab.md).
