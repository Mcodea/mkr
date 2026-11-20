# Contrat WPF–Python v1

Un document JSON UTF-8 sur stdin, fermé après écriture, un document JSON UTF-8 sur stdout. Une exécution par requête. stderr est réservé au diagnostic interne. Le client limite l'exécution à dix secondes et termine le processus enfant à l'annulation.

Requête exacte : `{"version":1,"requestId":"abc123","operation":"ping","payload":{}}`. L'identifiant est ASCII alphanumérique, avec tiret/underscore autorisés, limité à 64 caractères. L'opération ping vérifie la disponibilité et ne calcule aucun résultat. Le moteur 0.1 acceptait au plus 8192 octets en entrée.

Succès : `{"version":1,"requestId":"abc123","engineVersion":"0.2.0","result":{"status":"ready"}}`.

Erreur : `{"version":1,"requestId":"abc123","engineVersion":"0.2.0","error":{"code":"unsupported_operation"}}`. requestId vaut null si l'identifiant ne peut être validé. Codes : invalid_json, invalid_request, unsupported_version, unsupported_operation. Les erreurs de contrat sont des réponses JSON. Un code de sortie non nul est un échec de processus. Toute future opération scientifique versionnera aussi son algorithme.

## Extension CSV du moteur 0.2

Le plafond du message passe à 1 500 000 octets. Le client v1 conserve la même requête ping. Les opérations `inspect_csv` et `analyze_csv` prennent `raw` (octets originaux en Base64) et `delimiter` (virgule ou point-virgule). L’analyse demande aussi `timeColumn`, `valueColumn`, `role` et `unit`. La réponse contient `headers`, `preview` (200 enregistrements au plus) et `rowCount`. L’analyse ajoute `quality` et son `algorithmVersion`. Les erreurs CSV utilisent le même format d’enveloppe avec les codes `csv_limit`, `csv_header`, `csv_width`, `csv_empty`, `csv_encoding_or_syntax` et `csv_mapping`.
