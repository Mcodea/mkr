namespace Mkr.Core;

public enum ModelFamily { FOPDT, SOPDT, Integrateur, BroidaFOPDT }

public sealed record StepColumns(string TimeColumn, string MvColumn, string PvColumn);
/// <summary>Inclusive original CSV data-row indices (header excluded).</summary>
public sealed record StepWindow(int RowStart, int RowEnd)
{
    public bool IsValid => RowStart >= 0 && RowEnd > RowStart;
}

public sealed record StepImport(CsvImport Import, StepColumns Columns, StepWindow? Window = null);
public sealed record FitPoint(double Seconds, double Observed, double Predicted);
public sealed record FitDiagnostics(int Samples, double InputStep, double Baseline, double OnsetIntervalSeconds,
    double HorizonSeconds, double Rmse, double Mae, double? RSquared, double? ResidualLagOne, FitPoint[] Points);
public sealed record FitStandardErrors(double Gain, double TauSeconds, double DelaySeconds);
public sealed record BroidaEvidence(double Crossing28Seconds, double Crossing40Seconds,
    double TerminalPv, int TerminalSamples, string TimeOrigin, double[] ThresholdFractions);

public sealed record IdentificationResult(string AlgorithmVersion, double Gain, double? TauSeconds, double DelaySeconds,
    double? TauLowerSeconds, double? TauUpperSeconds, double DelayUpperSeconds, string[] Warnings,
    FitDiagnostics Training, FitDiagnostics Validation, FitStandardErrors? StandardErrors = null,
    double? SecondTauSeconds = null, BroidaEvidence? BroidaEvidence = null);

public sealed record IdentificationStudy(Guid Id, Guid TrainingId, Guid ValidationId, StepColumns TrainingColumns,
    StepColumns ValidationColumns, string MvUnit, string PvUnit, double MvTolerance, double PvTolerance,
    DateTimeOffset CalculatedAt, string EngineVersion, IdentificationResult Result, ModelFamily Family = ModelFamily.FOPDT,
    StepWindow? TrainingWindow = null, StepWindow? ValidationWindow = null, ModelComparison? Comparison = null)
{
    public string Label => $"{Family} · {CalculatedAt.ToLocalTime():g} · {Id.ToString()[..8]}";
}

public sealed record ModelCandidate(double ValidationRmse, double? ValidationResidualLagOne, string[] RejectionReasons);
public sealed record ComparisonCriteria(double MaximumRmse, double MaximumResidualLagOne);
public sealed record ModelComparison(string? Selected, Dictionary<string, ModelCandidate> Candidates, string Decision,
    string AlgorithmVersion, ComparisonCriteria Criteria);

public interface IIdentificationEngine
{
    Task<ModelComparison> CompareAsync(StepImport training, StepImport validation, double mvTolerance,
        double pvTolerance, double maximumRmse, double maximumResidualLagOne, CancellationToken token);

    Task<TrialPreview> PreviewTrialAsync(StepImport trial, CancellationToken token);
    Task<(IdentificationResult Result, string EngineVersion)> IdentifyAsync(ModelFamily family, StepImport training, StepImport validation,
        double mvTolerance, double pvTolerance, CancellationToken token);
}

public interface IIdentificationStore
{
    IReadOnlyList<IdentificationStudy> ListStudies(Guid projectId);
    void SaveStudy(Guid projectId, IdentificationStudy study);
}
