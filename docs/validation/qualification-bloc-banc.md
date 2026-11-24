# Qualification d'un bloc régulateur sur banc (sans hypothèse d'équation)

Objet : établir par la mesure comment un bloc constructeur transforme ses paramètres en action de commande, afin de qualifier la conversion depuis la forme générique de MKR (AGENTS.md §7). Applicable à PIDFF et PIDINT (Schneider, Control Expert) et PID_Compact (Siemens, TIA Portal).

**Ce protocole complète la documentation officielle, il ne la remplace pas.** L'équation, la forme et les unités viennent d'abord de la documentation du produit et de la version utilisés. Cette documentation n'a pas pu être consultée par MKR au 24/11/2026 : aucune équation de ces blocs n'est enregistrée dans le catalogue.

## 1. Identifier la configuration (obligatoire)

CPU, firmware, version du logiciel (Control Expert ou TIA Portal), version de la bibliothèque ou du bloc (par exemple PID_Compact V2.x ou V3.x), période d'exécution du bloc, plage et unité de la mesure, plage et unité de la sortie. Le relevé de banc du catalogue reprend ces champs.

## 2. Relevé de la documentation (obligatoire, à remplir par un humain)

Pour chaque bloc et version : forme (série, parallèle ou mixte), place des actions P, I et D, filtre de dérivée et pondération de consigne, unités de Ti et de Td (secondes, minutes, durée IEC), normalisation de la mesure et de la sortie (unité physique ou pourcentage), sens d'action, anti-windup, comportement en manuel et au retour en automatique. Chaque information cite la page, l'édition et la version.

## 2 bis. Neutraliser les fonctions annexes (essai E0)

Avant toute équivalence, mettre à leur valeur **neutre documentée** toutes les fonctions qui n'existent pas dans le régulateur générique de MKR, et vérifier qu'elles le sont (mesure de la sortie, sans action P, I ni D). Valeur neutre = celle que la documentation désigne, jamais une valeur supposée. D'après la structure observée du PIDFF (`docs/catalogue/blocs-observes.md`) :
- pondération de consigne (`ovs_att`) ;
- zone morte et gain dans la zone morte (`dband`, `gain_kp`) ;
- choix de l'entrée de dérivée (`pv_dev`) et facteur `kd` ;
- anticipation `FF` et ses bornes, décalage `outbias` ;
- limiteur de variation (`outrate`) et limiteurs de sortie ;
- suivi (`TR_I`, `TR_S`) et retour de position (`RCPY`, `en_rcpy`).

Pour PID_Compact et pour le bloc « PID » : dresser la même liste depuis la documentation de la version utilisée. Tant que la liste n'est pas établie et que E0 n'est pas passé, E1 à E7 ne sont pas interprétables.

## 3. Essais en boucle fermée sur le procédé simulé ou réel (banc)

Procédé de banc ou modèle simulé dans l'automate, avec un modèle connu : par exemple un premier ordre avec retard dont K, τ et θ sont mesurés.

| Essai | Réglage | Observation | Ce qu'on établit |
|---|---|---|---|
| E1 · gain proportionnel | P seul (intégrale et dérivée désactivées) | Échelon de consigne ΔSP, sortie en régime établi ou sortie initiale | Relation entre Kp saisi et ΔMV/ΔPV : unité, normalisation, signe |
| E2 · intégrale | Kp fixé, Ti réglé, erreur constante imposée en boucle ouverte | Pente de la sortie dMV/dt | Unité de Ti et sa place (devant Kp ou non) |
| E3 · dérivée | Kp fixé, Td réglé, rampe de mesure | Sortie, filtre de dérivée | Unité de Td, dérivée sur PV ou sur l'erreur, constante du filtre |
| E4 · sens d'action | Signe de l'erreur inversé | Sens de la sortie | Convention de signe, et paramètre d'inversion |
| E5 · saturation | Erreur grande et durable | Sortie, intégrale | Limites et anti-windup |
| E6 · manuel → automatique | Sortie manuelle puis passage en automatique | Sortie avant et après | Transfert sans à-coup |
| E7 · équivalence | Réglage calculé par MKR, converti | Réponse du banc à comparer à la simulation MKR | Écart quantifié, avec tolérance fixée avant l'essai |

## 4. Résultat et décision

Le rapport contient la configuration, la documentation citée, les traces de mesure (fichiers d'origine et empreintes), les écarts et la conclusion de la revue, **signée par un relecteur distinct de l'opérateur**. Seule une conversion qui satisfait E1 à E7 dans les tolérances fixées peut passer du statut « non qualifiée » à « qualifiée sur banc pour la configuration identifiée ». Elle n'est valable que pour cette configuration.
