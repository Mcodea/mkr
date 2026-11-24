# MKR-COM — Acquisition sur banc en lecture seule

Périmètre décidé par le mainteneur du projet le 23/11/2026 : **Schneider Electric par Modbus TCP**
et **Siemens S7-1200/1500 par S7comm** (ISO-on-TCP, port 102). Aucun autre constructeur n’est pris en charge.
PROFINET IO n’est pas utilisé : un PC ne peut y lire des variables qu’en étant déclaré dans le projet TIA Portal,
ce qui modifie la configuration de l’automate. S7comm emprunte le même port Ethernet PROFINET de la CPU.
Décision et alternatives : [ADR 0003](../adr/0003-acquisition-automates.md).

## Utilisation

Ouvrir **Données CSV → Acquisition automate…**, choisir le constructeur, puis renseigner :

| Champ | Schneider · Modbus TCP | Siemens · S7comm |
|---|---|---|
| Port | 502 | 102 |
| Identification | Unité Modbus (souvent 1 ou 255 sur M340/M580) | Rack et emplacement (S7-1200/1500 : 0 et 1) |
| Zone | Registres de maintien FC03 (%MW) ou d’entrée FC04 | Bloc de données (DB n) ou mémentos (M) |
| Adresse d’un signal | Offset de registre en base 0 : %MW100 → 100, Modicon 400101 → 100 | Offset d’octet : DB10.DBD4 → 4, MW20 → 20 |
| Ordre des octets | ABCD, CDAB, BADC ou DCBA, à choisir explicitement | Toujours ABCD (big-endian S7) |

Chaque signal (SP, PV, MV) a son adresse, son type (INT16, UINT16, INT32, UINT32, REAL32) et une
mise à l’échelle linéaire : `physique = Pbas + (brut − Bbas) × (Phaut − Pbas) / (Bhaut − Bbas)`.
Exemples : entrée analogique Siemens 0–27648 → 0–100 % ; Schneider 0–10000 → 0–100 %.
Laisser 0 / 1 / 0 / 1 pour un REAL déjà en unité physique.

**Toujours faire « Tester la lecture » avant l’essai** : l’écran affiche les valeurs brutes et physiques,
à comparer aux tables d’animation de Control Expert ou de TIA Portal. Un mauvais offset ou un mauvais
ordre d’octets produit des valeurs plausibles mais fausses : seule cette comparaison le détecte.

### Prérequis Siemens

- *Protection & Security* de la CPU : cocher **Autoriser l’accès PUT/GET par un partenaire distant**.
  Ce réglage affaiblit la protection de la CPU : le limiter au banc, et le désactiver après l’essai.
- Les valeurs doivent être dans une DB à **accès non optimisé** (adresses absolues).
  Les DB d’instance de PID_Compact sont optimisées : copier SP, PV et MV dans une DB dédiée.
- Sans PUT/GET, MKR affiche l’erreur 0x8104 et l’explication. Une DB optimisée donne le code 0x03.

## Garanties en lecture seule (COM-002)

- Modbus : seules les fonctions **0x03** et **0x04** sont construites (`ModbusTcpReader.EmittedFunctionCodes`).
- S7 : seuls la demande de connexion COTP, la négociation **0xF0** et la lecture **0x04** sont construites
  (`S7Reader.EmittedFunctions`). Aucune écriture (0x05), commande CPU, chargement ou fonction de bloc n’existe dans le code.
- Les tests vérifient chaque trame reçue par les serveurs simulés. La preuve sur banc reste à faire
  par capture réseau (Wireshark) sur l’équipement identifié.
- Charge : au plus une lecture par période, avec une période d’au moins 100 ms. Modbus regroupe les trois signaux
  en une requête s’ils tiennent dans 125 registres, sinon une requête par signal. S7 lit les trois signaux en un seul job.

## Cadence, horodatage et pertes (COM-004)

- La période est un **calendrier** sur horloge monotone (mesure k à k × période), pas une pause après chaque réponse.
- L’horodatage UTC est le **milieu de l’aller-retour** sur le PC. L’aller-retour de chaque mesure est archivé (`rtt_ms`).
- Une mesure impossible (délai de 5 s dépassé, liaison coupée, exception Modbus 04/06/0A/0B, valeur non finie,
  créneau dépassé) est une **mesure manquée**. Les manques consécutifs forment une **lacune** horodatée avec sa cause.
  Les lacunes ne sont jamais interpolées.
- Après une perte de liaison, MKR se reconnecte au créneau suivant. Au-delà de la **coupure tolérée** (10 s par défaut),
  l’acquisition s’arrête avec le motif `connection_lost`. Les mesures reçues restent enregistrables.
- Les refus de configuration arrêtent tout de suite, sans nouvelle tentative : exception Modbus 01/02/03,
  DB absente (0x0A), adresse hors zone (0x05), accès refusé (0x03), PUT/GET désactivé (0x8104).
- L’écran affiche l’état de la connexion, le dernier aller-retour, une barre de progression,
  les mesures reçues et manquées, le temps écoulé et le temps restant, ainsi que la durée prévue avant le lancement.

## Enregistrement continu : fabriquer un historique

Coche « Enregistrement continu » dans la fenêtre d'acquisition. L'écran demande une durée en heures, une taille de segment et un recouvrement. Il affiche le nombre de segments et la taille approximative.
- **Lecture :** c'est la même lecture seule, au même calendrier, avec les mêmes lacunes. La limite passe de 5 000 mesures à 2 000 000 (par exemple 23 jours à 1 s).
- **Segments :** la lecture est découpée en segments de 10 à 5 000 mesures (3 000 par défaut). Chaque segment est archivé dans le projet dès qu'il est complet, avec son empreinte, son nom `historique-<session>-<n°>.csv` et sa provenance. Une coupure de courant ou un arrêt ne perd que le segment ouvert, et l'arrêt par l'opérateur enregistre ce segment.
- **Recouvrement :** chaque segment reprend les dernières mesures du précédent (300 par défaut), à l'identique. Un échelon proche d'une coupure reste entier dans l'un des segments. Un essai plus long que le recouvrement peut être coupé : utilisez alors l'acquisition ponctuelle.
- **Lacunes :** chaque lacune est rattachée au segment qui la suit, avec sa cause, et jamais interpolée.
- **Arrêts :** une erreur d'enregistrement d'un segment (disque plein, par exemple) arrête la lecture avec le motif `storage_error`. Les segments déjà enregistrés sont conservés.
- **Usage :** réalisez les échelons de MV avec le régulateur en manuel pendant l'enregistrement. Ouvrez ensuite « Identification » et utilisez « Rechercher les essais exploitables dans cet enregistrement… ». Deux segments consécutifs se recouvrent en temps. Si une paire choisie sur deux segments voisins est refusée (`fit_independence`), raccourcissez la fin de la première plage pour qu'elle précède le début de la seconde.

## Archivage (DATA-001)

Le CSV archivé contient `timestamp,SP,PV,MV,rtt_ms,SP_raw,PV_raw,MV_raw` et son empreinte SHA-256.
La provenance enregistrée dans SQLite comprend la cible complète (protocole, adresses, types, ordre, mise à l’échelle),
le plan, le motif d’arrêt et les statistiques : mesures reçues et manquées, reconnexions, aller-retour moyen et maximal,
retard maximal et liste des lacunes. La fiche PDF reprend cette provenance et chaque lacune.
Les acquisitions enregistrées avant ce lot (format Modbus d’origine) restent relisibles.

## Vérification

| Preuve | Où | Résultat attendu |
|---|---|---|
| Décodage sur constantes IEEE 754 et complément à deux, 4 ordres, 5 types | `tests/Mkr.Communication.Tests` | Valeurs exactes |
| Chemins d’erreur Modbus : exceptions 02 et 06, silence, coupure, trame fragmentée, NaN, refus de connexion, coupure prolongée, arrêt opérateur | idem, serveur simulé | Motif d’arrêt, lacunes et reconnexions conformes |
| Calendrier : 100 ms tenus malgré 40 ms d’aller-retour | idem | Période médiane 100 ± 20 ms |
| S7 : fonctions émises, TSAP, DB et M, alignement des longueurs impaires, refus 0x0A, 0x03, 0x8104 | idem, serveur simulé | Conforme |
| **Interopérabilité indépendante** : valeurs encodées par pymodbus 3.8.6 et servies par la bibliothèque Snap7 (python-snap7 2.0.2) | idem avec `MKR_INTEROP_PYTHON` | 10 cas relus exactement, refus Snap7 reconnus |
| Historique : 5 cas (découpe avec recouvrement et chronologie complète, lacune rattachée au bon segment, arrêt sur erreur de stockage, arrêt opérateur, validation du plan) | idem | Aucune mesure perdue ni dupliquée hors recouvrement |
| Historique de bout en bout : enregistrement, archivage en segments, réouverture, recherche des échelons, identification validée sur deux segments, refus d'un chevauchement en temps | `tests/Mkr.Smoke/AcquisitionChecks.cs` | Gain et constante de temps retrouvés |
| Parcours graphique S7 puis Modbus depuis le paquet Windows | `tests/windows/acquisition.ps1` | Lecture d’essai, progression 5/5, enregistrement, refus d’adresse |
| Acquisition, identification et PI ; équivalence avec le parcours CSV (COM-001) | `tests/Mkr.Smoke/AcquisitionChecks.cs` | Inchangé |

**Non vérifié à ce stade** : CPU physiques M340/M580 et S7-1200/1500, firmwares, passerelles,
gigue et pertes sur un réseau réel, capture réseau de non-écriture sur banc. Ces essais conditionnent
le statut « QUALIFIÉ SUR BANC INDUSTRIEL ».
