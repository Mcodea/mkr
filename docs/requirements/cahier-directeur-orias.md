# ORIAS / MKR — Cahier des charges directeur — Moteur scientifique intelligent et parcours complet d'ingénierie de la régulation industrielle

**Source :** transmis par le client le 2026-11-26, déposé tel quel comme référence fonctionnelle et scientifique de la phase scientifique.
**Statut :** référence normative de la phase ; ne pas modifier sans décision explicite du client.

---

## 41. BENCHMARK SCIENTIFIQUE ET COMPÉTITIVITÉ INDUSTRIELLE

L’objectif d’ORIAS est d’atteindre progressivement un niveau scientifique, fonctionnel et ergonomique comparable aux logiciels industriels de référence, notamment LOOP-PRO Tuner, Valmet ExperTune et MATLAB PID Tuner.

### Identification en conditions industrielles difficiles

Étudier et qualifier les méthodes permettant d’exploiter :

- Des procédés hors régime permanent.
- Des conditions initiales non stabilisées.
- Des mesures bruitées.
- Des procédés oscillants.
- Des procédés intégrateurs.
- Des procédés à grand temps mort.
- Des historiques comportant plusieurs variations de commande ou de consigne.
- Des données issues du fonctionnement normal.
- Des données acquises en boucle fermée.
- Des données comportant des perturbations.

Étudier particulièrement les méthodes d’estimation des conditions initiales et d’identification en régime transitoire.

Analyser les capacités publiquement documentées de la technologie NSS de LOOP-PRO Tuner sans prétendre reproduire son algorithme propriétaire.

### Benchmark scientifique comparatif

Constituer une bibliothèque de cas industriels reproductibles.

Comparer ORIAS aux solutions existantes lorsque les logiciels et licences nécessaires sont disponibles.

À défaut, utiliser des publications scientifiques, des implémentations indépendantes et des résultats analytiques vérifiables.

Comparer :

- La précision de l’identification.
- La qualité de prédiction.
- Les incertitudes.
- La robustesse des modèles.
- La stabilité des régulations.
- La performance des réglages.
- Le rejet des perturbations.
- L’effort de commande.
- Le temps de calcul.
- La capacité à refuser des données inadéquates.
- La simplicité d’utilisation.

Définir les critères et les tolérances avant les benchmarks.

Utiliser les mêmes données, hypothèses et contraintes pour les comparaisons.

Ne pas sélectionner uniquement les situations favorables à ORIAS.

Documenter les situations où ORIAS est comparable, supérieur, inférieur ou non comparable aux références étudiées.

### Diagnostic industriel approfondi

Étudier la capacité à distinguer :

- Un mauvais réglage PID.
- Un défaut de mesure.
- Un actionneur saturé.
- Une vanne présentant du frottement ou de l’hystérésis.
- Une perturbation externe.
- Une variation de la dynamique du procédé.
- Un problème d’échantillonnage.
- Un modèle inadéquat.

Ne pas recommander systématiquement un nouveau réglage PID lorsqu’un défaut matériel ou une autre cause peut expliquer le mauvais comportement de la régulation.

Ne pas attribuer une cause certaine lorsque les données ne permettent pas de la démontrer.

### Critère de compétitivité

Ne jamais déclarer ORIAS équivalent ou supérieur à un logiciel concurrent sans preuves comparatives vérifiables.

La pertinence scientifique, la robustesse, la simplicité d’utilisation et la sécurité industrielle priment sur le nombre de fonctionnalités.

Intégrer ces exigences dans le LOT 0 et les lots scientifiques concernés.

### Discipline de démarrage

Avant toute modification du moteur scientifique, identifier et figer le commit de référence UI/UX réceptionné.

Vérifier la compilation Windows, les tests existants et l’absence de modifications scientifiques involontaires pendant la refonte.

Créer une branche dédiée au développement scientifique.

Commencer exclusivement par le LOT 0.

Présenter les conclusions de l’audit avant toute modification majeure des algorithmes.

---

# ORIAS / MKR

# CAHIER DES CHARGES DIRECTEUR — MOTEUR SCIENTIFIQUE INTELLIGENT ET PARCOURS COMPLET D’INGÉNIERIE DE LA RÉGULATION INDUSTRIELLE

## 1. IDENTITÉ ET CONTEXTE DU PROJET

Nom commercial : ORIAS

Signification : Optimisation de la Régulation Industrielle et Assistance aux Systèmes asservis.

Slogan : De la mesure à la maîtrise du procédé.

Nom technique : MKR.

Dépôt GitHub : https://github.com/Mcodea/mkr

Architecture technique existante :

- C# / .NET / WPF / MVVM.
- Python scientifique.
- SQLite.
- Application Windows autonome.
- Communication avec les automates industriels en lecture seule.

La phase de refonte UI/UX et d’intégration de l’identité commerciale ORIAS précède cette phase scientifique.

Préserver les écrans validés, le design system, les thèmes clair et sombre, les ressources graphiques, l’identité commerciale et l’architecture technique existante.

Ne jamais renommer le dépôt GitHub Mcodea/mkr.

Ne pas engager une nouvelle refonte générale de l’interface.

Pour des taches complexes bascules en opus 5.5 sinon le mode par défaut c’est sonnet 5.5

---

## 2. RÔLE ET MISSION

Agis comme une équipe pluridisciplinaire de recherche et développement composée :

- D’un expert senior en automatique et théorie de la commande.
- D’un ingénieur spécialisé en identification des systèmes dynamiques.
- D’un ingénieur procédés industriels.
- D’un spécialiste en régulation P, PI, PID et stratégies avancées.
- D’un spécialiste en optimisation numérique.
- D’un ingénieur Python scientifique.
- D’un architecte logiciel C#/.NET/WPF.
- D’un expert en automates Schneider, Siemens, Rockwell et autres constructeurs.
- D’un spécialiste en acquisition et communication industrielle.
- D’un ingénieur indépendant chargé de la vérification et de la validation scientifique.
- D’un ingénieur qualité logiciel.

Ta mission consiste à poursuivre le développement d’ORIAS afin d’obtenir un logiciel industriel complet, intelligent, fiable, explicable, reproductible et progressivement qualifiable.

ORIAS doit accompagner un automaticien ou un ingénieur procédés depuis la création d’une étude jusqu’à la préparation de la validation industrielle du réglage.

Ne développe pas de fonctionnalités isolées sans les intégrer au parcours global.

La priorité est la pertinence industrielle et la rigueur scientifique, et non le nombre de fonctionnalités ou de lignes de code.

---

## 3. VISION FONDAMENTALE DU LOGICIEL

ORIAS doit être capable de transformer automatiquement des données de procédé exploitables en une recommandation de régulation scientifiquement justifiée.

Le logiciel doit notamment déterminer :

- Si les données sont exploitables.
- Si des mesures complémentaires sont nécessaires.
- Quels modèles mathématiques sont admissibles.
- Quelles méthodes d’identification sont adaptées.
- Quel modèle représente suffisamment bien le procédé.
- Quelle stratégie de régulation est pertinente.
- Quelles méthodes de réglage sont applicables.
- Quels paramètres offrent un compromis approprié.
- Si les réglages sont suffisamment robustes.
- Si le bloc constructeur est compatible.
- Quelles conditions restent à vérifier sur l’installation.

L’utilisateur ne doit pas être obligé de maîtriser les méthodes FOPDT, SOPDT, Broïda, IMC, SIMC, les fonctions de transfert ou les critères de stabilité pour exploiter le logiciel.

Le moteur scientifique doit prendre les décisions lorsqu’elles peuvent être justifiées.

Il doit être capable de refuser une recommandation lorsqu’une preuve essentielle est insuffisante.

L’intelligence d’ORIAS doit reposer sur des méthodes scientifiques, des algorithmes numériques, des règles explicites et des mécanismes d’optimisation.

Une intelligence artificielle générative ne doit jamais remplacer les calculs, les contrôles scientifiques ou les mécanismes de validation.

---

## 4. AUDIT INITIAL DU DÉPÔT

Avant toute modification :

1. Examiner main, les branches pertinentes, notamment celles contenant les derniers développements, et les pull requests.
2. Identifier la dernière révision de référence intégrant la phase UI/UX réceptionnée.
3. Lire MKR-SPEC-001, AGENTS.md, les ADR et les documents de développement.
4. Examiner l’architecture C# et Python.
5. Examiner les contrats d’échange entre composants.
6. Examiner la base SQLite.
7. Examiner les modules d’acquisition CSV, Excel, Modbus TCP, S7 et autres protocoles existants.
8. Examiner les modules d’identification.
9. Examiner les méthodes de réglage IMC, Lambda et SIMC.
10. Examiner les simulations.
11. Examiner le catalogue et les adaptateurs constructeurs.
12. Examiner les rapports et la traçabilité.
13. Examiner les tests et les résultats GitHub Actions.
14. Identifier les équations, hypothèses, seuils et paramètres codés en dur.
15. Identifier les fonctions réellement opérationnelles, partielles ou non intégrées.
16. Identifier les risques scientifiques et les risques de régression.

Produire une matrice de traçabilité comprenant :

- Exigence.
- Composant existant.
- État actuel.
- Référence scientifique.
- Tests disponibles.
- Preuves disponibles.
- Écarts.
- Priorité.
- Action nécessaire.

Distinguer clairement les fonctions :

- Implémentées.
- Compilées.
- Testées.
- Vérifiées indépendamment.
- Qualifiées dans un domaine précis.
- Non qualifiées.

Ne pas réécrire inutilement les composants fiables.

---

## 5. RÉFÉRENTIEL SCIENTIFIQUE OBLIGATOIRE

Avant toute modification d’un algorithme, rechercher et examiner les références scientifiques pertinentes.

S’appuyer notamment sur :

- Lennart Ljung — System Identification: Theory for the User.
- Torsten Söderström et Petre Stoica — System Identification.
- Karl J. Åström et Tore Hägglund — Advanced PID Control.
- Karl J. Åström et Richard M. Murray — Feedback Systems.
- Sigurd Skogestad — Simple analytic rules for model reduction and PID controller tuning.
- Rivera, Morari et Skogestad — Internal Model Control: PID Controller Design.
- Publications de Grimholt et Skogestad concernant l’optimisation et la robustesse des réglages PI/PID.
- Publications scientifiques complémentaires adaptées aux procédés étudiés.

Étudier les bibliothèques scientifiques pertinentes :

- NumPy.
- SciPy.
- Python Control.
- SIPPY.
- SysIdentPy.
- Autres bibliothèques scientifiquement pertinentes.

Vérifier leurs licences, leurs versions, leurs hypothèses, leur maintenance et leurs domaines d’application.

Rechercher des données expérimentales publiques, notamment :

- DaISy — Database for the Identification of Systems.
- Nonlinear Benchmark.
- TCLab.
- Autres jeux expérimentaux documentés et légalement exploitables.

Ne pas considérer qu’un jeu de données est adapté simplement parce qu’il est public. Vérifier la nature des signaux, les conditions expérimentales et sa pertinence pour le modèle étudié.

Étudier les fonctionnalités et les approches publiquement documentées des logiciels industriels existants :

- Control Station LOOP-PRO Tuner.
- Valmet ExperTune PID Loop Optimizer.
- MATLAB System Identification Toolbox.
- MATLAB PID Tuner.
- Siemens PID Tuner.
- Autres solutions industrielles pertinentes.

S’inspirer de leurs bonnes pratiques fonctionnelles et scientifiques sans prétendre connaître leurs algorithmes propriétaires.

Développer une implémentation propre à ORIAS en respectant les licences et la propriété intellectuelle.

Pour chaque méthode scientifique, produire une fiche comprenant :

- Référence bibliographique exacte.
- Auteur.
- Publication.
- Année.
- DOI ou lien lorsque disponible.
- Équations.
- Hypothèses.
- Unités.
- Domaine d’application.
- Limites.
- Méthode numérique.
- Critères de validation.
- Tests indépendants.
- Résultats de vérification.

Ne jamais inventer une référence, une équation ou une preuve scientifique.

---

## 6. ARCHITECTURE CIBLE DU MOTEUR

Organiser le logiciel autour de composants aux responsabilités clairement séparées :

1. Gestion des projets et études.
2. Acquisition et historisation.
3. Qualification des données.
4. Identification des procédés.
5. Validation des modèles.
6. Sélection des stratégies.
7. Calcul des réglages.
8. Optimisation multicritère.
9. Simulation.
10. Évaluation de la robustesse.
11. Catalogue des contrôleurs.
12. Adaptateurs constructeurs.
13. Préparation de la mise au point.
14. Rapports.
15. Qualification scientifique.
16. Orchestration métier.

Préserver les composants existants lorsqu’ils satisfont ces responsabilités.

Les modules scientifiques doivent pouvoir être testés indépendamment de l’interface WPF.

Les échanges C# / Python doivent reposer sur des contrats explicites, versionnés et testés.

Prévoir des résultats structurés comprenant les valeurs, les unités, les hypothèses, les avertissements, les erreurs, les limites et les preuves.

Ne pas mélanger les calculs scientifiques et la présentation graphique.

---

## 7. PARCOURS UTILISATEUR COMPLET

ORIAS doit guider l’utilisateur dans le parcours suivant :

Création de l’étude → Acquisition → Diagnostic → Identification → Validation du modèle → Sélection de la stratégie → Calcul des réglages → Simulation et optimisation → Adaptation constructeur → Préparation de la mise au point → Validation humaine → Rapport.

Ce parcours doit être intégré dans les écrans existants.

Il ne doit pas imposer une succession d’étapes inutiles lorsque certaines informations sont déjà disponibles.

Le moteur d’orchestration doit déterminer les étapes nécessaires selon la situation réelle de l’étude.

---

## 8. CRÉATION DE L’ÉTUDE

Permettre à l’utilisateur de définir :

- Nom du projet.
- Nom de l’étude.
- Type de procédé.
- Objectif de régulation.
- Grandeur régulée PV.
- Consigne SP.
- Grandeur manipulée MV.
- Actionneur.
- Capteur.
- Perturbations connues.
- Unités.
- Plages de mesure.
- Plages de commande.
- Limites d’exploitation.
- Conditions initiales.
- Contraintes de sécurité.
- Automate.
- Bloc de régulation, lorsqu’il est connu.
- Paramètres de régulation existants, lorsqu’ils sont disponibles.

Les familles de procédés prioritaires sont :

- Pression.
- Débit.
- Niveau.
- Température.
- Dosage chimique.
- Chloration.
- Procédés hydrauliques.
- Autres procédés compatibles avec les méthodes qualifiées.

Ne pas imposer la saisie de données inutiles.

Distinguer les informations obligatoires, facultatives et inconnues.

Ne jamais inventer les caractéristiques physiques manquantes.

Demander uniquement les informations indispensables à la pertinence des calculs.

---

## 9. OBJECTIFS UTILISATEUR SIMPLES

L’utilisateur doit pouvoir exprimer ses préférences dans un langage industriel compréhensible.

Proposer, lorsque cela est pertinent :

- Réglage équilibré — recommandé par défaut.
- Privilégier la stabilité.
- Privilégier la réactivité.
- Préserver les équipements.
- Améliorer le rejet des perturbations.

Ces préférences sont facultatives.

L’utilisateur ne doit pas être obligé de définir :

- Un temps de réponse cible.
- Une marge de phase.
- Une marge de gain.
- Une valeur Lambda.
- Une fonction de coût.
- Une pondération mathématique.
- Un critère de robustesse numérique.

Le moteur doit traduire les préférences simples en objectifs scientifiques documentés et testés.

Ne pas introduire de correspondance arbitraire entre une préférence utilisateur et une valeur scientifique.

Une préférence utilisateur ne doit jamais permettre de contourner une contrainte impérative de sécurité, de stabilité ou d’admissibilité.

Le fonctionnement automatique constitue le parcours normal.

Les paramètres scientifiques détaillés peuvent être accessibles dans un panneau expert facultatif, sans créer un parcours parallèle complexe.

---

## 10. ACQUISITION DES DONNÉES

Consolider les trois modes d’acquisition.

### Mode A — Import de fichiers

Permettre l’importation des formats pris en charge, notamment CSV et Excel.

Gérer :

- Horodatages.
- Séparateurs.
- Formats numériques.
- Unités.
- Correspondance des colonnes.
- Données manquantes.
- Sélection des signaux.
- Aperçu graphique.
- Historique de l’import.

Ne pas modifier silencieusement les données originales.

### Mode B — Acquisition et historisation

Permettre :

- La sélection du contrôleur.
- La configuration de la connexion.
- La sélection des variables.
- La configuration de la période d’acquisition.
- La visualisation des tendances.
- L’enregistrement des données.
- Le suivi de la connexion.
- L’export des mesures.

Lorsque les informations disponibles sont suffisantes, estimer la durée d’acquisition nécessaire à l’identification du procédé.

Réévaluer cette estimation selon :

- La dynamique observée.
- Le temps mort estimé.
- La durée de réponse.
- Le niveau d’excitation.
- La qualité des mesures.

Distinguer impérativement :

- La progression temporelle de l’acquisition.
- La disponibilité des données.
- Leur exploitabilité scientifique.

Une acquisition terminée ne signifie pas automatiquement que les données sont exploitables.

Ne pas présenter une durée prévisionnelle comme certaine lorsque la dynamique du procédé est inconnue.

### Mode C — Assistance à la mise au point en direct

Permettre l’observation des variables en direct et l’analyse progressive des données disponibles.

Afficher en permanence :

- L’état réel de la communication.
- La dernière lecture.
- La fraîcheur des données.
- La période effective d’échantillonnage.
- Les erreurs.
- Les interruptions.
- Les pertes de communication.
- Les données potentiellement obsolètes.

Ne pas imposer systématiquement un historique prolongé.

Les trois modes doivent partager un format interne de données commun.

Toutes les communications industrielles restent strictement en lecture seule.

---

## 11. QUALIFICATION SCIENTIFIQUE DES DONNÉES

Avant toute identification, analyser :

- Présence des signaux nécessaires.
- Cohérence des horodatages.
- Période d’échantillonnage.
- Échantillonnage irrégulier.
- Données manquantes.
- Valeurs aberrantes.
- Bruit.
- Saturations.
- Blocages de signaux.
- Changements de mode.
- Perturbations.
- Conditions initiales.
- Variations du point de fonctionnement.
- Niveau d’excitation.
- Durée exploitable.
- Qualité de la dynamique observée.

Distinguer les données obtenues :

- En boucle ouverte.
- En boucle fermée.
- Pendant un essai dynamique.
- Pendant le fonctionnement normal du procédé.

En boucle fermée, traiter explicitement les risques de biais liés à la rétroaction et aux corrélations entre commande, perturbations et bruit.

Ne pas appliquer indistinctement les mêmes estimateurs aux données en boucle ouverte et fermée.

Préserver les données brutes.

Documenter tous les prétraitements, filtrages, exclusions et transformations.

Le diagnostic doit produire un résultat explicite :

- Données exploitables.
- Données exploitables sous conditions.
- Données à compléter.
- Données insuffisantes.
- Identification bloquée.

Expliquer les motifs et, lorsque cela est possible, indiquer quelles données complémentaires sont nécessaires.

Ne pas introduire de seuils scientifiques arbitraires.

---

## 12. BIBLIOTHÈQUE DES MODÈLES SCIENTIFIQUES

Construire une bibliothèque extensible de modèles.

### Périmètre prioritaire

- FOPDT : premier ordre avec temps mort.
- SOPDT : deuxième ordre avec temps mort.
- Modèles intégrateurs avec ou sans temps mort, lorsque qualifiés.

### Extensions progressives

- Modèles d’ordre supérieur.
- ARX.
- OE.
- Box-Jenkins.
- Modèles à réponse inverse.
- Modèles instables.
- Modèles non linéaires.
- Modèles multivariables.

Ne pas développer toutes ces familles simultanément.

Pour chaque famille, définir :

- Équations.
- Paramètres.
- Unités.
- Domaine de validité.
- Préconditions.
- Méthodes d’estimation admissibles.
- Critères de validation.
- Limites.
- Tests scientifiques.
- Compatibilité avec les méthodes de réglage.

Les modèles non qualifiés ne doivent pas être proposés automatiquement.

---

## 13. BIBLIOTHÈQUE DES MÉTHODES D’IDENTIFICATION

Distinguer rigoureusement le modèle mathématique et la méthode utilisée pour estimer ses paramètres.

Étudier et qualifier selon leur pertinence :

- Méthode de Broïda.
- Méthodes graphiques de réponse indicielle.
- Moindres carrés linéaires.
- Moindres carrés non linéaires.
- Minimisation de l’erreur de prédiction.
- Optimisation numérique sous contraintes.
- Méthodes robustes aux valeurs aberrantes.
- Méthodes adaptées aux données en boucle fermée.
- Autres méthodes reconnues et documentées.

Broïda ne doit pas être présenté comme une famille de modèles indépendante du FOPDT.

Vérifier :

- Les conditions d’application.
- La convergence numérique.
- L’identifiabilité des paramètres.
- La sensibilité aux valeurs initiales.
- La cohérence physique.
- Les contraintes numériques.
- Les unités.
- Les incertitudes.
- La reproductibilité.

Ne pas considérer une convergence numérique comme une preuve de validité scientifique.

Ne pas ajouter une méthode simplement pour augmenter le nombre d’algorithmes disponibles.

---

## 14. SÉLECTION AUTOMATIQUE DU MODÈLE ET DE LA MÉTHODE D’IDENTIFICATION

Le moteur doit sélectionner les couples modèle-méthode admissibles à partir des caractéristiques des données.

Pour chaque candidat :

1. Vérifier les préconditions.
2. Estimer les paramètres.
3. Vérifier la convergence.
4. Vérifier la cohérence physique.
5. Évaluer les erreurs.
6. Analyser les résidus.
7. Évaluer les incertitudes.
8. Vérifier le domaine de validité.
9. Vérifier la qualité de prédiction.
10. Accepter ou rejeter le candidat avec justification.

Présenter, lorsque pertinent :

- Gain du procédé.
- Constantes de temps.
- Temps mort.
- Conditions initiales.
- Courbes mesurées.
- Courbes prédites.
- Résidus.
- Erreurs.
- Hypothèses.
- Incertitudes.
- Domaine de validité.

Conserver une possibilité de sélection manuelle en mode expert.

Une sélection manuelle ne doit pas permettre de contourner les critères de validation.

---

## 15. VALIDATION SCIENTIFIQUE ET COMPARAISON DES MODÈLES

Comparer les modèles selon des critères scientifiquement justifiés.

Utiliser lorsque leur application est pertinente :

- RMSE.
- MAE.
- NRMSE/FIT.
- Analyse des résidus.
- Autocorrélation.
- Corrélations entre résidus et entrées adaptées au contexte expérimental.
- AIC.
- AICc.
- BIC.
- Incertitudes des paramètres.
- Cohérence physique.
- Complexité.
- Qualité de prédiction indépendante.
- Pertinence pour la conception du régulateur.

Ne jamais sélectionner un modèle uniquement parce qu’il possède le meilleur FIT.

Privilégier le modèle suffisamment précis, physiquement cohérent et adapté à la régulation étudiée.

Les modèles doivent être comparés sur des bases réellement comparables.

Séparer les données d’estimation, de sélection et de qualification finale lorsque nécessaire.

Utiliser des données indépendantes pour la validation lorsque les méthodes et le contexte l’exigent.

Éviter toute fuite de données entre estimation et validation.

Ne pas effectuer de séparation aléatoire naïve des séries temporelles.

Lorsque les données indépendantes nécessaires sont indisponibles, distinguer explicitement une identification exploratoire d’un modèle validé.

Si aucun candidat ne satisfait les critères nécessaires, bloquer les recommandations de réglage.

Expliquer pourquoi le modèle est retenu ou rejeté.

---

## 16. SÉLECTION AUTOMATIQUE DE LA STRATÉGIE DE RÉGULATION

Le moteur doit déterminer les stratégies admissibles selon :

- Type de procédé.
- Dynamique.
- Gain.
- Temps mort.
- Constantes de temps.
- Caractère intégrateur.
- Bruit.
- Perturbations.
- Incertitudes.
- Contraintes de l’actionneur.
- Conditions d’exploitation.
- Fonctionnalités du contrôleur industriel.

Comparer initialement :

- P.
- PI.
- PID.

Étudier progressivement :

- Cascade.
- Feedforward.
- Régulation de rapport.
- Split-range.
- Override.
- Gain scheduling.
- Prédicteur de Smith.
- MPC.
- Autres stratégies documentées.

Ne pas supposer qu’un procédé nécessite systématiquement un PI, un PID ou un prédicteur de Smith.

Une stratégie avancée ne doit être proposée que lorsque :

- Son intérêt est démontré.
- Les données sont suffisantes.
- L’architecture de commande est compatible.
- Les fonctionnalités constructeur sont disponibles.
- La méthode est vérifiée.
- Les limites sont explicites.

La recommandation doit présenter les avantages, les inconvénients et les raisons de la sélection.

---

## 17. CALCUL DES PARAMÈTRES DE RÉGULATION

Consolider et vérifier les méthodes existantes, notamment :

- IMC.
- Lambda.
- SIMC.

Vérifier précisément leurs équations, leurs différences, leurs variantes, leurs hypothèses et leurs domaines d’application.

Étudier d’autres méthodes reconnues uniquement lorsqu’elles apportent un intérêt démontrable.

Pour chaque couple modèle-stratégie admissible :

- Générer plusieurs réglages candidats.
- Vérifier les conditions d’application.
- Respecter la structure mathématique du régulateur.
- Calculer les paramètres.
- Évaluer les performances.
- Évaluer la robustesse.
- Rejeter les candidats incompatibles.

Présenter selon le type de régulateur :

- Kp.
- Ti.
- Td.
- Filtrage dérivé.
- Structure mathématique.
- Méthode de réglage.
- Unités.
- Sens d’action.
- Hypothèses.
- Limites.

Ne pas calculer de recommandation exploitable industriellement lorsque les critères préalables de validation ne sont pas satisfaits.

Les formules et références scientifiques doivent rester accessibles à l’utilisateur expert.

---

## 18. MOTEUR INTELLIGENT D’OPTIMISATION DES RÉGLAGES

Développer un moteur capable de rechercher automatiquement les réglages les plus appropriés dans le domaine qualifié.

Ne pas limiter ORIAS à l’application d’une formule IMC ou SIMC avec une valeur Lambda arbitraire.

Lorsque les méthodes sont suffisamment qualifiées, explorer les paramètres de réglage admissibles et optimiser les résultats sous contraintes.

Évaluer les compromis entre :

- Stabilité.
- Robustesse.
- Rapidité.
- Précision.
- Rejet des perturbations.
- Effort de commande.
- Variations de commande.
- Saturations.
- Préservation des équipements.
- Contraintes d’exploitation.

Étudier les approches multicritères et les compromis de Pareto lorsque cela est pertinent.

Ne pas introduire de fonctions de coût ou de coefficients arbitraires.

Les pondérations, limites et règles de sélection doivent être justifiées, documentées et testées.

Une réponse plus rapide ne doit jamais être considérée automatiquement comme meilleure.

Le moteur doit être capable de présenter :

- Un réglage recommandé.
- Les alternatives admissibles.
- Les compromis associés.
- Les raisons du rejet des autres candidats.
- Les limites de la recommandation.

Si plusieurs solutions sont raisonnablement équivalentes, ne pas inventer une supériorité artificielle.

L’utilisateur doit pouvoir comprendre la recommandation sans devoir analyser lui-même tous les résultats mathématiques.

---

## 19. SIMULATION ET ÉVALUATION DE LA ROBUSTESSE

Développer ou consolider la simulation des réglages candidats.

Distinguer :

- Simulation du procédé.
- Simulation du régulateur.
- Simulation de la boucle fermée.
- Simulation du comportement du bloc constructeur lorsque qualifié.

Évaluer selon les situations :

- Réponse à la consigne.
- Rejet des perturbations.
- Variations du gain.
- Variations des constantes de temps.
- Variations du temps mort.
- Bruit de mesure.
- Saturations.
- Limitations de vitesse de l’actionneur.
- Échantillonnage.
- Filtrage.
- Conditions initiales.
- Incertitudes du modèle.

Calculer lorsque leur domaine d’application le permet :

- Temps de montée.
- Temps de réponse.
- Temps d’établissement.
- Dépassement.
- Erreur statique.
- IAE.
- ISE.
- ITAE.
- Marges de gain.
- Marges de phase.
- Pic de sensibilité Ms.
- Effort de commande.
- Variations de commande.
- Durée de saturation.

Ne pas conclure à la robustesse sur la seule base de quelques simulations nominales.

Vérifier la précision numérique des simulations, notamment lorsque le temps mort, la période d’échantillonnage ou les dynamiques rapides peuvent influencer les résultats.

Distinguer clairement :

- Analyse théorique.
- Simulation nominale.
- Simulation sous incertitudes.
- Essai sur banc.
- Essai industriel réel.

Ne pas prétendre démontrer une stabilité globale lorsque seules des conditions locales ont été étudiées.

---

## 20. COMPARAISON AVEC LE RÉGLAGE EXISTANT

Lorsque les paramètres de régulation actuellement utilisés sont disponibles :

- Identifier la structure du régulateur existant.
- Vérifier les conventions constructeur.
- Reproduire son comportement lorsque cela est possible.
- Comparer le réglage existant et les candidats ORIAS dans des conditions identiques.
- Quantifier les améliorations potentielles.
- Présenter les compromis.
- Identifier les risques d’une modification.

Distinguer les gains calculés, simulés et réellement mesurés.

Ne pas inventer une situation de référence lorsque le réglage existant est inconnu.

Ne pas annoncer de gains énergétiques, économiques ou de durée de vie sans données et modèle permettant de les justifier.

Lorsque l’installation ne dispose pas encore de régulation, ORIAS peut évaluer l’intérêt potentiel d’une stratégie de régulation uniquement si les données et hypothèses disponibles permettent cette comparaison.

---

## 21. CATALOGUE DES CONSTRUCTEURS ET ADAPTATEURS

Créer une représentation mathématique interne indépendante des constructeurs.

Développer des adaptateurs spécifiques aux blocs réellement documentés.

### Priorité initiale

Schneider Electric :

- Modicon M340.
- Modicon M580.
- EcoStruxure Control Expert.
- Blocs de régulation effectivement disponibles et documentés.

### Extensions ultérieures

- Siemens S7-1200 et S7-1500.
- Rockwell ControlLogix et CompactLogix.
- ABB.
- Beckhoff.
- Autres constructeurs pertinents.

Pour chaque bloc, vérifier :

- Constructeur.
- Famille automate.
- Référence exacte du bloc.
- Version du logiciel constructeur.
- Version de bibliothèque.
- Structure mathématique.
- Forme parallèle, série ou autre.
- Convention du gain.
- Unités.
- Temps intégral.
- Temps dérivé.
- Sens d’action.
- Période d’exécution.
- Filtrage.
- Saturations.
- Anti-windup.
- Tracking.
- Initialisation.
- Modes de fonctionnement.
- Transitions manuel/automatique.

Documenter les conversions avec les manuels constructeurs officiels.

Créer des tests de référence indépendants.

Ne jamais supposer que deux blocs PID sont mathématiquement équivalents.

Distinguer :

- Bloc documenté.
- Conversion implémentée.
- Conversion vérifiée numériquement.
- Conversion vérifiée sur banc.
- Conversion non qualifiée.

Ne produire des paramètres constructeur exploitables que lorsque leur conversion est suffisamment documentée et vérifiée pour le domaine concerné.

En cas d’incertitude, afficher explicitement :

« Conversion non qualifiée ».

Ne jamais masquer une incompatibilité.

---

## 22. PRÉPARATION DE LA MISE AU POINT INDUSTRIELLE

Générer une fiche de préparation des essais comprenant :

- Identification du procédé.
- Automate.
- Bloc de régulation.
- Paramètres actuels lorsqu’ils sont connus.
- Paramètres candidats.
- Unités.
- Sens d’action.
- Prérequis.
- Conditions d’exploitation.
- Contraintes de sécurité.
- Limites de commande.
- Points de surveillance.
- Critères d’acceptation.
- Conditions d’arrêt de l’essai.
- Procédure de retour aux réglages précédents.
- Contrôles à réaliser.
- Validation humaine requise.

La fiche doit distinguer les informations connues, calculées, recommandées et restant à confirmer sur site.

Ne pas présenter une procédure générique comme une procédure de sécurité validée pour une installation particulière.

La mise au point doit rester sous la responsabilité des personnes habilitées.

ORIAS ne doit jamais écrire automatiquement les paramètres dans l’automate.

---

## 23. ASSISTANCE À LA VALIDATION INDUSTRIELLE

Permettre à l’utilisateur d’enregistrer les résultats des essais réalisés sous contrôle humain.

Lorsque des données après réglage sont disponibles :

- Comparer le comportement observé et le comportement prévu.
- Évaluer les écarts.
- Vérifier les critères d’acceptation définis.
- Présenter les résultats de performance.
- Identifier les limites et les anomalies.
- Proposer une nouvelle analyse lorsque nécessaire.

Distinguer impérativement les statuts :

- Résultat calculé.
- Résultat vérifié numériquement.
- Modèle validé sur données indépendantes.
- Réglage évalué en simulation.
- Conversion constructeur vérifiée.
- Essai réalisé sur banc.
- Essai réalisé sur installation.
- Validation humaine enregistrée.

Une simulation réussie ne constitue pas une qualification industrielle.

La validation finale ne doit jamais être automatiquement attribuée par le logiciel sur la seule base de calculs théoriques.

---

## 24. RAPPORT FINAL D’INGÉNIERIE

Produire un rapport professionnel, traçable et reproductible.

Le rapport doit comprendre :

- Identification du projet et de l’étude.
- Description du procédé.
- Objectifs de régulation.
- Origine des données.
- Conditions d’acquisition.
- Qualité des mesures.
- Prétraitements effectués.
- Méthodes d’identification évaluées.
- Modèles acceptés et rejetés.
- Modèle retenu.
- Justification de la sélection.
- Paramètres du modèle.
- Incertitudes.
- Résultats de validation.
- Stratégies évaluées.
- Stratégie recommandée.
- Méthodes de réglage.
- Paramètres calculés.
- Résultats d’optimisation.
- Simulations.
- Indicateurs de performance.
- Évaluation de la robustesse.
- Comparaison avec le réglage existant lorsque possible.
- Bloc constructeur.
- Paramètres convertis.
- Limites de qualification.
- Risques et avertissements.
- Fiche de préparation de la mise au point.
- Résultats des essais industriels lorsqu’ils sont disponibles.
- Statut de validation.
- Références scientifiques.
- Versions des algorithmes.

Respecter l’identité graphique ORIAS et les modèles de rapports existants.

Ne pas inventer de résultats manquants.

---

## 25. MOTEUR CENTRAL D’ORCHESTRATION MÉTIER

Développer ou consolider un moteur central organisant automatiquement le parcours.

Le moteur doit :

1. Vérifier les prérequis.
2. Déterminer les étapes nécessaires.
3. Proposer l’action suivante.
4. Sélectionner les méthodes admissibles.
5. Vérifier les résultats.
6. Autoriser ou bloquer la progression.
7. Expliquer les décisions.
8. Permettre les retours en arrière.
9. Enregistrer les preuves.
10. Préserver le contexte de travail.
11. Gérer les erreurs.
12. Invalider les résultats dépendants lorsqu’une donnée change.

Exemples :

- Une modification des données invalide les identifications dépendantes.
- Une modification du modèle invalide les réglages et simulations associés.
- Une modification du régulateur invalide les simulations correspondantes.
- Une modification du bloc constructeur invalide la conversion associée.
- Une modification d’une limite d’exploitation impose une nouvelle vérification des résultats concernés.

Ne jamais laisser un résultat obsolète apparaître comme actuellement valide.

Les règles de décision doivent être :

- Explicites.
- Déterministes.
- Versionnées.
- Documentées.
- Testées.
- Explicables.

Aucune intelligence artificielle générative ne doit remplacer ce moteur décisionnel.

---

## 26. INTÉGRATION DANS L’INTERFACE ORIAS

Préserver les six écrans industriels et le design system réceptionnés.

Intégrer progressivement les nouvelles capacités scientifiques dans l’interface existante.

L’utilisateur doit bénéficier :

- D’un parcours clair.
- D’actions suivantes adaptées.
- De recommandations compréhensibles.
- De graphiques interactifs.
- De résultats synthétiques.
- D’explications accessibles.
- D’unités explicites.
- D’une comparaison des solutions.
- D’un accès facultatif aux détails scientifiques.

Utiliser des statuts explicites :

- Non commencé.
- En cours.
- À compléter.
- À vérifier.
- Validé pour le domaine étudié.
- Bloqué.
- Non applicable.
- Résultat obsolète.

Le moteur doit afficher les raisons des blocages et les actions possibles.

Ne pas obliger l’utilisateur à naviguer entre de multiples fenêtres.

Ne pas introduire de bouton fictif.

Préserver les thèmes clair et sombre, la navigation, la localisation français/anglais et la compatibilité avec les résolutions et mises à l’échelle Windows retenues.

Toute adaptation UI/UX rendue nécessaire par une nouvelle fonctionnalité scientifique doit rester cohérente avec l’interface validée.

---

## 27. QUALIFICATION SCIENTIFIQUE INDÉPENDANTE

Construire un véritable programme de vérification et de validation scientifique.

Utiliser plusieurs catégories de données.

### Catégorie A — Cas analytiques

Cas dont les paramètres ou résultats sont connus mathématiquement.

### Catégorie B — Données synthétiques indépendantes

Données générées par des mécanismes indépendants des fonctions testées.

### Catégorie C — Données expérimentales publiques

Jeux expérimentaux documentés, dont DaISy, TCLab ou Nonlinear Benchmark lorsque pertinents.

### Catégorie D — Essais sur banc physique

Essais réalisés sur des équipements autorisés et documentés.

### Catégorie E — Données industrielles réelles

Données provenant d’installations réelles, utilisées avec les autorisations nécessaires.

Pour chaque méthode, établir :

- Cas nominal.
- Cas limites.
- Cas dégradés.
- Cas hors domaine.
- Résultats attendus.
- Tolérances justifiées.
- Références indépendantes.
- Résultats observés.
- Écarts.
- Décision de qualification.

Tester la capacité du logiciel à refuser des données et réglages inadéquats.

Ne pas générer les résultats attendus uniquement avec les fonctions qui font l’objet des tests.

Comparer les résultats à :

- Des calculs analytiques.
- Des exemples publiés.
- Des implémentations indépendantes.
- Des logiciels de référence lorsque leurs résultats sont accessibles.
- Des mesures expérimentales.

Utiliser des jeux de qualification non employés pour calibrer les algorithmes ou les seuils.

Ne jamais modifier les critères simplement pour transformer un échec en réussite.

---

## 28. CAS SCIENTIFIQUES ET TESTS DE ROBUSTESSE

Inclure au minimum des cas adaptés au périmètre qualifié couvrant :

- Gains positifs.
- Gains négatifs.
- Procédés rapides.
- Procédés lents.
- Temps morts faibles.
- Temps morts importants.
- Procédés intégrateurs.
- Données bruitées.
- Valeurs aberrantes.
- Données manquantes.
- Échantillonnage irrégulier.
- Saturations.
- Excitation insuffisante.
- Changements de mode.
- Perturbations.
- Variations du point de fonctionnement.
- Modèles inadéquats.
- Identification en boucle ouverte.
- Identification en boucle fermée.
- Conversions constructeurs incompatibles.
- Résultats obsolètes.
- Cas devant entraîner un refus.

Utiliser des tests reproductibles.

Documenter les graines aléatoires lorsque des générations stochastiques sont employées.

Mesurer les performances numériques et les temps de calcul.

Ne pas confondre couverture logicielle et qualification scientifique.

Un grand nombre de tests réussis ne prouve pas automatiquement la pertinence industrielle des méthodes.

---

## 29. SCÉNARIO INDUSTRIEL COMPLET PRIORITAIRE

Avant d’élargir le logiciel, réaliser et vérifier intégralement un scénario de référence.

### Procédé

Régulation de pression par pompe avec variateur de vitesse et automate Schneider M580.

### Parcours attendu

Création de l’étude → Import ou acquisition → Diagnostic → Identification automatique → Validation indépendante → Sélection de la stratégie → Calcul des réglages → Optimisation → Simulation → Vérification de robustesse → Adaptation au bloc Schneider → Fiche de réglage → Rapport.

Utiliser initialement des données synthétiques de référence et des jeux expérimentaux documentés.

Évaluer les modèles FOPDT/SOPDT lorsque leurs conditions d’application sont satisfaites.

Évaluer les stratégies P, PI et PID pertinentes.

Comparer notamment IMC/Lambda et SIMC lorsque applicables.

Ne pas prédéterminer artificiellement le modèle, la stratégie ou la méthode gagnante.

Si un PI constitue la meilleure solution admissible, justifier ce choix.

La compatibilité avec un bloc Schneider particulier ne doit être déclarée qualifiée qu’après les vérifications documentaires et les essais correspondants.

Tous les résultats doivent être reproductibles.

Ne pas considérer le scénario comme terminé tant que les exigences essentielles ne sont pas satisfaites ou que les limitations restantes ne sont pas explicitement documentées.

---

## 30. SCÉNARIOS INDUSTRIELS COMPLÉMENTAIRES

Après consolidation du scénario prioritaire, étendre progressivement la qualification aux cas suivants :

1. Régulation de débit.
2. Régulation de niveau intégratrice.
3. Régulation de température.
4. Régulation de dosage chimique.
5. Chloration avec grand temps mort, par exemple 34 minutes entre l’injection et la mesure.
6. Procédé identifié en boucle fermée.
7. Installation disposant déjà d’un réglage PI/PID.
8. Historique insuffisant ou inexploitable.
9. Procédé présentant des saturations importantes.
10. Procédé hors domaine des modèles qualifiés.

Pour chaque scénario :

- Documenter le procédé.
- Documenter les données.
- Définir les hypothèses.
- Vérifier les méthodes admissibles.
- Évaluer les modèles.
- Évaluer les stratégies.
- Comparer les réglages.
- Évaluer la robustesse.
- Vérifier les conversions constructeurs.
- Documenter les résultats.
- Documenter les limites.

Ne pas élargir le périmètre simplement pour annoncer davantage de procédés compatibles.

---

## 31. TRAÇABILITÉ ET REPRODUCTIBILITÉ

Chaque recommandation doit enregistrer :

- Identifiant du projet.
- Identifiant de l’étude.
- Origine des données.
- Données utilisées.
- Transformations réalisées.
- Version des algorithmes.
- Version des bibliothèques.
- Configuration scientifique.
- Méthodes évaluées.
- Modèles acceptés et rejetés.
- Raisons des décisions.
- Paramètres calculés.
- Incertitudes.
- Résultats de simulation.
- Contraintes appliquées.
- Références scientifiques.
- Version du bloc constructeur.
- Limites de qualification.
- Statut final.

Préserver l’historique des études.

Ne pas écraser silencieusement les résultats précédents.

Les mêmes données, configurations, versions et conditions numériques doivent permettre de reproduire les résultats dans les tolérances documentées.

Préserver la compatibilité des projets existants.

Toute évolution des formats de stockage doit être versionnée, testée et accompagnée d’une stratégie de migration.

---

## 32. EXIGENCES IMPÉRATIVES DE SÉCURITÉ INDUSTRIELLE

ORIAS reste un outil d’assistance à l’ingénierie.

Il ne doit pas intervenir directement sur la commande du procédé.

Interdictions absolues dans cette version :

- Écriture de registres automate.
- Forçage.
- Modification de consigne.
- Modification des paramètres PID.
- Changement de mode.
- Commande d’actionneur.
- Téléchargement de programme.
- Déclenchement automatique d’un essai d’échelon.
- Contournement des mécanismes de sécurité existants.

Les communications industrielles doivent rester en lecture seule.

Utiliser des simulateurs et des serveurs de communication simulés pour les tests.

Ne pas se connecter à une installation réelle sans autorisation.

Prévoir des tests démontrant que les interfaces de communication ne permettent pas d’écriture.

Étudier les exigences pertinentes des normes IEC 61131, IEC 61508, IEC 61511 et IEC 62443 lorsque leur périmètre est applicable.

Ne jamais revendiquer une conformité ou une certification normative non démontrée.

La validation industrielle finale demeure humaine.

---

## 33. QUALITÉ DE L’ARCHITECTURE LOGICIELLE

Préserver une architecture :

- Modulaire.
- Maintenable.
- Extensible.
- Testable.
- Documentée.
- Versionnée.
- Compatible avec l’existant.

Éviter :

- Les duplications d’algorithmes.
- Les règles scientifiques dispersées dans les ViewModels.
- Les valeurs arbitraires codées en dur.
- Les dépendances circulaires.
- Les changements incompatibles non documentés.
- Les fonctions isolées non intégrées.
- Les mécanismes de validation opaques.

Chaque nouvelle dépendance doit être justifiée.

Vérifier les licences des composants tiers.

Préserver les performances et la réactivité de l’application.

Gérer correctement les calculs longs, leur annulation, les erreurs et la progression.

Ne pas afficher une progression fictive.

---

## 34. ORGANISATION DU DÉVELOPPEMENT PAR LOTS

Procéder dans l’ordre suivant.

### LOT 0 — Audit initial et référentiel scientifique

- Audit du dépôt.
- Cartographie des composants.
- Vérification des méthodes existantes.
- Bibliographie.
- Analyse des tests.
- Matrice de conformité.
- Définition du périmètre initial qualifiable.

### LOT 1 — Socle et orchestration

- Contrats scientifiques.
- Gestion des études.
- États du parcours.
- Dépendances entre résultats.
- Traçabilité.
- Mécanismes de blocage.

### LOT 2 — Acquisition et qualification des données

- Consolidation des trois modes.
- Format commun.
- Contrôle qualité.
- Exploitabilité scientifique.
- Boucle ouverte et boucle fermée.

### LOT 3 — Identification scientifique

- Vérification des modèles existants.
- Vérification des méthodes.
- Estimation des paramètres.
- Incertitudes.
- Tests analytiques et indépendants.

### LOT 4 — Sélection et validation des modèles

- Génération des candidats.
- Comparaison.
- Validation indépendante.
- Analyse des résidus.
- Sélection automatique.
- Refus scientifique.

### LOT 5 — Sélection des stratégies et calcul des réglages

- P, PI, PID.
- IMC/Lambda.
- SIMC.
- Règles d’admissibilité.
- Justification des recommandations.

### LOT 6 — Optimisation, simulation et robustesse

- Réglages candidats.
- Optimisation multicritère.
- Préférences utilisateur.
- Simulation sous contraintes.
- Analyse des incertitudes.
- Comparaison avec le réglage existant.

### LOT 7 — Adaptateurs constructeurs

- Catalogue documenté.
- Représentation mathématique commune.
- Conversions.
- Tests indépendants.
- Limites de qualification.

### LOT 8 — Mise au point et rapport

- Fiche d’essai.
- Comparaison avant/après lorsque possible.
- Traçabilité.
- Validation humaine.
- Rapport final.

### LOT 9 — Scénario industriel complet

- Pression.
- Pompe avec variateur.
- Schneider M580.
- Parcours de bout en bout.
- Tests d’intégration.

### LOT 10 — Qualification scientifique indépendante

- Jeux de qualification réservés.
- Cas limites.
- Cas expérimentaux.
- Comparaisons indépendantes.
- Matrice de qualification.
- Limites industrielles.

### LOT 11 — Extensions progressives

- Débit.
- Niveau.
- Température.
- Chloration.
- Boucle fermée.
- Autres constructeurs et stratégies lorsque qualifiés.

La vérification scientifique doit être réalisée dans chaque lot, et non uniquement à la fin.

Ne pas commencer plusieurs lots majeurs simultanément sans justification.

Le scénario complet doit être construit progressivement à travers les lots afin d’éviter la création de composants scientifiques déconnectés du parcours utilisateur.

---

## 35. DÉVELOPPEMENT DIRECT DANS GITHUB

Développer les fonctions autorisées directement dans :

https://github.com/Mcodea/mkr

Pour chaque lot :

1. Identifier les exigences.
2. Examiner les fichiers existants.
3. Identifier les composants réutilisables.
4. Documenter les références scientifiques.
5. Développer les modifications.
6. Ajouter les tests.
7. Exécuter les tests accessibles.
8. Vérifier les régressions.
9. Documenter les résultats.
10. Créer des commits cohérents.
11. Créer une pull request dédiée.
12. Signaler les validations impossibles.

Ne jamais fusionner automatiquement dans main.

Ne pas renommer le dépôt GitHub.

Ne pas intégrer accidentellement des modifications provenant de travaux parallèles non validés.

Utiliser comme base de travail la révision UI/UX explicitement réceptionnée.

Maintenir :

docs/DEVELOPMENT_HANDOFF.md

Ce document doit permettre la reprise après interruption de session et contenir :

- Révision de départ.
- Branche active.
- Pull requests.
- Lots terminés.
- Lots en cours.
- Derniers résultats de tests.
- Références scientifiques.
- Blocages.
- Décisions prises.
- Prochaines tâches.

Éviter de recommencer l’audit ou les travaux déjà documentés à chaque nouvelle session.

---

## 36. TESTS ET NON-RÉGRESSION

Exécuter selon les composants concernés :

- Tests unitaires Python.
- Tests unitaires C#.
- Tests des équations.
- Tests numériques.
- Tests analytiques indépendants.
- Tests des contrats C# / Python.
- Tests SQLite.
- Tests d’acquisition.
- Tests des protocoles industriels.
- Tests de sécurité lecture seule.
- Tests d’intégration.
- Tests du parcours complet.
- Tests des adaptateurs constructeurs.
- Tests de compatibilité des anciens projets.
- Tests de l’interface Windows.
- Tests de non-régression.

Préserver les tests existants.

Ne jamais supprimer une assertion ou neutraliser un test pour obtenir une campagne GitHub Actions verte.

Toute modification d’un test existant doit être justifiée.

Distinguer :

- Test réussi.
- Test échoué.
- Test non exécuté.
- Test bloqué.
- Résultat non vérifié.

Une compilation réussie ne constitue pas une validation scientifique.

---

## 37. MATRICE DE QUALIFICATION SCIENTIFIQUE

Maintenir une matrice actualisée pour chaque :

- Famille de modèles.
- Méthode d’identification.
- Méthode de réglage.
- Stratégie de régulation.
- Algorithme d’optimisation.
- Fonction de simulation.
- Indicateur scientifique.
- Bloc constructeur.
- Adaptateur.
- Cas industriel.

Pour chaque élément, enregistrer :

- Référence scientifique.
- Version.
- Domaine d’application.
- Hypothèses.
- Tests analytiques.
- Tests indépendants.
- Tests expérimentaux.
- Résultats.
- Tolérances.
- Limites.
- Anomalies connues.
- Niveau de qualification.

Utiliser des états explicites :

- Non implémenté.
- Expérimental.
- Implémenté.
- Vérifié numériquement.
- Validé sur données indépendantes.
- Vérifié sur banc.
- Qualifié dans un domaine défini.
- Non qualifié.
- Bloqué.

Ne jamais attribuer un niveau de qualification sans preuve.

---

## 38. LIVRABLES OBLIGATOIRES

Pour chaque lot, fournir :

- Liens des commits.
- Pull request.
- Liste des modifications.
- Références scientifiques.
- Équations implémentées.
- Jeux de données utilisés.
- Tests.
- Résultats GitHub Actions.
- Comparaisons indépendantes.
- Résultats numériques.
- Graphiques pertinents.
- Matrice de conformité.
- Matrice de qualification.
- Écarts résiduels.
- Limites.
- Blocages.
- Mise à jour du document DEVELOPMENT_HANDOFF.md.

Pour le scénario industriel complet, fournir également :

- Projet de démonstration reproductible.
- Fichiers de données.
- Résultats du diagnostic.
- Comparaison des modèles.
- Comparaison des méthodes de réglage.
- Paramètres recommandés.
- Simulations.
- Évaluation de la robustesse.
- Vérification du bloc constructeur.
- Fiche de préparation de la mise au point.
- Rapport final.
- Captures de l’application Windows réelle.
- Paquet Windows compilé.

Ne présenter aucune maquette ou simulation graphique comme une preuve d’exécution réelle du logiciel.

---

## 39. CRITÈRES DE RÉUSSITE

ORIAS doit démontrer qu’il est capable de conduire un automaticien dans un parcours complet et cohérent, depuis la création d’une étude jusqu’à une recommandation de réglage industriel justifiée.

Le logiciel doit expliquer :

- Quelles données sont nécessaires.
- Pourquoi certaines données sont rejetées.
- Pourquoi une méthode d’identification est utilisée.
- Pourquoi un modèle est retenu.
- Si le modèle est suffisamment validé.
- Pourquoi une stratégie est recommandée.
- Comment les paramètres sont calculés.
- Pourquoi un réglage est préféré.
- Quels compromis ont été retenus.
- Ce que démontrent les simulations.
- Quelles sont les limites de robustesse étudiées.
- Si la conversion constructeur est qualifiée.
- Quelles conditions restent à vérifier avant utilisation industrielle.

Le moteur doit démontrer sa capacité à refuser :

- Des données insuffisantes.
- Un modèle inadéquat.
- Une stratégie incompatible.
- Un réglage insuffisamment robuste.
- Une conversion constructeur non qualifiée.
- Une recommandation hors domaine.

La réception doit reposer sur des preuves scientifiques et fonctionnelles vérifiables.

Ne jamais déclarer ORIAS fiable à 100 %.

Ne jamais confondre une méthode implémentée avec une méthode qualifiée.

Définir précisément les domaines effectivement vérifiés et les limites restant ouvertes.

---

## 40. INSTRUCTION FINALE ET DISCIPLINE D’EXÉCUTION

Commence exclusivement par le LOT 0 : audit scientifique, analyse du dépôt, cartographie de l’existant et constitution du référentiel scientifique.

Ne commence pas par ajouter de nouveaux algorithmes.

Identifie d’abord :

- Ce qui existe.
- Ce qui fonctionne réellement.
- Ce qui est scientifiquement vérifié.
- Ce qui reste insuffisamment justifié.
- Ce qui doit être consolidé.
- Ce qui doit être développé.

Présente l’audit initial, le périmètre scientifique proposé et le plan de développement priorisé avant toute modification majeure des algorithmes.

Après validation de l’audit, poursuivre les lots successivement, en respectant les critères de qualification.

Ne pas demander une validation intermédiaire pour chaque décision technique mineure.

Demander une intervention humaine lorsqu’une décision majeure, une modification du périmètre, un risque scientifique ou un blocage industriel le nécessite.

Ne pas élargir le périmètre lorsqu’un lot présente encore des défauts scientifiques bloquants.

Ne pas fusionner automatiquement dans main.

Ne pas inventer de résultats pour masquer une limitation.

Ne pas remplacer la rigueur scientifique par des affirmations de réussite non démontrées.

Ne pas privilégier la quantité de fonctionnalités au détriment de la qualité.

L’objectif final est de faire d’ORIAS un véritable logiciel professionnel d’assistance à l’ingénierie de la régulation et de l’asservissement industriel, capable de produire des recommandations pertinentes, transparentes, reproductibles et scientifiquement justifiées.

La qualité scientifique, la sécurité industrielle et la vérifiabilité des résultats priment sur la vitesse de développement.

---

*Note de transcription : dans le texte source, plusieurs listes numérotées étaient toutes numérotées « 0. » ; elles sont numérotées 1., 2., 3., … dans ce fichier (sections 4, 6, 14, 25, 30 et 35). Le contenu, l'ordre et le libellé des sections sont conservés tels que transmis ; seule la mise en forme Markdown (titres, puces) a été adaptée. Le bloc « 41. BENCHMARK SCIENTIFIQUE ET COMPÉTITIVITÉ INDUSTRIELLE » a été transmis avant la section 1 et est reproduit en tête. La section 4 cite « MKR-SPEC-001 » : ce document n'est pas dans le dépôt (voir docs/science/lot0/).*
