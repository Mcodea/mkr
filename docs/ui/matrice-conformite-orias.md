# Matrice de conformité — finalisation UI/UX et identité ORIAS

Statuts : Conforme · Partiellement conforme · Non conforme · Non vérifié · Bloqué. Une fonction présente dans le code n'est « conforme » qu'avec une preuve
(capture réelle de la CI Windows ou test exécuté). Au moment de la rédaction, les changements des commits 5887d01 à 65266d3 n'ont pas encore de preuve visuelle :
ils sont **Non vérifié** jusqu'à lecture des captures du run correspondant. Les notes UI/UX restent à attribuer par la réception humaine.

| Exigence | Écran / composant | Avant | Modification | Commit | Preuve | État |
|---|---|---|---|---|---|---|
| Nom commercial ORIAS, nom technique MKR conservé | produit | titre « MKR » | `ProductIdentity` (source unique), titres de fenêtres ciblés, dépôt/namespaces/dossier de données inchangés | 5887d01 | diff (aucun renommage technique) | Partiellement conforme : messages internes des moteurs disant « MKR » non repris |
| Logo clair / sombre / symbole / monochrome / ICO 16–256 | ressources | aucun | `src/Mkr.Desktop/Assets`, fond transparent vérifié (alpha), variante sombre à mot-symbole clair | 5887d01 | aperçus fond clair/sombre produits, captures d'application à lire | Non vérifié (application) |
| Logo compact dans l'en-tête, sans slogan | en-tête | texte MKR | image du thème | 5887d01 | — | Non vérifié |
| Accueil : logo + slogan | accueil | « ACCUEIL » | logo + slogan | 5887d01 | — | Non vérifié |
| Écran de démarrage (logo, slogan, version réelle, état réel, sans progression fictive) | démarrage | aucun | `StartupWindow`, étapes réelles, barre indéterminée | 5887d01 | capture 00-demarrage attendue | Non vérifié |
| Fenêtre À propos (signification, slogan, version, limites, tiers, pas de société inventée) | À propos | aucune | `AboutWindow`, menu Aide | 5887d01 | capture 22-a-propos attendue | Non vérifié |
| Icône Windows, métadonnées produit | exécutable | sans | `ApplicationIcon`, `Product`, `Description` (nom d'assembly inchangé) | 5887d01 | — | Non vérifié (barre des tâches, Démarrer, installateur : non testés) |
| Logo sur les rapports PDF | fiche PDF | texte MKR | logo monochrome + titre ORIAS | 5887d01 | PDF d'exemple de la CI | Non vérifié |
| Acquisition : tendance centrale, synthèse, états | Acquisition | zone vide | `TrendReader`, onglets Tendances / Données brutes, panneau d'état et de synthèse, choix des signaux | 955fe25 | capture 21-1 avec données attendue | Non vérifié. Limites : SP/PV/MV réels seulement si présents dans le fichier ; états « acquisition en cours / déconnexion » de la fenêtre d'acquisition en direct non repris |
| Diagnostic : graphiques et verdict | Diagnostic | tableau seul | graphiques des signaux, anomalies repérées à leur instant, verdict qui reprend les constats existants | 955fe25 | capture 21-2 attendue | Non vérifié. Aucun seuil ajouté |
| Simulation : encombrement réduit, pas de défilement à 1366×768 | Simulation | défilement | légende et détails repliés, hauteur minimale 520 px | 955fe25 | capture 21-5 attendue | Non vérifié |
| Bouton Suivant cohérent avec l'écran | barre d'état | « Réglage PID » partout | étape suivante du parcours, désactivée avec raison, absente sur Rapport | 1e (voir historique) | capture 21-4 attendue | Non vérifié |
| Identification / Réglage PID : finitions | écrans | validés | non refondus | — | captures du run 308 | Partiellement conforme (réserves précédentes) |
| Graphiques : zoom, déplacement, ajuster, curseur, A–B | graphiques | — | corrigés (retour exact à la vue complète) | 6e0…/f8de513 | tests UIA exécutés en CI | Conforme (déplacement par flèche : touche parfois perdue par l'automate) |
| Export CSV / PNG | graphiques | — | dossier de test `MKR_EXPORT_DIRECTORY` | cce3091 | écriture à relire | Non vérifié |
| Thèmes clair / sombre, contrastes | tous | — | détails Simulation lisibles en sombre | a03afde | captures run 308 | Partiellement conforme |
| Échelles Windows 125 % / 150 % | tous | — | — | — | — | Non vérifié (non automatisable avec le mécanisme de capture) |
| Internationalisation (ressources de traduction) | tous | — | — | — | — | Non conforme : reporté, centralisation des textes non démarrée |
| Moteur scientifique préservé | moteurs | — | aucun fichier de calcul modifié (fichiers touchés : Desktop, Infrastructure/StudySheet, tests, docs) | — | diffs | Conforme (vérifiable dans les diffs) |
| Lecture seule des automates | acquisition | — | inchangé | — | tests d'acquisition existants | Conforme |
