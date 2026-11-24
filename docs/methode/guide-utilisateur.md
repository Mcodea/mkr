# MKR — Guide de l'utilisateur : de l'essai au réglage

Ce guide suit la chaîne complète, avec les trois façons d'obtenir des données. Il dit aussi ce que MKR **ne fait pas**.
Tout réglage produit reste une **proposition technique** à valider en simulation, puis sur banc. MKR n'écrit jamais dans l'automate.

## 1. Choisir le mode de données

| Mode | Quand | Comment |
|---|---|---|
| **Importer des données déjà enregistrées** | Un historien ou un fichier existe | *Données CSV* → choisir le fichier. L'original et son empreinte sont conservés. Un historique long peut contenir plusieurs essais : voir §3. |
| **Acquisition en direct** | Vous faites l'essai maintenant, sur un banc autorisé | *Acquisition automate…* : Schneider (Modbus TCP) ou Siemens S7-1200/1500 (S7comm). Lecture seule, avec « Tester la lecture » avant l'essai. |
| **Fabriquer un historique** | Vous voulez enregistrer longtemps (heures ou jours) et chercher les essais après coup | *Acquisition automate…* → « Enregistrement continu ». Les segments sont enregistrés au fil de l'eau. |

Les trois modes aboutissent au même objet : des enregistrements horodatés MV/PV, avec leur provenance. Le moteur d'identification ne sait pas d'où ils viennent.

## 2. Faire un bon essai (ce que MKR exige, et pourquoi)

**Principe : un échelon de MV, régulateur en manuel, procédé stable.**

1. **Stabiliser** le procédé : PV constante (dans votre tolérance) pendant au moins quelques constantes de temps. MKR exige au moins 4 mesures stables avant l'échelon.
2. **Passer en manuel** et noter la MV. Un essai en boucle fermée n'est pas exploitable : le régulateur masque la dynamique du procédé. MKR **ne détecte pas** le mode de l'automate. C'est vous qui confirmez la case « boucle ouverte ».
3. **Amplitude** : assez grande pour dominer le bruit (MKR exige une réponse supérieure à 4 fois la tolérance PV), assez petite pour rester près du point de fonctionnement (5 à 10 % de l'étendue de MV en général). Évitez la saturation et, sur une vanne, la zone morte : un échelon plus petit que le jeu de la vanne n'agit pas.
4. **Durée** : au moins le retard plus 4 à 5 constantes de temps après l'échelon. Un essai trop court est signalé (`short_horizon`).
5. **Cadence** : au moins 20 mesures sur la montée, et une période inférieure au quart du retard si possible. Un échelon qui tombe entre deux mesures rend le retard incertain d'une période : MKR affiche l'intervalle.
6. **Deuxième essai, indépendant**, de préférence **dans l'autre sens** et d'amplitude comparable (rapport entre 0,5 et 2). Il sert de validation : MKR ne l'utilise jamais pour ajuster le modèle. Un essai aller-retour (montée puis descente) convient.
7. **Ne rien changer d'autre** : pas de perturbation de charge, pas de changement de consigne d'une boucle voisine.

## 3. Retrouver les essais dans un historique

Dans *Identification*, choisissez l'enregistrement et ses colonnes, renseignez les unités et les tolérances, puis « Rechercher les essais exploitables ».
- MKR liste les échelons que l'estimateur accepte, avec les lignes exactes du fichier d'origine. Il liste aussi ceux qu'il **écarte** et pourquoi.
- Il propose des paires, avec les sens opposés et les réponses stabilisées d'abord. Choisissez-en une : les deux plages sont renseignées pour l'estimation et la validation.
- Rien n'est lissé ni comblé : MKR ne fait que repérer des lignes.

## 4. Identifier et valider

1. **Critères** : cliquez « Proposer les critères ». Ils sont calculés à partir du bruit mesuré avant l'échelon, avant tout ajustement. Vous pouvez les durcir, pas les choisir après avoir vu le résultat.
2. **Comparer FOPDT et SOPDT** : MKR garde le premier ordre sauf si le second ordre prédit l'essai de validation nettement mieux.
3. **Décision** : si le modèle est refusé, l'étude est conservée mais aucun réglage ne peut en être déduit. La raison est affichée (erreur de validation, corrélation des résidus, essai trop court, paramètre en borne).
4. **Lire le retard comme un intervalle** : θ est compris entre la valeur estimée et la valeur estimée augmentée de l'intervalle d'échantillonnage.

## 5. Régler et simuler

- **Réglages PI** (Lambda et SIMC) sur FOPDT, **PID SIMC** sur SOPDT. La cible rapide proposée (τc = retard) utilise la borne prudente du retard. Choisissez une cible plus lente si l'effort de commande ou la robustesse l'exige.
- **Lisez les marges** affichées avec chaque réglage : marge de phase, marge de gain, marge de retard et Ms. Les avertissements apparaissent en dessous de 30° de phase, de 2 de gain, ou au-dessus de 2 de Ms. Une cible plus rapide que le retard les dégrade vite. Ce sont des repères, pas des seuils d'acceptation.
- **Simulez avant tout** : saturation, vitesse de MV, perturbation, bruit de mesure, passage manuel → automatique, sensibilité aux erreurs de modèle (±gain, ±τ, ±θ).
- Un meilleur indicateur en simulation n'est **pas** une garantie de stabilité ni de compatibilité avec le bloc de l'automate.

## 6. Passer au bloc de l'automate

MKR calcule des coefficients **génériques** (forme idéale, e = SP − PV, unités physiques). Les blocs Schneider (PIDFF, PID_INT) et Siemens (PID_Compact) n'ont pas tous la même structure, ni les mêmes unités ni la même normalisation.
- **État actuel : aucune conversion n'est qualifiée.** MKR n'exporte aucun paramètre « prêt à charger ». L'écran de la fiche de bloc et le PDF le disent.
- Pour chaque bloc, il faut d'abord relever dans la documentation officielle l'équation, la forme, les unités et la normalisation de la version utilisée, puis la qualifier sur banc avec le protocole `docs/validation/qualification-bloc-banc.md`.
- **Signe du gain :** MKR donne un Kp signé (négatif si le gain du procédé l'est). Le PIDFF refuse un Kp négatif (statut 080, pas de sortie) et PID_Compact n'accepte pas un gain négatif : il faut |Kp| et l'inversion du sens d'action (`rev_dir`, « Invert control logic »). Sources et niveau de preuve : catalogue des blocs.
- **Unités du temps :** PID_Compact attend Ti et Td en secondes. Pour un bloc dont le temps est un entier converti en durée IEC, vérifiez l'unité de cet entier.
- Tant que ce n'est pas fait, la saisie dans le bloc est manuelle, sous votre responsabilité, avec des essais en manuel puis en automatique à faible amplitude.

## 7. Ce que MKR ne fait pas (aujourd'hui)

- **Identification en boucle fermée** et à partir de données d'exploitation « sales » : un régulateur actif masque le procédé.
- **Lecture du mode** (manuel ou automatique) et de la consigne de l'automate pour contrôler qu'un essai est bien en boucle ouverte.
- **Procédés intégrateurs** avec un régulateur : l'identification intégrateur existe, mais le réglage PI/PID n'est calculé que pour le premier et le second ordre.
- **Garantie de stabilité sur le procédé réel** : MKR calcule les marges de gain, de phase, de retard et le pic de sensibilité Ms du modèle **nominal**, avec leurs avertissements, et fait varier gain, τ et θ dans l'analyse de sensibilité. Ni l'un ni l'autre ne garantit la stabilité du procédé réel.
- **Conversion qualifiée** vers PIDFF, PID_INT et PID_Compact.
