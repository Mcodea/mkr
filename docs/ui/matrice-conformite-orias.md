# Matrice de conformité — finalisation UI/UX et identité ORIAS

Statuts : Conforme · Partiellement conforme · Non conforme · Non vérifié · Bloqué. Une fonction présente dans le code n'est « conforme » qu'avec une preuve
(capture réelle de la CI Windows ou test exécuté).

**Preuves.** Run CI 322 (commit 2b8aac5) et run CI 323 (commit ffe92cd, même code d'application : seul le script de capture diffère), tous deux entièrement verts :
compilation, tests Python, SQLite, acquisition Modbus/S7 en lecture seule, transfert PI, historique, intégrateur, contrôles d'interaction des graphiques (clair et sombre),
parité des textes fr/en, 28 captures d'écrans + échelles + démarrage + anglais. Paquet Windows : artefact `MKR-Windows-x64` du run 323
(sha256 865421492d5fd0632a9b55efc44c034d6fc196ee9130fffd03af67570b60d82f), https://github.com/Mcodea/mkr/actions/runs/45877698889.
Données montrées : synthétiques (jeu de test déterministe), jamais présentées comme physiques. Les notes UI/UX restent à attribuer par la réception humaine ;
aucune note n'est attribuée ici.

| Exigence | Écran / composant | Avant | Modification | Commit | Preuve | État |
|---|---|---|---|---|---|---|
| Nom commercial ORIAS, nom technique MKR conservé | produit | titre « MKR » | `ProductIdentity` (source unique), titres de fenêtres ciblés ; dépôt Mcodea/mkr, namespaces, assemblies, dossier de données, formats inchangés | 5887d01 | captures (titres « ORIAS — … »), tests UIA passés | Partiellement conforme : les messages internes des moteurs disant « MKR » ne sont pas repris |
| Logo clair / sombre / symbole / monochrome / ICO 16–256 | ressources | aucun | `src/Mkr.Desktop/Assets`, fond transparent, variante sombre | 5887d01 | logo visible en en-tête, accueil, À propos, démarrage, clair et sombre | Conforme (icône en barre des tâches / menu Démarrer : non inspectée ; pas d'installateur) |
| Logo compact en en-tête, sans slogan | en-tête | texte MKR | image du thème | 5887d01 | captures 21-* | Conforme |
| Accueil : logo + slogan | accueil | « ACCUEIL » | logo + slogan | 5887d01 | captures 01 | Conforme |
| Écran de démarrage (logo, slogan, version réelle, état réel) | démarrage | aucun | `StartupWindow`, attente sans bloquer la boucle de messages | 12911a9 | capture 00-demarrage : version « 1.0.0 · compilation <révision> », étape réelle | Conforme |
| Fenêtre À propos | À propos | aucune | `AboutWindow`, menu Aide | 5887d01 | captures 22-a-propos (fr), 24-english-about (en) | Conforme (éditeur et mentions légales volontairement non renseignés) |
| Logo sur la fiche PDF | PDF | texte MKR | logo monochrome | 5887d01 | test de lecture PDF passé ; artefact `MKR-exemple-fiche-PDF` | Partiellement conforme : rendu du logo non relu visuellement |
| Acquisition : tendance centrale, synthèse, signaux | Acquisition | zone vide | `TrendReader`, onglets, état et synthèse, choix des signaux | 955fe25 | capture 21-1 : tendances réelles d'un essai (mesure, commande) | Partiellement conforme : états de la fenêtre d'acquisition directe (aucun, chargement, en cours, terminé, déconnexion, erreur) implémentés mais non capturés (nécessite un banc) ; consigne SP absente de cet essai |
| Diagnostic : graphiques, anomalies, verdict | Diagnostic | tableau seul | graphiques, anomalies à leur instant, verdict reprenant les constats existants | 955fe25 | capture 21-2 | Partiellement conforme : aucun seuil ajouté ; l'essai ne comporte pas d'anomalie à localiser |
| Simulation : une ligne de commandes, graphiques et résultats sans défilement à 1366×768 | Simulation | défilement, textes | en-tête compact, exports en panneau, légende et détails repliés | 17cc11a | capture 21-5 1366×768 : trois pistes et tableau de comparaison visibles | Conforme |
| Bouton Suivant cohérent | barre d'état | « Réglage PID » partout | étape suivante du parcours, désactivée avec raison, absente sur Rapport | 955fe25 | captures 21-1 à 21-7 | Conforme |
| Identification / Réglage PID : finitions | écrans | validés | non refondus | — | captures 21-3, 21-4 | Partiellement conforme (réserves du tableau `conformite-identification.md`) |
| Validation / Rapport | écrans | — | contraste sombre corrigé | a03afde, 12911a9 | captures 21-6, 21-7 | Partiellement conforme |
| Graphiques : zoom, déplacement, ajuster, curseur synchronisé, A–B | graphiques | — | retour exact à la vue complète | f8de513 | tests UIA exécutés, clair et sombre | Conforme (la touche de déplacement est parfois perdue par l'automate de test, rejouée) |
| Export CSV / PNG des graphiques | graphiques | — | dossier de sortie imposable pour les tests (`MKR_EXPORT_DIRECTORY`) | cce3091 | CSV de 113 297 caractères relu en UTF-8 ; PNG valide, clair et sombre | Conforme pour l'écriture ; la boîte système « Enregistrer sous » n'est pas automatisée (Non vérifié) |
| Contraste thèmes clair / sombre | tous | texte des panneaux illisible en sombre | style commun des panneaux | 12911a9 | captures sombres 21-2, 21-6 | Conforme sur les écrans capturés ; secondaires non tous recapturés |
| Échelles Windows 100 / 125 / 150 % | Acquisition, Identification, Simulation | — | script de capture sensible au DPI | ffe92cd | DPI mesurés 96/120/144 ; captures complètes 1920×1032 / 1920×1020 / 1920×1008, barre d'état et panneaux visibles ; 1366×768 : 100 et 125 % (150 % non proposé par Windows) | Conforme pour ces trois écrans ; autres écrans non capturés aux échelles |
| Navigation clavier, ordre de tabulation, lecteurs d'écran | tous | — | noms accessibles, menus à mnémoniques | — | tests UIA de recherche par nom | Non vérifié (pas de test d'ordre de tabulation) |
| Internationalisation fr/en | chrome | textes en dur | `Loc`, `LocExtension`, menu Langue, test de parité | 12911a9, 17cc11a | parité testée ; captures 24-english-* : navigation, bouton « Next », À propos | Partiellement conforme (run 327) : menus, formulaires, navigation, états, Simulation, À propos, démarrage en anglais ; restes constatés en anglais : nom du projet saisi, séries des graphiques (« Consigne »), phrases issues de `Mkr.Core` (marges de la boucle, en-tête d'essai de l'Identification, colonne « Méthode »), messages de validation et PDF. Slogan volontairement non traduit |
| Moteur scientifique préservé | moteurs | — | aucun fichier de calcul modifié (Desktop, StudySheet, Localization, tests, docs) | — | diffs vérifiables | Conforme |
| Automates en lecture seule | acquisition | — | inchangé | — | tests d'acquisition Modbus/S7 passés | Conforme |
| Écrans secondaires harmonisés (projets, catalogue, paramètres, historique, audit) | secondaires | — | thème et titres | — | captures 01, 03 | Partiellement conforme |
| Installateur | — | — | — | — | — | Non conforme : pas d'installateur (paquet portable) |

## Run 327 (commit 2059d3d) — anglais, états d'acquisition, écrans secondaires

Tous les tests passent (Python 318, smoke .NET, SQLite, Modbus/S7, tests graphiques, 12 contrôles de graphiques). 73 captures décodées ; aucune capture en échec. Le journal restitué par l'API est tronqué en son début : les captures sombres sont complètes, les claires partielles (21-5 à 24).

- Anglais complet : rapport `##FRENCH` — 6 textes français restants en mode anglais (slogan voulu, valeurs de données, phrases de `Mkr.Core`). Statut : **Partiellement conforme**.
- États d'acquisition directe : code et textes fr/en livrés ; non capturés faute de banc. Statut : **Non vérifié**.
- Écrans secondaires : fenêtre d'acquisition, catalogue, revue, audit, prototype PID capturés en thème sombre, lisibles ; revue visuelle détaillée non faite. Statut : **Partiellement conforme**.
