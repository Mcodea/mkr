---
name: mkr-expert-workflows
description: Concevoir les workflows guidés et productifs d'ingénierie de la régulation dans MKR, de l'import jusqu'au rapport et à la validation humaine.
---

# Workflows expert régulation — MKR

## But
Chaque écran doit servir une décision concrète de l'ingénieur et enchaîner intelligiblement vers le suivant : étude, données, qualité, modèle, réglage, simulation, bloc constructeur, validation.

1. Définir rôle, entrée, sortie, contrôle de validité et transition pour chaque étape.
2. Afficher clairement le statut : données incomplètes, qualité à revoir, modèle exploratoire, rejeté, réglage proposé, simulation calculée, décision validée.
3. Ne pas imposer un parcours linéaire rigide : l'utilisateur peut revenir corriger un mapping ou sélectionner un autre modèle sans perdre l'historique.
4. Les actions précédentes qui rendent les résultats invalides doivent marquer explicitement les résultats dépendants « à recalculer » plutôt que les conserver comme actuels.
5. Prévoir comparaison avant/après sur mêmes hypothèses, fenêtres et unités; traiter réglages existants comme donnés par l'utilisateur, non imaginés.
6. Donner accès aux hypothèses, paramètres, limites et raisons de rejet sans noyer le plan principal sous des options avancées.
7. Sauvegarde/reprise : persistance des études, choix et résultats, avec identité des données brutes, versions de modèle et méthodes.
8. Fournir un chemin de validation humaine explicite; aucune publication de réglage constructeur ni action de commande automatique.
9. Évaluer le nombre d'actions et les erreurs de parcours sur scénarios de terrain représentatifs. Ne pas inventer de score de productivité.

Livrable : spécification de parcours, critères d'acceptation UX, implémentation intégrée et cas de tests end-to-end.
