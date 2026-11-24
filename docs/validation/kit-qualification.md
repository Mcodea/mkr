# Kit de qualification sur banc

Procédure exécutable par un tiers qui dispose d'un banc, sans passer par les développeurs. Elle complète [qualification-bloc-banc.md](qualification-bloc-banc.md) (essais E0 à E7 d'un bloc constructeur) par des **critères d'acceptation chiffrés, fixés avant l'essai**, et un rapport à remplir. Rien ici ne qualifie MKR tant qu'un humain n'a pas signé un rapport complété.

## 0. Règles

- Les tolérances ci-dessous sont **fixées avant les mesures** et ne se changent pas après les avoir vues. Tout écart de procédure est consigné.
- Conserver les fichiers bruts (CSV, captures réseau) avec leur empreinte SHA-256 : MKR les affiche à l'import.
- Exporter le journal d'audit à la fin et noter l'empreinte de tête hors du poste (`docs/audit.md`).
- Un essai échoué est un résultat, pas à refaire jusqu'à réussite sans le consigner.

## 1. Acquisition en lecture seule (Modbus TCP, S7comm)

| Essai | Procédure | Critère d'acceptation |
|---|---|---|
| A1 · valeurs | Lire SP, PV, MV et comparer aux valeurs de la table d'animation de l'atelier de l'automate (au moins 10 instants) | Écart ≤ 1 unité de quantification du signal, sur chaque instant |
| A2 · non-écriture | Capture réseau (Wireshark) pendant une acquisition de 10 minutes | **Aucune** trame d'écriture : Modbus fonctions autres que 03 et 04 ; S7 fonctions autres que 0xF0 et 0x04 |
| A3 · cadence | Acquisition à 1 s sur 1 h | Période médiane 1 s ± 5 % ; lacunes consignées avec leur cause, aucune interpolée |
| A4 · coupure | Débrancher le câble 5 s puis 60 s | Lacune enregistrée ; reprise automatique après 5 s ; arrêt `connection_lost` après la coupure tolérée |
| A5 · mode | Lire le mode à l'adresse déclarée, passer manuel/automatique | La colonne MANUAL suit le changement sans retard de plus d'une période |

## 2. Identification

Sur un procédé réel en boucle ouverte, réaliser **deux échelons de MV indépendants** (sens opposés, amplitudes dans un rapport de 0,5 à 2).

| Essai | Critère d'acceptation |
|---|---|
| I1 · porte de validation | La décision de MKR (accepté ou refusé) est conforme à l'examen d'un ingénieur de régulation qui ne l'a pas vue ; chaque refus a un motif lisible |
| I2 · prédiction | Un troisième échelon, non utilisé pour l'estimation, est prédit avec une erreur RMS ≤ 2 fois le bruit mesuré sur le plateau initial |
| I3 · répétabilité | Trois estimations indépendantes du même procédé : écart relatif ≤ 15 % sur le gain et ≤ 25 % sur la constante de temps |
| I4 · défauts | Provoquer volontairement une perturbation de charge pendant un échelon : MKR doit refuser ou avertir, jamais accepter en silence |

## 3. Réglage et simulation

| Essai | Critère d'acceptation |
|---|---|
| R1 · boucle fermée | Régler la boucle avec le PI calculé (valeurs saisies à la main) : la réponse mesurée à un échelon de consigne présente un dépassement et un temps d'établissement dans une tolérance de ± 30 % de la simulation MKR (tolérance à confirmer avant l'essai) |
| R2 · marges | La marge de gain mesurée par augmentation du gain jusqu'à l'oscillation entretenue est au moins la moitié de la marge calculée |

## 4. Valeurs de blocs constructeur

Suivre [qualification-bloc-banc.md](qualification-bloc-banc.md) (E0 à E7). **Les valeurs affichées par MKR restent « non qualifiées » tant qu'un rapport signé ne couvre pas la version exacte du logiciel, de la bibliothèque et du micrologiciel.** Ne jamais charger de valeur automatiquement.

## 5. Identité et journal

| Essai | Critère d'acceptation |
|---|---|
| J1 · identités | Deux comptes Windows distincts : les entrées portent chacun leur SID et leur poste |
| J2 · quatre yeux | Le second compte vérifie ce que le premier a calculé sans auto-revue ; le premier ne le peut pas sans la déclarer |
| J3 · export | Le script `verify-audit-export.py` valide l'export ; une modification d'un octet est détectée |

## 6. Rapport (à remplir)

```
Banc : (automate, version, firmware, logiciel, procédé)   Date :        Opérateur (compte Windows) :
Version MKR (build, empreinte du paquet) :
Empreinte de tête du journal :
| Essai | Valeur mesurée | Critère | Conforme (O/N) | Pièce (fichier, SHA-256) |
| A1    |                |         |                |                          |
... (une ligne par essai)
Écarts de procédure :
Décision : (non qualifié / qualifié pour le périmètre suivant : ...)
Signature, date :
```

La décision « qualifié » n'est pas prise par MKR : elle appartient à l'exploitant, pour un périmètre écrit.
