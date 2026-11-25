namespace Mkr.Core;

public enum StepState { Done, Ready, Blocked }

/// <summary>What the project actually contains. The journey is derived from these facts only, never from a stored "progress" flag.</summary>
public sealed record ProjectFacts(bool HasLoopDescription, int Imports, int Studies, int AcceptedModels);

/// <param name="Detail">What the step is, and, when blocked, why and what to do (problem · cause · action).</param>
public sealed record JourneyStep(int Number, string Title, StepState State, string Detail);

/// <summary>
/// The ten steps of an MKR study with their entry conditions. The only hard conditions are those the application already enforces:
/// data before identification, an identified model before validation, and an ACCEPTED (validated) model before any tuning, block choice,
/// simulation or report. Nothing here estimates completion: a step is Done only when a stored object proves it.
/// </summary>
public static class StudyJourney
{
    public const string NoProject = "Sélectionnez un projet pour voir les conditions de chaque étape.";

    public static IReadOnlyList<JourneyStep> Evaluate(ProjectFacts? facts)
    {
        if (facts is null)
            return Titles.Select((title, i) => new JourneyStep(i + 1, title, StepState.Blocked, NoProject)).ToArray();

        var data = facts.Imports > 0;
        var identified = facts.Studies > 0;
        var accepted = facts.AcceptedModels > 0;
        const string needModel = "Aucun modèle accepté à la validation. Un réglage sur un modèle non validé n'est pas autorisé : identifiez le procédé (étape 4) puis faites accepter le modèle (étape 5).";
        return new[]
        {
            new JourneyStep(1, Titles[0], facts.HasLoopDescription ? StepState.Done : StepState.Ready,
                "Repère de la boucle, grandeur réglée, point de fonctionnement et objectif. " + (facts.HasLoopDescription ? "Description enregistrée." : "Description à compléter.")),
            new JourneyStep(2, Titles[1], data ? StepState.Done : StepState.Ready,
                "Import d'un CSV ou acquisition en lecture seule depuis l'automate. " + (data ? $"{facts.Imports} enregistrement(s) dans le projet." : "Aucun enregistrement dans le projet.")),
            new JourneyStep(3, Titles[2], data ? StepState.Done : StepState.Blocked,
                data ? "Qualité temporelle, saturation, excitation et trous sont contrôlés à l'import et consultables." :
                    "Aucune donnée à analyser. Importez un CSV ou lancez une acquisition (étape 2)."),
            new JourneyStep(4, Titles[3], !data ? StepState.Blocked : identified ? StepState.Done : StepState.Ready,
                !data ? "Aucune donnée. Importez ou acquérez au moins un essai en échelon (étape 2)." :
                    identified ? $"{facts.Studies} étude(s) d'identification enregistrée(s)." : "Choisissez un essai d'identification et un essai de validation distinct."),
            new JourneyStep(5, Titles[4], !identified ? StepState.Blocked : accepted ? StepState.Done : StepState.Ready,
                !identified ? "Aucune identification à valider (étape 4)." :
                    accepted ? $"{facts.AcceptedModels} modèle(s) accepté(s) sur un essai indépendant." :
                        "Les modèles identifiés n'ont pas passé la validation. Consultez les critères refusés ; il faut un essai de validation plus informatif ou un autre type de modèle."),
            new JourneyStep(6, Titles[5], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? "Choix de la méthode de réglage (Lambda ou SIMC) et du type de régulateur adapté au modèle accepté." : needModel),
            new JourneyStep(7, Titles[6], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? "Catalogue Schneider et Siemens : les valeurs préparées restent « non qualifiées »." : needModel),
            new JourneyStep(8, Titles[7], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? "Calcul des paramètres et conversion vers la structure du bloc choisi." : needModel),
            new JourneyStep(9, Titles[8], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? "Simulation en boucle fermée, sensibilité, marges de stabilité, saturation." : needModel),
            new JourneyStep(10, Titles[9], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? "Fiche d'étude PDF. Les résultats simulés y sont distingués des mesures." : needModel),
        };
    }

    public static readonly IReadOnlyList<string> Titles = new[]
    {
        "Configuration du procédé", "Acquisition des données", "Analyse et qualification des signaux", "Identification du modèle",
        "Validation du modèle", "Stratégie de régulation", "Bloc constructeur", "Calcul des paramètres", "Simulation et robustesse", "Validation et rapport"
    };
}
