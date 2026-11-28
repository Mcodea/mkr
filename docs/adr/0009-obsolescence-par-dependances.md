# ADR 0009 — Obsolescence automatique des résultats par dépendances réelles

Statut : **tranche 1 implémentée** (logique pure du cœur, testée) ; tranches 2 (enregistrement + migration SQLite 12) et 3 (interface) **à faire**. **Décision du client du 2026-11-28** : gestion automatique de l'obsolescence fondée sur les dépendances réelles entre les étapes ; données importées immuables ; propagation aux seuls résultats dépendants ; anciens résultats, historique, provenance et raison conservés ; validation finale et export opérationnel interdits pour une recommandation obsolète tant qu'elle n'est pas recalculée et revalidée ; l'utilisateur comprend immédiatement pourquoi et quoi faire sans gérer les dépendances à la main (cahier directeur §25 point 12, §26).

## Contexte
Aujourd'hui, l'avancement du parcours (`StudyJourney`) est dérivé de simples **compteurs** (importations, études, modèles acceptés). Rien ne dit qu'une étude a été calculée avec une configuration qui n'est plus la configuration active : un « modèle accepté » le reste même si la fenêtre d'identification, les unités ou la stratégie ont changé depuis. Le cahier exige « ne jamais laisser un résultat obsolète apparaître comme actuellement valide » (§25).

## Décision
1. **L'obsolescence est DÉRIVÉE, jamais stockée.** Chaque résultat enregistre l'**empreinte (SHA-256 d'un texte canonique) des facettes de configuration dont il dépend**, telle qu'elle était à son calcul. L'état courant est recalculé en comparant ces empreintes à la configuration **active** du projet : aucun drapeau « obsolète » ne peut donc rester périmé, et rétablir une valeur antérieure rend de nouveau courants les résultats qui en dépendent.
2. **Onze facettes** : jeu de données actif ; colonnes associées aux signaux ; unités et conversions ; fenêtre d'identification ; prétraitement ; méthode ou modèle d'identification ; paramètres du procédé ; stratégie de régulation ; bloc constructeur ; paramètres PID ; contraintes et limites d'exploitation. Les **données originales importées restent immuables** : le « jeu de données actif » est la **sélection** de l'import (id + SHA-256), pas son contenu.
3. **Table de règles explicite et versionnée** (`DependencyRules`, version `1.0.0`) : de quelles facettes chaque type de résultat dépend. Un changement de facette n'invalide **que** les résultats dont la table dit qu'ils en dépendent, puis, par la chaîne amont → aval, leurs dérivés. Une facette non listée pour un type ne l'invalide jamais.
4. **Propagation en cascade par les dépendances de résultats** (modèle → réglage → simulation / conversion → rapport). Un amont obsolète ou **absent** rend l'aval obsolète (raison `UpstreamObsolete` / `UpstreamMissing`). Les branches indépendantes ne sont pas touchées. Un cycle de dépendances est une erreur (`InvalidDataException`).
5. **Huit statuts** du cahier §26 : non commencé, en cours, à compléter, à vérifier, validé pour le domaine étudié, bloqué, non applicable, résultat obsolète. Les raisons sont **structurées** (cause, facette, amont, empreintes, date et auteur du changement), jamais du texte libre : la localisation et la formulation de l'action relèvent de l'interface.
6. **Anciens résultats conservés** : un résultat obsolète n'est ni modifié ni supprimé ; il reste consultable avec sa provenance et la raison de son obsolescence. Un recalcul crée **un nouveau résultat** (nouvelle empreinte) ; l'ancien reste historisé.
7. **Export opérationnel et validation finale** : interdits si le résultat ou l'un de ses amonts est obsolète ou bloqué, ou s'il n'a pas franchi sa propre porte de validation. Un résultat **antérieur à cette ADR** (sans enregistrement de dépendances) est « à vérifier », **non obsolète** : il garde son comportement actuel et l'interface signale qu'aucune dépendance n'a été enregistrée (aucune obsolescence ne peut être affirmée ni exclue pour lui).
8. **Historique de configuration en ajout seul** : chaque changement de facette (ancienne et nouvelle empreintes, date, auteur, motif) est conservé ; réappliquer une valeur identique ne crée pas d'entrée.

## Alternatives écartées
- **Drapeau « obsolète » stocké** et mis à jour à chaque modification : risque de dérive et d'oubli (un oubli laisse un résultat périmé apparaître valide) ; contraire à §25.
- **Invalider tout le projet à chaque changement** : invalide inutilement les étapes indépendantes (interdit par la décision du client).
- **Détecter l'obsolescence par comparaison de dates** : une date ne dit pas quelle donnée a changé et crée des faux positifs.

## Plan par tranches
- **Tranche 1 (faite)** : `Mkr.Core.Dependencies` (facettes, empreintes, configuration et historique, règles, moteur d'évaluation) ; `tests/Mkr.Orchestration.Tests` (réalisation exécutable sous Linux et en CI).
- **Tranche 2 (à faire, soumise à cette ADR)** : champ optionnel `Dependencies` dans les résultats (JSON, sans migration) ; **migration SQLite 12, additive** : table `configuration_versions` (ajout seul, chaîne de hachage comme l'audit) ; sauvegarde préalable automatique (déjà en place) ; tests de migration depuis 11 ; lecture compatible des anciens résultats.
- **Tranche 3 (à faire)** : `StudyJourney` branché sur le moteur ; raison et action affichées dans le parcours et sur chaque résultat ; export et validation finale bloqués ; captures CI.

## Conséquences et risques
- Les études existantes passent « à vérifier » tant qu'elles n'ont pas été recalculées : le parcours ne régresse pas, mais l'interface devra l'expliquer (tranche 3).
- La table de règles est une **hypothèse de conception documentée** : elle est testée mais sa pertinence métier doit être relue (par exemple : les limites d'exploitation invalident le réglage et la simulation, pas le modèle).
- Aucun champ SQL n'est modifié en tranche 1.

## Retour arrière
Tranche 1 : retirer le fichier du cœur (aucune dépendance entrante). Tranche 2 : restaurer la sauvegarde avant migration ; un MKR antérieur refuse `user_version = 12` (convention des migrations 9 à 11).
