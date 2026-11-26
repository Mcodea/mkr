namespace Mkr.Core;

/// <summary>
/// Margins of the NOMINAL loop closed by a tuning on the identified model (audit NC-17). They bound the
/// effect of model error only if read with the sensitivity analysis; they guarantee nothing for the real process.
/// </summary>
public sealed record LoopMargins(string AlgorithmVersion, double? GainCrossoverRadPerSecond, double? PhaseMarginDegrees,
    double? DelayMarginSeconds, double? PhaseCrossoverRadPerSecond, double? GainMargin, double? GainMarginDb,
    double PeakSensitivity, double PeakSensitivityRadPerSecond, string[] Warnings)
{
    public string Describe()
    {
        string Number(double? value, string unit, string format = "G4") =>
            value is { } v ? v.ToString(format, System.Globalization.CultureInfo.CurrentCulture) + unit : "—";
        var text = Loc.T("margins.phase", Number(PhaseMarginDegrees, "°", "F1")) + " · " + Loc.T("margins.gain") + " " +
            (GainMargin is null ? Loc.T("margins.infinite") : $"{Number(GainMargin, "")} ({Number(GainMarginDb, " dB", "F1")})") +
            " · " + Loc.T("margins.delay", Number(DelayMarginSeconds, " s")) + " · " + Loc.T("margins.peak", PeakSensitivity.ToString("G3", System.Globalization.CultureInfo.CurrentCulture));
        var notes = Warnings.Select(code => code switch
        {
            "nominal_unstable" => Loc.T("margins.w.unstable"),
            "low_phase_margin" => Loc.T("margins.w.phase"),
            "low_gain_margin" => Loc.T("margins.w.gain"),
            "high_sensitivity" => Loc.T("margins.w.sens"),
            "multiple_gain_crossovers" => Loc.T("margins.w.multi"),
            "no_gain_crossover" => Loc.T("margins.w.none"),
            _ => code
        }).ToArray();
        return notes.Length == 0 ? text : text + " · " + string.Join(", ", notes);
    }
}

public sealed record PiCoefficients(double Kp, double TiSeconds, double ParallelKi, LoopMargins? Margins = null);
/// <summary>ImcPublished: PI IMC of Rivera, Morari and Skogestad (1986), first-order Padé of the delay. Absent in studies saved before engine 1.2.0.</summary>
public sealed record PiComparison(string AlgorithmVersion, double ResponseSeconds, string[] Warnings,
    PiCoefficients Imc, PiCoefficients Simc, PiCoefficients? ImcPublished = null);

public sealed record PiStudy(Guid Id, Guid ModelId, DateTimeOffset CalculatedAt, string EngineVersion, PiComparison Result)
{
    public string Label => $"{Result.ResponseSeconds:G5} s · {CalculatedAt.ToLocalTime():g}";
}

public sealed record PiTransferPoint(double Seconds, string Mode, double Pv, double? Cv, double? Mv);
public sealed record PiTransferResult(string AlgorithmVersion, string[] Warnings, double TransferBumpMv,
    double SaturationSeconds, double RateLimitedSeconds, PiTransferPoint[] Points);

public sealed record SeriesPid(double Kc, double TiSeconds, double TdSeconds);
public sealed record IdealPid(double Kp, double TiSeconds, double TdSeconds);
public sealed record SopdtPid(string AlgorithmVersion, string Method, string ControllerForm, string TimeUnit,
    SeriesPid Series, IdealPid Ideal, string[] Warnings, LoopMargins? Margins = null);

/// <summary>Nominal-loop margins of an integrating process with a PI (gain and phase margins can be absent if no crossing exists).</summary>
public sealed record IntegratorMargins(double? GainMargin, double? PhaseMarginDegrees, double PeakSensitivity);

/// <summary>PI for an integrating process (SIMC rule, Skogestad 2003). Kp has the sign of the process slope.</summary>
public sealed record IntegratorPi(string AlgorithmVersion, string Rule, double ResponseSeconds, string[] Warnings,
    double Kp, double TiSeconds, double ParallelKi, IntegratorMargins Margins);

public interface IPiEngine
{
    Task<IntegratorPi> CalculateIntegratorPiAsync(double slope, double delaySeconds, double responseSeconds, CancellationToken token);

    /// <summary>Guided tuning of an ACCEPTED model for a robustness objective (see <see cref="DesignObjectives"/>), with refusal when the data do not support one.</summary>
    Task<GuidedTuning> RecommendTuningAsync(ModelFamily family, IdentificationResult model, string objective, double? processVariationPercent, CancellationToken token);

    /// <summary>Guided tuning from an ACCEPTED ARX / OE study (first-order equivalent only; refusals explained). ADR 0008.</summary>
    Task<GuidedTuning> RecommendTuningFromArxAsync(ArxStudy study, string objective, double? processVariationPercent, CancellationToken token);

    /// <summary>Documentary PIDFF conversion of a tuning (mixed structure). Never qualified, never ready to load.</summary>
    Task<BlockConversionResult> ConvertToPidffAsync(double kp, double tiSeconds, double tdSeconds,
        double? filterSeconds, CancellationToken token);

    /// <summary>Documentary conversion to PID_INT or PID_Compact; parameters are the adapter's, none has a default.</summary>
    Task<BlockSettingsResult> ConvertToBlockAsync(string block, IReadOnlyDictionary<string, object> parameters, CancellationToken token);

    Task<SopdtPid> CalculateSopdtPidAsync(IdentificationResult model, double responseSeconds, CancellationToken token);
    Task<PiTransferResult> SimulateTransferAsync(IdentificationResult model, PiCoefficients coefficients,
        PiScenario scenario, double manualMv, double automaticAtSeconds, CancellationToken token);

    Task<(PiSimulationResult Result, string EngineVersion)> SimulatePiAsync(
        IdentificationResult model, PiComparison tuning, PiScenario scenario, CancellationToken token);

    Task<(PiRobustnessResult Result, string EngineVersion)> AnalyzeRobustnessAsync(
        IdentificationResult model, PiComparison tuning, PiScenario scenario,
        double variationPercent, CancellationToken token);

    Task<(PiRobustnessResult Result, string EngineVersion)> AnalyzeCombinedAsync(
        IdentificationResult model, PiComparison tuning, PiScenario scenario,
        double variationPercent, CancellationToken token);

    Task<(PiComparison Result, string EngineVersion)> CalculatePiAsync(
        double gain, double tauSeconds, double delaySeconds, double responseSeconds, CancellationToken token);
}

public interface IPiStore
{
    IReadOnlyList<PiStudy> ListPi(Guid projectId, Guid modelId);
    void SavePi(Guid projectId, PiStudy study);
    IReadOnlyList<PiSimulationStudy> ListSimulations(Guid projectId, Guid piId);
    void SaveSimulation(Guid projectId, PiSimulationStudy study);
}
