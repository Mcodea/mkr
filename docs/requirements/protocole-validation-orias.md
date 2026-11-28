# Protocole directeur de validation scientifique, expérimentale et industrielle du moteur ORIAS — résumé opérationnel

**Statut : document directeur du client reçu le 2026-11-28 (26 sections).** Ce fichier en est un **résumé fidèle section par section, PAS le texte intégral** ; le texte intégral fait foi et doit être déposé par le client s'il doit être conservé verbatim dans le dépôt (le cahier directeur, lui, est conservé verbatim dans `cahier-directeur-orias.md`). En cas d'écart, le texte du client prévaut.

| § | Exigence | Engagement ORIAS / où c'est traité |
|---|---|---|
| 1 | Équipe pluridisciplinaire ; démontrer la **généralisation**, pas reproduire des résultats connus ; ajustement ≠ aptitude à la régulation | `docs/science/lot4/` ; principe appliqué à toutes les études |
| 2 | Audit préalable ; ne pas refaire l'existant vérifié ; non-régression ; ne pas remplacer un algorithme sans faiblesse démontrée | `docs/science/lot4/audit-protocole-validation.md` |
| 3 | Bibliothèque de données expérimentales indépendantes : 5 sources imposées (versions figées) + sources complémentaires diversifiées | `registre-jeux-de-donnees.md` / `.json` |
| 4 | Contrôle des sources : DOI, version, licence, empreintes officielles, SHA-256, intégrité ; ne jamais contourner un blocage ; ne jamais prétendre avoir téléchargé ; ne pas substituer une version sans documenter | registre (statut par source : téléchargé / référencé / inaccessible) |
| 5 | Registre versionné des jeux de données (24 champs) ; ne pas publier ce dont la redistribution est incertaine | registre ; aucune donnée brute versionnée |
| 6 | Quatre ensembles A développement, B calibration, C validation intermédiaire, D validation finale à l'aveugle ; séparation par procédé/campagne/essai, pas par lignes ; aucune fuite | registre §« ensembles » ; **D non réalisable avec les sources publiques actuelles** (voir §7) |
| 7 | Validation finale à l'aveugle : moteur, critères et seuils **figés** (commit exact) ; évaluateur indépendant ; résultats enregistrés et empreintes **avant** révélation des références ; ne pas prétendre à l'aveugle sans séparation effective des accès ; un autre agent IA ne prouve pas l'indépendance | préparation de l'outillage seulement ; **aucune validation à l'aveugle n'est revendiquée** |
| 8 | Validation du diagnostic des données (manquants, aberrants, bruit, échantillonnage, saturations, dérives, modes, unités, excitation, durée) avec taux de détection / non-détection / fausses alertes | à mesurer sur les jeux du registre |
| 9 | Assistant d'essais (lecture seule, aucune sollicitation du procédé sans autorisation humaine) | existant : `trial_assistant.py` ; validation expérimentale à faire |
| 10 | Identification : structures ≠ méthodes (Broïda est une méthode, pas une famille) ; ne jamais supposer la boucle ouverte ; méthodes non implémentées signalées | audit ; voir `docs/science/ID-07A-broida.md` |
| 11 | Sélection intelligente du modèle (pas l'erreur minimale ; alternatives ; essai complémentaire ; refus) | audit ; fonctionnalité à consolider |
| 12 | Stratégies P, PI, PID, PD, cascade, feedforward, ratio, split-range, override, Smith, gain scheduling, MPC : évaluer, ne pas recommander sans conditions | audit (couverture réelle à établir) |
| 13 | Réglages IMC, Lambda, SIMC, Ziegler-Nichols, Cohen-Coon, optimisation, robuste : équations, conventions, domaines ; juger sur la boucle fermée | audit ; l'utilisateur ne règle aucun seuil statistique |
| 14 | Stabilité et robustesse indépendantes de Ms ; garanties démontrées ≠ estimations numériques | existant : Nyquist + temps vs `stability_margins.py` (étude LOT 4) |
| 15 | Situations A (boucle ouverte), B (boucle fermée historique), C (nouveau réglage : simulation seulement, jamais preuve expérimentale) | appliqué dans `registre` (type de boucle) |
| 16 | Blocs constructeurs : niveaux documenté / modélisé / conversion vérifiée / simulation vérifiée / testé chez le constructeur / qualifié sur automate | audit ; aucun bloc n'est qualifié sur automate |
| 17 | Critères de décision : faux positifs/négatifs définis, bornes unilatérales, événements rares, comparaisons multiples, biais de sélection, couverture, 5 % et 1 % (objectifs provisoires), cohérence risque/fonction de décision | `rapport-etude-validation.md` + amendements v1.4 et v1.5 |
| 18 | Références et oracles indépendants ; une sortie enregistrée du moteur n'est pas un oracle ; reproductibilité ≠ exactitude | `docs/science/references/` ; audit |
| 19 | Gestion des échecs : conserver, analyser, corriger, tester, ne pas supprimer les cas difficiles ni changer les seuils pour réussir | pratique constante ; amendements datés |
| 20 | Traçabilité et invalidation ; états non calculé/en cours/non validé/accepté/refusé/obsolète ; pas de booléen de validation sans preuve | LOT 1 (ADR 0009), preuve scellée |
| 21 | Infrastructure de tests et GitHub Actions ; données à l'aveugle hors des infrastructures du développeur | `.github/workflows/verify.yml` |
| 22 | Expérience utilisateur : conclusions simples, justifiées, avec action | `Advisor` (LOT 1) ; interface validée par le client |
| 23 | Rapport scientifique et matrice de qualification en 8 niveaux, sans niveau sans preuve | `matrice-de-qualification.md` |
| 24 | Interdits : inventer données / publications / résultats / règles ; fausse téléchargement ; synthétique présenté comme expérimental ; consulter des références réservées avant l'aveugle ; adapter aux résultats attendus ; modifier les seuils arbitrairement ; masquer ; confondre ajustement/aptitude, simulation/validation, logiciel/qualification | appliqués |
| 25 | Plan en 10 étapes : audit, acquisition, benchmarks, évaluation, consolidation, intégration/non-régression, gel, validation à l'aveugle, qualification industrielle, bilan | `audit-protocole-validation.md` §« Plan » |
| 26 | Autonomie ; une étude = une conclusion exploitable ; ne solliciter le responsable que pour arbitrages, accès, sécurité, moyens matériels | appliqué |
