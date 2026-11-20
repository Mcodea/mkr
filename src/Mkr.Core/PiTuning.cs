namespace Mkr.Core;

public sealed record PiCoefficients(double Kp, double TiSeconds, double ParallelKi);
public sealed record PiComparison(string AlgorithmVersion, double ResponseSeconds, string[] Warnings,
    PiCoefficients Imc, PiCoefficients Simc);

public sealed record PiStudy(Guid Id, Guid ModelId, DateTimeOffset CalculatedAt, string EngineVersion, PiComparison Result)
{
    public string Label => $"{Result.ResponseSeconds:G5} s · {CalculatedAt.ToLocalTime():g}";
}

public interface IPiEngine
{
    Task<(PiComparison Result, string EngineVersion)> CalculatePiAsync(
        double gain, double tauSeconds, double delaySeconds, double responseSeconds, CancellationToken token);
}

public interface IPiStore
{
    IReadOnlyList<PiStudy> ListPi(Guid projectId, Guid modelId);
    void SavePi(Guid projectId, PiStudy study);
}
