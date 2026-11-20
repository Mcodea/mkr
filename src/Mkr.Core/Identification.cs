namespace Mkr.Core;

public enum ModelFamily { FOPDT, SOPDT, Integrateur }

public sealed record StepColumns(string TimeColumn, string MvColumn, string PvColumn);
public sealed record StepImport(CsvImport Import, StepColumns Columns);
public sealed record FitPoint(double Seconds, double Observed, double Predicted);
public sealed record FitDiagnostics(int Samples, double InputStep, double Baseline, double OnsetIntervalSeconds,
    double HorizonSeconds, double Rmse, double Mae, double? RSquared, double? ResidualLagOne, FitPoint[] Points);
public sealed record FitStandardErrors(double Gain, double TauSeconds, double DelaySeconds);
public sealed record IdentificationResult(string AlgorithmVersion, double Gain, double? TauSeconds, double DelaySeconds,
    double? TauLowerSeconds, double? TauUpperSeconds, double DelayUpperSeconds, string[] Warnings,
    FitDiagnostics Training, FitDiagnostics Validation, FitStandardErrors? StandardErrors = null, double? SecondTauSeconds = null);

public sealed record IdentificationStudy(Guid Id, Guid TrainingId, Guid ValidationId, StepColumns TrainingColumns,
    StepColumns ValidationColumns, string MvUnit, string PvUnit, double MvTolerance, double PvTolerance,
    DateTimeOffset CalculatedAt, string EngineVersion, IdentificationResult Result, ModelFamily Family = ModelFamily.FOPDT)
{
    public string Label => $"{Family} · {CalculatedAt.ToLocalTime():g} · {Id.ToString()[..8]}";
}

public interface IIdentificationEngine
{
    Task<(IdentificationResult Result, string EngineVersion)> IdentifyAsync(ModelFamily family, StepImport training, StepImport validation,
        double mvTolerance, double pvTolerance, CancellationToken token);
}

public interface IIdentificationStore
{
    IReadOnlyList<IdentificationStudy> ListStudies(Guid projectId);
    void SaveStudy(Guid projectId, IdentificationStudy study);
}
