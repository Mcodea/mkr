namespace Mkr.Core;

// Plain-language conclusions for the user (cahier directeur §26 and the client's arbitration of 2026-11-28): the scientific complexity stays inside the engine, the user sees ONE
// conclusion, why, and what to do. Pure and deterministic: no thresholds are ever asked from the user, and no localisation happens here (codes only).

public enum AdvisorConclusion
{
    DataSufficient, ComplementaryTestNeeded, ModelUsable, ModelNotRepresentative, TuningRecommended, TuningNotRecommended, ResultObsoleteRecompute
}

public enum DataSufficiency { Unknown, Sufficient, Insufficient }
public enum ModelVerdict { NotValidatedYet, Accepted, RefusedNotRepresentative, RefusedUncertain }
public enum TuningVerdict { NotComputed, Recommended, NotRecommendedUnstable, NotRecommendedInsufficientRobustness, NotRecommendedUncertain }

/// <summary>What the engine can still try before refusing for good: an alternative model, a more robust tuning, a complementary trial.</summary>
public sealed record RecourseOptions(bool AlternativeModel, bool MoreRobustTuning, bool ComplementaryTrial);

public sealed record ConclusionInput(NodeStatus? Status, DataSufficiency Data, ModelVerdict Model, TuningVerdict Tuning, RecourseOptions Recourse);

/// <param name="Explanation">Why, as a code: obsolete_facet_changed, obsolete_upstream, data_insufficient, model_not_representative, uncertainty_too_high, tuning_unstable, robustness_insufficient, ...</param>
/// <param name="Action">What to do, as a code ; always present (a conclusion without an action is refused by the tests).</param>
public sealed record AdvisorResult(AdvisorConclusion Conclusion, string Explanation, string Action, ConfigurationFacet? Facet = null);

public static class Advisor
{
    public const string Version = "1.0.0";

    public static AdvisorResult Conclude(ConclusionInput input)
    {
        // 1. An obsolete result is never shown as current: recompute first, whatever else it says.
        if (input.Status is { Status: ResultStatus.Obsolete } obsolete)
        {
            var reason = obsolete.Reasons.FirstOrDefault();
            return new AdvisorResult(AdvisorConclusion.ResultObsoleteRecompute, reason?.Cause == ObsolescenceCause.FacetChanged ? "obsolete_facet_changed" : "obsolete_upstream",
                obsolete.Action ?? "recompute", reason?.Facet);
        }

        // 2. A tuning that is unstable, or not robust enough, is NEVER recommended, whatever the model says.
        if (input.Tuning == TuningVerdict.NotRecommendedUnstable)
            return Recourse(input.Recourse, "tuning_unstable");
        if (input.Tuning == TuningVerdict.NotRecommendedInsufficientRobustness)
            return Recourse(input.Recourse, "robustness_insufficient");

        // 3. Insufficient data: say which trial is needed.
        if (input.Data == DataSufficiency.Insufficient)
            return new AdvisorResult(AdvisorConclusion.ComplementaryTestNeeded, "data_insufficient", "run_complementary_trial");

        // 4. The model is not trusted enough: look for a way out BEFORE refusing for good (alternative model, more robust tuning, complementary trial).
        if (input.Model == ModelVerdict.RefusedNotRepresentative)
            return input.Recourse.AlternativeModel ? new AdvisorResult(AdvisorConclusion.ModelNotRepresentative, "model_not_representative", "try_alternative_model")
                                                   : input.Recourse.ComplementaryTrial ? new AdvisorResult(AdvisorConclusion.ComplementaryTestNeeded, "model_not_representative", "run_complementary_trial")
                                                   : new AdvisorResult(AdvisorConclusion.ModelNotRepresentative, "model_not_representative", "review_data_and_assumptions");
        if (input.Model == ModelVerdict.RefusedUncertain || input.Tuning == TuningVerdict.NotRecommendedUncertain)
            return Recourse(input.Recourse, "uncertainty_too_high");

        // 5. Usable model, tuning, or nothing yet.
        if (input.Model == ModelVerdict.Accepted && input.Tuning == TuningVerdict.Recommended)
            return new AdvisorResult(AdvisorConclusion.TuningRecommended, "tuning_recommended", "simulate_then_validate");
        if (input.Model == ModelVerdict.Accepted)
            return new AdvisorResult(AdvisorConclusion.ModelUsable, "model_accepted", "compute_tuning");
        if (input.Model == ModelVerdict.NotValidatedYet && input.Data == DataSufficiency.Sufficient)
            return new AdvisorResult(AdvisorConclusion.DataSufficient, "data_sufficient", "identify_model");
        return new AdvisorResult(AdvisorConclusion.DataSufficient, "start", "import_or_acquire_data");
    }

    /// <summary>The order of recourse before a definitive refusal: another model, then a more robust tuning, then a complementary trial ; only then, no recommendation.</summary>
    private static AdvisorResult Recourse(RecourseOptions options, string explanation) =>
        options.AlternativeModel ? new AdvisorResult(AdvisorConclusion.TuningNotRecommended, explanation, "try_alternative_model")
        : options.MoreRobustTuning ? new AdvisorResult(AdvisorConclusion.TuningNotRecommended, explanation, "use_more_robust_tuning")
        : options.ComplementaryTrial ? new AdvisorResult(AdvisorConclusion.ComplementaryTestNeeded, explanation, "run_complementary_trial")
        : new AdvisorResult(AdvisorConclusion.TuningNotRecommended, explanation, "no_recommendation_possible");
}
