# Feuille de route MKR — d'après le schéma fonctionnel du mainteneur

Source : schéma « Ingénierie de la régulation et de l'asservissement industriel » (10 étapes) fourni par le mainteneur le 24/11/2026. État relevé le même jour. Légende : **OK** couvert, **PARTIEL**, **ABSENT**, **VOLONTAIRE** (écart assumé).

Principe : le noyau (identification, validation, réglage, simulation) ne connaît aucun constructeur ; seuls le catalogue de blocs et les lecteurs de protocole sont spécifiques. Aucune conversion de bloc n'est qualifiée ; aucune écriture vers l'automate n'existe.

| # | Étape | État | Ce qui existe | Ce qui manque |
|---|---|---|---|---|
| 01 | Définir la boucle | PARTIEL | Repère, grandeur réglée, point de fonctionnement, objectif ; limites et vitesse de MV dans les scénarios | Modèle d'actionneur et d'instrumentation (frottement, jeu, zone morte) |
| 02 | Acquérir | PARTIEL | CSV, Modbus TCP, S7comm (lecture seule), enregistrement continu, mode manuel/auto à adresse saisie | OPC UA, Excel, essai en rampe ; écran de la boucle fermée |
| 03 | Prétraiter | VOLONTAIRE | Qualité, bruit mesuré, échantillonnage, fenêtres d'analyse, saturation détectée | Aucun filtrage ni lissage, par choix : il fausserait l'identification |
| 04 | Identifier | PARTIEL | FOPDT, SOPDT, intégrateur, Broïda ; boucle fermée (moteur expérimental) | Ordre supérieur, non linéaire |
| 05 | Valider | OK | Essai indépendant, erreur comparée au bruit, incertitude du retard, refus bloquant le réglage | Données réelles d'installation |
| 06 | Choisir la stratégie | PARTIEL | Sélection P / PI / PID sur simulations comparables | Cascade, anticipation, Smith, gain scheduling, MPC |
| 07 | Calculer | PARTIEL | IMC/Lambda, SIMC (PI, PID second ordre, intégrateur) | Ziegler–Nichols, Cohen–Coon, optimisation |
| 08 | Simuler et comparer | OK | Réponse, comparaison, sensibilité, marges (gain, phase, Ms), dépassement, établissement, montée, ITAE | Perturbations de procédé plus riches |
| 09 | Implémenter et valider | PARTIEL | Valeurs documentaires PIDFF, PID_INT, PID_Compact V2.x/V3.x, protocole de banc E0–E7 | Rockwell, ABB ; qualification sur banc ; **aucune application à l'automate (VOLONTAIRE)** |
| 10 | Capitaliser et reporter | PARTIEL | Fiche PDF, empreintes, provenance en base | Retour d'expérience structuré |

## Au-delà du schéma (exigé pour un usage industriel)

| Sujet | État |
|---|---|
| Journal des modifications | PARTIEL : journal d'audit en ajout seul, chaîné par SHA-256, avec vérification, fenêtre de consultation et export vérifiable sans MKR (`scripts/verify-audit-export.py`) (identifications, réglages, valeurs de blocs affichées, dérive, imports, acquisitions, fiche de boucle) |
| Droits des utilisateurs (rôles, authentification) | ABSENT : l'acteur tracé est le nom de session Windows, non authentifié par MKR |
| Surveillance des boucles après mise en service (dérive du modèle) | PARTIEL : contrôle d'un modèle FOPDT, SOPDT ou intégrateur accepté sur un nouvel échelon en boucle ouverte (moteur, contrat .NET, bouton de l'écran d'identification) ; pas de surveillance en fonctionnement normal |
| Plusieurs points de fonctionnement d'un même procédé | ABSENT |
| Couplage entre boucles (MIMO) | ABSENT |
| Qualification de l'outil lui-même | ABSENT |
| Essai sur automate réel (Modbus, S7) | NON FAIT : serveurs simulés seulement |

## Priorités proposées (à confirmer)

1. Écran du réglage des procédés intégrateurs (étape 07, déjà calculé dans le moteur).
2. Suivi des boucles après mise en service : comparer de nouvelles données au modèle validé (étape 10 et au-delà).
3. Import OPC UA en lecture seule (étape 02).
4. Test graphique Windows des champs de mode de l'acquisition.
