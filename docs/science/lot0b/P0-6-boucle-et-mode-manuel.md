# P0-6 — `loop="open"` forcé et colonne MANUAL ignorée

## Anomalie
`PythonDiagnostics.cs:104` envoie toujours `loop = "open"` à l'ARX, donc le refus de données en boucle fermée ne peut pas se déclencher depuis l'application ; `fopdt.read_step` et l'ARX ignorent la colonne MANUAL (seul `step_finder` l'exploite) ; l'identification en boucle fermée (`closed_loop.py` 0.1.0, ADR 0006) n'est pas branchée à l'interface.

## Décision : reportée au LOT 2, avec justification
Une correction locale serait cosmétique : le type de boucle et le mode manuel/automatique sont des **propriétés des données** qui doivent être (1) déclarées ou détectées à l'import (mappage de la colonne de mode, étape de qualification), (2) enregistrées dans l'étude, (3) transmises aux moteurs d'identification, et (4) restituées dans le verdict de qualification à 5 niveaux (cahier §11). Cela exige un changement du modèle de données et du contrat d'import, qui est le cœur du LOT 2. Modifier uniquement la valeur `"open"` en C# sans cette chaîne reviendrait à déclarer sans preuve.

## Mesure de protection immédiate
Le moteur de réglage refuse désormais tout modèle sans validation acceptée (P0-4) et n'attribue plus un premier ordre à un intégrateur (P0-2) ; la limite reste documentée dans le handoff et la matrice de qualification : **l'identification suppose des données en boucle ouverte, non vérifiée par l'application**.

## Protection côté moteur ajoutée (2026-11-27) — correction PARTIELLE
`fopdt.read_step` (donc FOPDT, SOPDT, intégrateur, Broïda, comparaison, portail de validation, dérive) et le lecteur ARX **refusent** désormais toute fenêtre d'identification qui contient une ligne hors mode manuel **lorsque le CSV porte la colonne `MANUAL`** écrite par l'acquisition MKR (`fit_not_manual` / `arx_not_manual` ; valeur absente, illisible ou ≠ 1 = non manuel, même convention que `step_finder`). Seules les lignes de la fenêtre `rowStart`/`rowEnd` comptent ; une colonne de nom différent est ignorée. Tests : `engine/test_manual_mode_refusal.py` (8 tests ; ils échouent avant la correction ; un fichier entièrement manuel donne un résultat identique à celui du même fichier sans colonne).

**Ce qui reste non corrigé (inchangé).** Sans colonne `MANUAL`, aucune vérification : l'application ne déclare ni ne détecte le mode à l'import ; `loop = "open"` reste envoyé en dur à l'ARX par `PythonDiagnostics.cs` ; le verdict à 5 niveaux ne mentionne pas le mode ; l'identification en boucle fermée n'est pas branchée à l'interface. Ces points restent au LOT 2 (modèle de données et contrat d'import).
