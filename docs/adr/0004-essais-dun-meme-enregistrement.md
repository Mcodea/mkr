# ADR 0004 — Deux essais d'un même enregistrement

Statut : implémenté, à valider (revue humaine puis recette Windows).

## Contexte

La règle d'origine exigeait deux imports distincts pour l'estimation et la validation (`training_id <> validation_id`, octets différents). Un historique ou un enregistrement long contient pourtant plusieurs échelons de MV, dont un essai aller-retour (montée puis descente). Obliger l'utilisateur à découper le fichier à la main est source d'erreurs : fichiers modifiés, plages mal alignées.

## Décision

1. **Recherche d'essais (`find_steps`).** Le moteur parcourt l'enregistrement et repère les échelons de MV isolés, c'est-à-dire un changement de plus de 2 tolérances MV sur une seule mesure. Chaque fenêtre est validée par `fopdt.read_step`, le lecteur même des estimateurs : une fenêtre listée est donc acceptée par l'identification. Les échelons non exploitables sont listés avec leur motif (rampe, ligne de base instable, réponse trop courte, MV non constante, PV trop faible). Rien n'est lissé, comblé ni modifié.
2. **Règle d'indépendance (`TrialIndependence`).**
   - Deux imports aux mêmes octets restent refusés (`fit_duplicate_source`).
   - Un même import n'est accepté que si les deux plages sont explicites (`fit_same_source_window`) et ne partagent aucune ligne (`fit_same_source_overlap`).
   - Le moteur vérifie en plus que les intervalles de temps sont disjoints (`fit_independence`, inchangé).
3. **Plateau partagé.** Un essai aller-retour partage le plateau entre les deux échelons. La recherche coupe ce plateau : le second essai garde ses 4 à 8 dernières mesures de base et le premier s'arrête juste avant. Les deux fenêtres sont revalidées.
4. **Migration SQLite 9.** La contrainte `CHECK(training_id <> validation_id)` devient : sources distinctes, ou bien même source avec deux plages renseignées et différentes. La table est reconstruite selon la procédure officielle SQLite (clés étrangères désactivées, copie, remplacement, `foreign_key_check`). Les études et toutes les tables liées sont conservées ligne pour ligne.

## Alternatives écartées

- **Laisser l'utilisateur découper le fichier** : le fichier d'origine n'est alors plus la preuve, et les erreurs de plage ne sont pas détectables.
- **Autoriser le recouvrement du plateau** : la base commune de deux essais est le même échantillon. L'estimateur le refuse justement et la règle d'indépendance (AGENTS.md §6.3) ne doit pas être affaiblie.
- **Fenêtre glissante sans validation par l'estimateur** : le risque est de lister des essais que l'identification refuse ensuite.

## Conséquences et risques

- Deux échelons d'un même enregistrement partagent le capteur, la période d'échantillonnage et les conditions du procédé. C'est plus proche d'un essai répété le même jour que de deux essais sur des jours différents. La validation reste valable pour la linéarité et la reproductibilité sur ce banc, mais pas pour une dérive du procédé entre jours. L'écran le rappelle.
- Une plage rognée du plateau commun réduit un peu la fin de la réponse du premier essai ; l'indicateur « stabilisée » est recalculé.
- La recherche suppose un essai en **boucle ouverte**, régulateur en manuel. Elle ne détecte pas le mode automatique : l'utilisateur confirme la condition d'essai.

## Retour arrière

Restaurer la sauvegarde de la base projet avant la migration. Un MKR antérieur refuse `user_version = 9` au lieu de le lire de travers. Aucune donnée d'origine (CSV, empreintes) n'est touchée.
