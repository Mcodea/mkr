# Journal d'audit

Chaque projet MKR tient un journal **en ajout seul** des actions sensibles : identification enregistrée (acceptée ou refusée, avec les motifs), réglages et simulations enregistrés, valeurs de blocs affichées (toujours « non qualifiées » ou refusées), contrôle de dérive, imports, acquisitions et segments d'historique, fiche de boucle, fiche PDF, exports du journal.

## Protection

- Chaque entrée porte l'empreinte SHA-256 de la précédente (champs préfixés par leur longueur en unités UTF-16, pour qu'aucune valeur n'imite un séparateur). Une entrée modifiée ou retirée au milieu rompt la chaîne et la vérification la localise.
- Des déclencheurs SQLite refusent `UPDATE` et `DELETE`. Quelqu'un qui peut écrire dans le fichier peut les supprimer : la vérification de la chaîne le montre alors.
- **Limite :** qui peut tout écrire dans le fichier du projet peut recalculer toute la chaîne. Pour le détecter, **exportez le journal et conservez l'empreinte de tête ailleurs**.

## Export et vérification sans MKR

« Journal d'audit… » → « Exporter… » écrit un fichier JSON Lines (en-tête : projet, date d'export, nombre d'entrées, empreinte de tête). Il se vérifie indépendamment, sans MKR :

```
python3 -I scripts/verify-audit-export.py journal-audit.jsonl [EMPREINTE_DE_TETE_NOTEE]
```

Code de sortie 0 : chaîne intacte (et empreinte conforme si fournie). 1 : chaîne rompue ou empreinte différente. La CI vérifie que ce script et l'implémentation .NET s'accordent, y compris sur les caractères non ASCII et hors plan multilingue de base.

## Ce que le journal n'est pas

- L'acteur est l'**identité de la session Windows** (`DOMAINE\utilisateur [SID] @POSTE`), prise dans le système, pas saisie. MKR n'authentifie pas la personne : il n'y a ni rôles ni droits. Voir [exploitation.md](exploitation.md).
- Il ne trace ni l'ouverture d'un projet ni les simulations de simple prévisualisation.
- Ce n'est pas une signature : il ne prouve pas qui a écrit, seulement que la suite des entrées n'a pas été altérée depuis l'export.
