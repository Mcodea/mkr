namespace Mkr.Core;

public sealed record PiCoefficients(double Kp, double TiSeconds, double ParallelKi);
public sealed record PiComparison(string AlgorithmVersion, double ResponseSeconds, string[] Warnings,
    PiCoefficients Imc, PiCoefficients Simc);

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
    SeriesPid Series, IdealPid Ideal, string[] Warnings);

public interface IPiEngine
{
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
