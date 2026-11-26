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
    public static string NoProject = Loc.T("core.selectionnez_un_projet_pour_voir");

    public static IReadOnlyList<JourneyStep> Evaluate(ProjectFacts? facts)
    {
        if (facts is null)
            return Titles.Select((title, i) => new JourneyStep(i + 1, title, StepState.Blocked, NoProject)).ToArray();

        var data = facts.Imports > 0;
        var identified = facts.Studies > 0;
        var accepted = facts.AcceptedModels > 0;
        string needModel = Loc.T("core.aucun_modele_accepte_a_la");
        return new[]
        {
            new JourneyStep(1, Titles[0], facts.HasLoopDescription ? StepState.Done : StepState.Ready,
                Loc.T("core.repere_de_la_boucle_grandeur") + (facts.HasLoopDescription ? Loc.T("core.description_enregistree") : Loc.T("core.description_a_completer"))),
            new JourneyStep(2, Titles[1], data ? StepState.Done : StepState.Ready,
                Loc.T("core.import_d_un_csv_ou") + (data ? Loc.T("core.enregistrement_s_dans_le_projet", facts.Imports) : Loc.T("core.aucun_enregistrement_dans_le_projet"))),
            new JourneyStep(3, Titles[2], data ? StepState.Done : StepState.Blocked,
                data ? Loc.T("core.qualite_temporelle_saturation_excitation_et") :
                    Loc.T("core.aucune_donnee_a_analyser_importez")),
            new JourneyStep(4, Titles[3], !data ? StepState.Blocked : identified ? StepState.Done : StepState.Ready,
                !data ? Loc.T("core.aucune_donnee_importez_ou_acquerez") :
                    identified ? Loc.T("core.etude_s_d_identification_enregistree", facts.Studies) : Loc.T("core.choisissez_un_essai_d_identification")),
            new JourneyStep(5, Titles[4], !identified ? StepState.Blocked : accepted ? StepState.Done : StepState.Ready,
                !identified ? Loc.T("core.aucune_identification_a_valider_etape") :
                    accepted ? Loc.T("core.modele_s_accepte_s_sur", facts.AcceptedModels) :
                        Loc.T("core.les_modeles_identifies_n_ont")),
            new JourneyStep(6, Titles[5], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? Loc.T("core.choix_de_la_methode_de") : needModel),
            new JourneyStep(7, Titles[6], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? Loc.T("core.catalogue_schneider_et_siemens_les") : needModel),
            new JourneyStep(8, Titles[7], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? Loc.T("core.calcul_des_parametres_et_conversion") : needModel),
            new JourneyStep(9, Titles[8], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? Loc.T("core.simulation_en_boucle_fermee_sensibilite") : needModel),
            new JourneyStep(10, Titles[9], accepted ? StepState.Ready : StepState.Blocked,
                accepted ? Loc.T("core.fiche_d_etude_pdf_les") : needModel),
        };
    }

    /// <summary>
    /// The first step the project can do now and has not completed: guidance only, derived from the same facts as the journey. Null when no step is
    /// available (no project selected, or the facts could not be read), in which case the blocked reason of the first step is the right message.
    /// </summary>
    public static JourneyStep? NextStep(IReadOnlyList<JourneyStep> steps) => steps.FirstOrDefault(step => step.State == StepState.Ready);

    public static readonly IReadOnlyList<string> Titles = new[]
    {
        Loc.T("core.configuration_du_procede"), Loc.T("core.acquisition_des_donnees"), Loc.T("core.analyse_et_qualification_des_signaux"), Loc.T("core.identification_du_modele"),
        Loc.T("core.validation_du_modele"), Loc.T("core.strategie_de_regulation"), Loc.T("core.bloc_constructeur"), Loc.T("core.calcul_des_parametres"), Loc.T("core.simulation_et_robustesse"), Loc.T("core.validation_et_rapport")
    };
}
