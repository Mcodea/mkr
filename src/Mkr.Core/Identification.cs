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
    double? SecondTauSeconds = null, BroidaEvidence? BroidaEvidence = null, double[]? DelayIntervalSeconds = null)
{
    /// <summary>
    /// Conservative delay for tuning: the MV step happened between the last unchanged sample and the first
    /// changed one, so the true delay may exceed the estimate by up to that interval (audit NC-03).
    /// Studies recorded before the interval existed fall back to the training onset interval.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double TuningDelaySeconds => DelayIntervalSeconds is [_, var upper]
        ? upper : DelaySeconds + Training.OnsetIntervalSeconds;
}

public sealed record IdentificationStudy(Guid Id, Guid TrainingId, Guid ValidationId, StepColumns TrainingColumns,
    StepColumns ValidationColumns, string MvUnit, string PvUnit, double MvTolerance, double PvTolerance,
    DateTimeOffset CalculatedAt, string EngineVersion, IdentificationResult Result, ModelFamily Family = ModelFamily.FOPDT,
    StepWindow? TrainingWindow = null, StepWindow? ValidationWindow = null, ModelComparison? Comparison = null,
    ValidationAssessment? Assessment = null)
{
    public string Label => $"{Family} · {CalculatedAt.ToLocalTime():g} · {Id.ToString()[..8]}" +
        (IsAcceptedForTuning ? "" : " · refusée");

    /// <summary>
    /// True only when the independent validation trial satisfied declared criteria:
    /// an explicit assessment, or (studies recorded before it existed) a comparison that selected this family.
    /// Tuning and closed-loop simulation are refused otherwise (audit NC-02).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAcceptedForTuning => Assessment is { } assessment
        ? assessment.Accepted
        : Comparison?.Selected == Family.ToString();

    public void RequireAcceptedForTuning()
    {
        if (IsAcceptedForTuning) return;
        throw new ModelNotAcceptedException(Assessment is { Reasons.Length: > 0 } a
            ? $"Modèle refusé à la validation indépendante ({string.Join(", ", a.Reasons)}). Aucun réglage ni simulation en boucle fermée ne peut en être déduit."
            : "Cette étude ne comporte aucune décision de validation indépendante. Refaites l’estimation avec des critères de validation.");
    }
}

public sealed class ModelNotAcceptedException(string message) : InvalidOperationException(message);

/// <summary>Decision of the validation gate (engine/validation_gate.py) for one identified model.</summary>
public sealed record ValidationAssessment(string AlgorithmVersion, bool Accepted, string[] Reasons, ComparisonCriteria Criteria);

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

    /// <summary>Identifies the model and applies the validation gate on the independent trial.</summary>
    Task<(IdentificationResult Result, ValidationAssessment Assessment, string EngineVersion)> AssessAsync(ModelFamily family,
        StepImport training, StepImport validation, double mvTolerance, double pvTolerance,
        ComparisonCriteria criteria, CancellationToken token);
}

public interface IIdentificationStore
{
    IReadOnlyList<IdentificationStudy> ListStudies(Guid projectId);
    void SaveStudy(Guid projectId, IdentificationStudy study);
}
