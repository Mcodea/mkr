# Matrice de conformité — finalisation UI/UX et identité ORIAS

Statuts : Conforme · Partiellement conforme · Non conforme · Non vérifié · Bloqué. Une fonction présente dans le code n'est « conforme » qu'avec une preuve
(capture réelle de la CI Windows ou test exécuté). Preuves ci-dessous : run CI 318 (commit bdedc4b), entièrement vert : compilation, tests Python, SQLite,
acquisition Modbus/S7, transfert PI, historique, intégrateur, contrôles d'interaction des graphiques, 28 captures d'écran produites. Les notes UI/UX restent à
attribuer par la réception humaine ; aucune note n'est attribuée ici. Données montrées : synthétiques (jeu de test déterministe), jamais présentées comme physiques.

| Exigence | Écran / composant | Avant | Modification | Commit | Preuve | État |
|---|---|---|---|---|---|---|
| Nom commercial ORIAS, nom technique MKR conservé | produit | titre « MKR » | `ProductIdentity` (source unique), titres de fenêtres ciblés ; dépôt, namespaces, assemblies, dossier de données, formats inchangés | 5887d01 | captures (titres « ORIAS — … »), tests UIA passés avec les nouveaux titres | Partiellement conforme : les messages internes des moteurs qui disent « MKR » ne sont pas repris |
| Logo clair / sombre / symbole / monochrome / ICO 16–256 | ressources | aucun | `src/Mkr.Desktop/Assets` ; fond transparent (alpha vérifié) ; variante sombre | 5887d01 | logo visible en en-tête et accueil, thèmes clair et sombre | Conforme (ICO : dans l'exécutable, non inspecté en barre des tâches / menu Démarrer) |
| Logo compact en en-tête, sans slogan | en-tête | texte MKR | image du thème | 5887d01 | captures 21-*, 01 | Conforme |
| Accueil : logo + slogan | accueil | « ACCUEIL » | logo + slogan | 5887d01 | capture 01-espace-projets | Conforme |
| Écran de démarrage | démarrage | aucun | `StartupWindow` (état réel, barre indéterminée, sans délai) | 5887d01 | capture 00 : échec du pilote de capture (fenêtre de 4 s non saisie) | Non vérifié |
| Fenêtre À propos | À propos | aucune | `AboutWindow`, menu Aide | 5887d01 | capture 22-a-propos | Conforme (éditeur/mentions légales volontairement non renseignés) |
| Icône Windows, métadonnées produit | exécutable | sans | `ApplicationIcon`, `Product`, `Description` | 5887d01 | — | Non vérifié (barre des tâches, menu Démarrer, installateur : non testés ; pas d'installateur) |
| Logo sur la fiche PDF | PDF | texte MKR | logo monochrome + titre ORIAS | 5887d01 | test de lecture PDF de la CI passé ; rendu du logo non relu | Partiellement conforme |
| Acquisition : tendance centrale, synthèse, signaux | Acquisition | zone vide | `TrendReader`, onglets Tendances / Données brutes, état et synthèse, choix des signaux | 955fe25 | capture 21-1 : tendances SP/PV/MV réelles du fichier ouvert | Partiellement conforme : l'import présenté est court (5 lignes), tendances plates ; états « acquisition en cours / déconnexion » de la fenêtre d'acquisition directe non repris |
| Diagnostic : graphiques et verdict | Diagnostic | tableau seul | graphiques, anomalies repérées à leur instant, verdict reprenant les constats existants | 955fe25 | capture 21-2 | Partiellement conforme (même jeu court ; aucun seuil ajouté) |
| Simulation : encombrement réduit | Simulation | défilement, textes | légende et détails repliés, hauteur minimale 520 px | 955fe25 | capture 21-5 | Partiellement conforme : à 1366×768 la piste de commande est coupée, léger défilement |
| Bouton Suivant cohérent | barre d'état | « Réglage PID » partout | étape suivante du parcours, désactivée avec raison, absente sur Rapport | 955fe25 | captures 21-1…21-7 : Diagnostic, Identification, Validation, Réglage PID, Simulation, Rapport | Conforme |
| Identification / Réglage PID : finitions | écrans | validés | non refondus | — | captures 21-3, 21-4 | Partiellement conforme |
| Graphiques : zoom, déplacement, ajuster, curseur synchronisé, A–B | graphiques | — | retour exact à la vue complète | f8de513 | tests UIA exécutés, clair et sombre | Conforme |
| Export CSV / PNG des graphiques | graphiques | — | chemin de sortie imposable pour les tests (`MKR_EXPORT_DIRECTORY`) | cce3091 | CSV de 113 297 caractères relu en UTF-8 ; PNG valide, clair et sombre | Conforme pour l'écriture des fichiers ; la boîte système « Enregistrer sous » n'est pas automatisée (Non vérifié) |
| Thèmes clair / sombre, contrastes | tous | — | détails de simulation lisibles en sombre | a03afde | captures clair/sombre | Partiellement conforme |
| Échelles Windows 125 % / 150 % | tous | — | — | — | — | Non vérifié (non testable avec le mécanisme de capture) |
| Textes en ressources de traduction | tous | — | — | — | — | Non conforme : reporté à la phase suivante |
| Moteur scientifique préservé | moteurs | — | aucun fichier de calcul modifié (Desktop, StudySheet, tests, docs) | — | diffs | Conforme (vérifiable dans les diffs) |
| Automates en lecture seule | acquisition | — | inchangé | — | tests d'acquisition Modbus/S7 passés | Conforme |
