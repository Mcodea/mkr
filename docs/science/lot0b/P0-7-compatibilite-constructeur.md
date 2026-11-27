# P0-7 — Compatibilité constructeur non contrôlée

## Anomalie
`engine/block_catalogue.py::lookup` ne filtre un bloc que si le champ de la fiche est renseigné ET différent de la cible : un champ vide est donc traité comme compatible. Côté C#, la cible envoyée est toujours vide (`PythonDiagnostics.cs`). Résultat : une conversion PID_Compact est rendue pour une cible vide, sans avertissement, et une cible V3.x acceptait des paramètres V2.4.

## Preuve
`engine/test_block_target_compatibility.py` (cible vide, cible partielle, version de bibliothèque différente, autre constructeur). **Avant correction : 4 échecs** (aucun champ `compatibility`, aucun avertissement, refus de version absent).

## Impact
L'utilisateur pouvait lire une conversion « Siemens » pour un M580 Schneider sans qu'aucune incompatibilité ou absence de vérification ne soit signalée (cahier §21 : « ne jamais masquer une incompatibilité »).

## Référence
Exigence §21 ; manuels constructeurs cités par le catalogue (Schneider 33002536 et 33002520, Siemens A5E35300227-AJ) — le contenu documentaire du catalogue n'est pas modifié ici.

## Correction
`convert()` renvoie désormais `compatibility` : `status` = `verified_target` seulement si une fiche du catalogue est vérifiée pour **tous** les champs de la cible déclarée, sinon `not_verified`, avec la liste `unverifiedTargetFields`. Une conversion non vérifiée porte l'avertissement `hardware_compatibility_not_verified`, affiché par l'application (« compatibilité matérielle non vérifiée … conversion non qualifiée »). La version de bibliothèque de la cible doit correspondre à celle des paramètres (famille « V3.x » couvre « V3.0 »), sinon refus `conversion_version_mismatch`. La conversion numérique est inchangée ; `readyToLoad` reste faux.

## Résultats
Avant : 4 tests en échec. Après : 41 tests de conversion verts (nouveaux et existants, aucun modifié).

## Limites et suite (LOT 7)
Aucune fiche du catalogue n'est « vérifiée » aujourd'hui : le statut sera toujours `not_verified` jusqu'à vérification sur simulateur ou banc. L'interface doit encore **demander la cible** (CPU, firmware, versions) et la transmettre ; le blocage du bloc non applicable à la famille d'automate (ex. PID_Compact pour un M580) viendra avec les fiches vérifiées.
