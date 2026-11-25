# ADR 0003 — Acquisition Schneider et Siemens en lecture seule

Statut : implémenté, à valider (revue humaine, puis qualification sur banc).

## Contexte

Décision du mainteneur (23/11/2026) : seuls Schneider Electric et Siemens sont pris en compte, avec Modbus TCP pour Schneider et un accès par le réseau PROFINET pour Siemens. Le premier connecteur lisait trois REAL32 contigus, sans reprise sur perte ni qualité par mesure (audit du 23/11, NC-05 à NC-07).

## Décision

1. **Schneider** : Modbus TCP, fonctions 03 et 04 uniquement.
2. **Siemens** : S7comm sur ISO-on-TCP (RFC 1006, port 102), limité à la négociation (0xF0) et à la lecture (0x04), avec une connexion de type PG (TSAP distant 0x01, puis rack × 32 + emplacement).
3. Le client S7 est écrit pour MKR, sans bibliothèque tierce. Il ne contient aucune fonction d’écriture, ce qui rend la lecture seule vérifiable par lecture du code. Les bibliothèques S7netplus et Snap7 contiennent des écritures. La pile reste minimale : environ 200 lignes, auditées.
4. Un modèle commun, `AcquisitionTarget`, décrit chaque signal par une adresse, un type, un ordre d’octets et une mise à l’échelle. Une boucle commune, `PlcAcquisition`, gère le calendrier, les lacunes, la reconnexion, la progression et les statistiques. Chaque protocole n’implémente que `IPlcReader`.
5. L’interopérabilité est démontrée contre des implémentations indépendantes, utilisées en test uniquement et non distribuées : pymodbus (BSD), et la bibliothèque Snap7 (LGPL-3.0) via python-snap7 (MIT).

## Alternatives écartées

- **PROFINET IO** : il exige de déclarer le PC comme équipement dans le projet TIA Portal, ce qui modifie la configuration de l’automate. C’est contraire à AGENTS.md §4.
- **OPC UA** : c’est l’accès le plus sûr, avec certificats et droits en lecture seule. Mais il faut une licence d’exécution sur les S7-1500, et la pile .NET de l’OPC Foundation est sous double licence GPL-2.0/RCL. Il est à réexaminer si un site interdit PUT/GET.
- **Modbus TCP sur Siemens (MB_SERVER)** : il faut modifier le programme de chaque automate.

## Conséquences et risques

- PUT/GET affaiblit la protection de la CPU. Il ne faut l’activer que sur un banc, puis le désactiver (procédure dans `docs/communication/acquisition.md`).
- Les DB optimisées, dont les DB d’instance de PID_Compact, ne sont pas lisibles. Il faut copier les valeurs dans une DB non optimisée.
- S7comm n’est pas une spécification publiée par Siemens. Le client suit le format observé par Snap7 et par le dissecteur Wireshark. Sa compatibilité avec chaque CPU et chaque firmware reste à qualifier sur banc.
- Le format de provenance est étendu. Les anciens enregistrements Modbus restent relisibles (`AcquisitionOrigin.Source`).

## Retour arrière

Revenir au commit parent de ce lot. Les acquisitions enregistrées avec le nouveau format ne seraient alors plus décrites complètement, mais leurs CSV et empreintes restent intacts.
