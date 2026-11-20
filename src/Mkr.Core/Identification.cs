namespace Mkr.Core;

public sealed record StepColumns(string TimeColumn, string MvColumn, string PvColumn);
public sealed record StepImport(CsvImport Import, StepColumns Columns);
public sealed record FopdtPoint(double Seconds, double Observed, double Predicted);
public sealed record FitDiagnostics(int Samples, double InputStep, double Baseline, double OnsetIntervalSeconds,
    double HorizonSeconds, double Rmse, double Mae, double? RSquared, double? ResidualLagOne, FopdtPoint[] Points);
public sealed record FitStandardErrors(double Gain, double TauSeconds, double DelaySeconds);
public sealed record FopdtResult(string AlgorithmVersion, double Gain, double TauSeconds, double DelaySeconds,
    double TauLowerSeconds, double TauUpperSeconds, double DelayUpperSeconds, string[] Warnings,
    FitDiagnostics Training, FitDiagnostics Validation, FitStandardErrors? StandardErrors = null);

public sealed record FopdtStudy(Guid Id, Guid TrainingId, Guid ValidationId, StepColumns TrainingColumns,
    StepColumns ValidationColumns, string MvUnit, string PvUnit, double MvTolerance, double PvTolerance,
    DateTimeOffset CalculatedAt, string EngineVersion, FopdtResult Result)
{
    public string Label => $"FOPDT · {CalculatedAt.ToLocalTime():g} · {Id.ToString()[..8]}";
}

public interface IFopdtEngine
{
    Task<(FopdtResult Result, string EngineVersion)> IdentifyAsync(StepImport training, StepImport validation,
        double mvTolerance, double pvTolerance, CancellationToken token);
}

public interface IFopdtStore
{
    IReadOnlyList<FopdtStudy> ListFopdt(Guid projectId);
    void SaveFopdt(Guid projectId, FopdtStudy study);
}
