# État de maturité de MKR

Relevé du 24/11/2026 (complété le 25/11 : voir docs/science/audit-moteur-scientifique.md pour l'état du moteur). Il sépare ce qui est **prouvé**, ce qui est **seulement simulé** et ce qui est **non éprouvé**, fonction par fonction. Aucune fonction n'est « qualifiée industriellement ». Aucun accès à un automate ni à un procédé réel n'était disponible pour ce relevé : tout ce qui suit a été vérifié sur simulations, fixtures, serveurs de référence et un jeu de mesures réelles publiées (TCLab).

Niveaux : **P** prouvé par test automatisé et CI ; **S** simulé seulement ; **N** non éprouvé ; **R** une preuve sur donnée réelle existe mais très limitée.

| Fonction | Niveau | Ce qui existe | Ce qui manque pour passer au niveau supérieur |
|---|---|---|---|
| Lecture CSV, qualité, formats français | P | Tests, fixtures, DST | Fichiers d'historiens réels variés |
| Acquisition Modbus TCP (lecture seule) | S | Serveur pymodbus indépendant, trames auditées | CPU réels, passerelles, gigue réseau, capture de non-écriture |
| Acquisition S7comm (lecture seule) | S | Serveur Snap7 indépendant | S7-1200/1500 réels, PUT/GET, DB optimisées |
| Lecture du mode manuel/auto | S | Serveurs simulés | Convention réelle de plusieurs programmes |
| Recherche d'échelons | S | Enregistrements synthétiques | Historiques réels bruités |
| Identification FOPDT/SOPDT/intégrateur/Broïda | R | Simulations variées ; refus des défauts de terrain ; une identification TCLab réelle | Procédés industriels (vannes, non-linéarités) |
| Porte de validation | S | Critères fixés avant ajustement ; refus testés | Taux de fausses acceptations sur données réelles |
| Réglages IMC / SIMC / SIMC intégrateur | S | Simulations en boucle fermée, marges | Réponse mesurée d'une boucle réelle |
| Marges de stabilité | S | Comparaison à la simulation | Idem |
| Valeurs de blocs PIDFF / PID_INT / PID_Compact | N | Équations lues dans les documents officiels ; équivalence de forme à un oracle | **Essai sur le bloc réel** (protocole E0–E7). Statut non qualifié maintenu |
| Contrôle de dérive d'un modèle | S | Simulations, calibrage Monte Carlo des erreurs | Dérives réelles |
| Identification en boucle fermée | S (expérimental) | Simulations ; refus ; dispersion publiée sous-estimée d'environ 30 % | Enregistrement réel comparé à un essai en boucle ouverte |
| Journal d'audit, export vérifiable | P | Chaîne SHA-256, vérificateur Python indépendant | Revue de sécurité externe |
| Identité de session, quatre yeux | S | Identités simulées ; SID réel non observé sur plusieurs comptes | Test sur deux sessions Windows réelles |
| Interface Windows | P | Tests UI Automation sur le paquet autonome en CI | Retours d'utilisateurs, accessibilité complète |

## Ce que cela autorise à dire

- MKR est un **outil d'étude et d'aide à la décision, non qualifié**, dont chaque résultat est reproductible et tracé.
- MKR **n'est pas** « fiable » ni « qualifié industriellement » : il n'existe aucune preuve sur automate ou procédé industriel réel.

## Chemin le plus court vers une preuve

1. Rejouer des enregistrements réels publics (voir le kit) et consigner les écarts.
2. Réaliser les essais du [kit de qualification](kit-qualification.md) sur un banc, même à bas coût (TCLab et un automate logiciel).
3. Seulement après, ouvrir la discussion d'une qualification de bloc constructeur par un tiers disposant de Control Expert ou de TIA Portal.
