---
name: mkr-plc-readonly
description: Concevoir, tester ou auditer le futur connecteur d'acquisition PLC de MKR, particulièrement Schneider M340/M580 Modbus TCP en lecture seule.
---

# Acquisition industrielle en lecture seule — MKR

1. Lire `docs/communication/` et la matrice de qualification; aucune communication réelle sans banc autorisé.
2. Priorité de conception Schneider M340/M580 : Modbus TCP sous réserve du matériel, firmware, configuration et plan de mapping réels. OPC UA et autres protocoles sont des connecteurs complémentaires.
3. N'implémenter que les fonctions de lecture : coils, discrete inputs, input registers, holding registers. Interdire toute trame d'écriture dans le code et dans les tests.
4. Mapping explicite : zone, offset PDU, convention 0/1-base, type INT/UINT/REAL, nombre de registres, byte/word order, conversion, unité, signification métier et cadence.
5. Distinguer `CV` calculée et `MV` réellement appliquée; aucun nom de variable n'est une preuve de sémantique.
6. Horodatage source lorsqu'il existe, sinon horodatage client identifié comme tel; qualité, trous de communication, erreurs, latence, reconnexion et nombre de requêtes conservés.
7. Éviter la surcharge PLC : temporisations, regroupements raisonnables, plafonds d'acquisition et essais de charge sur simulateur et banc.
8. Modbus TCP classique ne garantit pas chiffrement/authentification : segmentation, filtrage, accès contrôlé et aucun port 502 exposé sans protection.
9. Tester séparément simulateur de protocole, banc matériel et site industriel; ne jamais annoncer « compatible » sans preuve constructeur et tests.

Livrable : table de mapping documentée, preuves de non-écriture et limites d'usage.
