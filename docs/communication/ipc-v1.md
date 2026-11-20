# Contrat WPF–Python v1

Un document JSON UTF-8 sur stdin, fermé après écriture, un document JSON UTF-8 sur stdout. Une exécution par requête. stderr est réservé au diagnostic interne. Le client limite l'exécution à dix secondes et termine le processus enfant à l'annulation.

Requête exacte : `{"version":1,"requestId":"abc123","operation":"ping","payload":{}}`. L'identifiant est ASCII alphanumérique, avec tiret/underscore autorisés, limité à 64 caractères. L'opération ping vérifie la disponibilité et ne calcule aucun résultat. Le moteur accepte au plus 8192 octets en entrée.

Succès : `{"version":1,"requestId":"abc123","engineVersion":"0.1.0","result":{"status":"ready"}}`.

Erreur : `{"version":1,"requestId":"abc123","engineVersion":"0.1.0","error":{"code":"unsupported_operation"}}`. requestId vaut null si l'identifiant ne peut être validé. Codes : invalid_json, invalid_request, unsupported_version, unsupported_operation. Les erreurs de contrat sont des réponses JSON. Un code de sortie non nul est un échec de processus. Toute future opération scientifique versionnera aussi son algorithme.
