# MKR-COM — Acquisition future

Le premier périmètre de qualification est Modbus TCP en lecture seule pour M340/M580. Avant essai sur équipement, établir mapping explicite, offsets, types, ordre des mots/octets, unités, qualité, source d'horodatage, limites de cadence, reconnexion et journalisation. Valider la limitation aux lectures, la charge et l'isolation réseau sur un banc identifié. Le premier connecteur et son périmètre de preuve sont décrits ci-dessous.

## Lecture Modbus TCP depuis un projet

Ouvrir **Données CSV → Acquisition Modbus** et renseigner la cible du banc,
l’identifiant d’unité, l’offset PDU base zéro, l’ordre des mots et les unités.
Le premier adaptateur lit trois REAL32 consécutifs dans l’ordre SP, PV, MV
en une requête FC03 (six registres). Chaque registre transporte son octet fort
en premier. Le choix d’ordre concerne les deux mots de chaque REAL32.
Référence de protocole : Modbus Application Protocol V1.1b3, §6.3.

La pause configurée commence après réception. Les échantillons sont horodatés
en UTC par le client. Un délai dépassé, une réponse incohérente ou une perte de
liaison interrompt la session. L’utilisateur peut conserver le préfixe acquis
avec son motif d’arrêt. Le CSV, son empreinte, le mapping et les unités sont
archivés dans le projet. Le test Windows utilise un serveur sur 127.0.0.1.
La compatibilité avec une configuration de CPU réelle reste à qualifier.
