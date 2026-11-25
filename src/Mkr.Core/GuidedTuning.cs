namespace Mkr.Core;

/// <summary>Controller coefficients in ideal form u = Kp (e + ∫e/Ti + Td de/dt), e = SP − PV. Kp has the sign of the process gain.</summary>
public sealed record TuningController(string Structure, double Kp, double TiSeconds, double TdSeconds, double FilterSeconds = 0);

/// <summary>One tuning of the SIMC family with its worst-case robustness over the declared parameter uncertainty.</summary>
public sealed record TuningCandidate(double ResponseSeconds, TuningController Controller, bool StableOnAllCorners, double WorstPeakSensitivity,
    string? WorstCase = null, double? MultipleOfEffectiveDelay = null);

/// <summary>PI that maximises the integral gain (load-disturbance rejection) under the same sensitivity constraint on every corner.</summary>
public sealed record OptimizedTuning(TuningController Controller, double IntegralGain, double WorstPeakSensitivity, double NominalOvershootPercent, double IntegralGainRatio);

/// <summary>
/// Result of the guided tuning (engine/recommend.py). The user states a robustness objective, never a response time.
/// <see cref="Decision"/> is "recommended" or "refused"; a refusal carries reasons and the actions that would allow a recommendation.
/// </summary>
public sealed record GuidedTuning(string AlgorithmVersion, string Decision, string Objective, double MaximumPeakSensitivity, string Family,
    string? Structure = null, TuningCandidate? Recommendation = null, double? UncertaintyCoverageSigma = null, bool UncertaintyQuantified = false,
    double EffectiveDelaySeconds = 0, double SamplePeriodSeconds = 0, TuningCandidate[]? Alternatives = null, string[]? Warnings = null,
    string[]? Reasons = null, string[]? Actions = null, string Qualification = "none", TuningCandidate? ClosestTuning = null, string[]? References = null,
    double? DerivativeFilterRatio = null, double? ProcessVariationPercent = null, OptimizedTuning? Optimized = null)
{
    public bool IsRecommended => Decision == "recommended";
}

public static class DesignObjectives
{
    public const string Robust = "robust", Balanced = "balanced", Performance = "performance";

    /// <summary>Maximum sensitivity peak accepted on every corner of the uncertainty set (Åström and Hägglund 2006: usual range 1.2 to 2.0).</summary>
    public static double MaximumPeakSensitivity(string objective) => objective switch { Robust => 1.4, Balanced => 1.7, Performance => 2.0, _ => throw new ArgumentException("Objectif inconnu.") };
}
