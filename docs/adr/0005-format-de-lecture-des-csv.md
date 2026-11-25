# ADR 0005 — Format de lecture des CSV : décimale, date et heure locales

Statut : implémenté, à valider (revue humaine, puis recette Windows).

## Contexte

Audit NC-16 : les exports d'historiens français sont refusés. Ils utilisent le point-virgule, la virgule décimale et une date et heure **locales sans décalage** (`01/07/2026 08:00:00`). MKR n'acceptait que des nombres à point décimal et des horodatages ISO 8601 avec décalage UTC.

## Décision

1. **Format déclaré, conservé avec l'import, jamais deviné.** Le format comprend la décimale (point ou virgule) et, pour une heure locale, un motif parmi une liste fermée et le **fuseau IANA**. Il est enregistré dans les métadonnées de l'import et appliqué à chaque lecture : qualité, aperçu, recherche d'essais et identification. Les octets d'origine et leur empreinte ne changent pas.
2. **Proposition, jamais application.** À l'inspection, MKR propose un format d'après un échantillon réparti sur tout le fichier : une proposition qui ne regarderait que les premières lignes ne verrait pas une virgule décimale cachée derrière un plateau d'entiers. L'utilisateur confirme ou corrige.
3. **Heures locales converties avec la base IANA.** Une heure qui **n'existe pas** (saut de printemps) ou qui est **ambiguë** (retour d'automne) est refusée avec son motif, jamais placée au hasard. Un fichier qui traverse un changement d'heure garde donc une période uniforme.
4. **Cohérence des séparateurs.** Une virgule décimale exige un séparateur de champs autre que la virgule.
5. **Motifs en liste fermée.** Un motif `strptime` libre pourrait cacher une inversion jour/mois. Les motifs sont : `jj/mm/aaaa`, `jj.mm.aaaa` et `aaaa-mm-jj`, avec heure complète, avec ou sans fractions de seconde.
6. **Dépendance `tzdata` 2025.2** épinglée dans `engine/requirements.txt`.

## Dépendance : justification

- **Besoin :** `zoneinfo` lit la base des fuseaux du système. Windows n'en a pas. Sans `tzdata`, tout fuseau autre que UTC est impossible sur le poste cible.
- **Nature :** données pures, sans code natif, roue universelle, licence Apache-2.0. Elle est embarquée dans le paquet, dont la vérification contrôle que `Europe/Paris` se résout.
- **Alternatives écartées :**
  - *décalage fixe déclaré* : faux dès qu'un historique traverse un changement d'heure ;
  - *pytz* : dépendance supplémentaire à l'API plus piégeuse ;
  - *détection silencieuse du fuseau* : impossible à partir des seules valeurs.

## Conséquences et risques

- **Fuseau déclaré par l'utilisateur.** Un mauvais fuseau décale les instants absolus mais pas les écarts, sauf à un changement d'heure. Un fuseau erroné qui traverse un changement d'heure s'y voit : période non uniforme ou horodatages refusés.
- **Enregistreurs qui ne suivent pas l'heure d'été** (heure locale fixe toute l'année) : à déclarer en UTC+fixe. Les noms `Etc/GMT-1`, au sens inversé, ne sont pas proposés.
- **Règles futures :** la base est épinglée et sa version est dans les exigences. Une règle modifiée par la loi demande une mise à jour de `tzdata`.
- **Compatibilité :** les imports antérieurs n'ont pas de format et sont lus comme avant. Le champ est facultatif dans le contrat IPC et dans SQLite (aucune migration).

## Retour arrière

Retirer le champ `format` des requêtes : les imports enregistrés avec un format ne seraient alors plus lisibles par l'ancien moteur, qui les refuserait explicitement. Leurs octets d'origine restent intacts.
