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
    string AlgorithmVersion, ComparisonCriteria Criteria, HoldoutTest? HoldoutTest = null);

/// <summary>One discrete-time candidate (ARX or output-error) judged on the independent validation series.</summary>
public sealed record ArxCandidate(int Na, int Nb, int Nk, int Parameters, string Estimator, double ValidationSimulationRmse,
    double? ResidualLagOne, string[] Reasons, bool Accepted);

/// <summary>First-order equivalent (zero-order hold) of an accepted (1,1) candidate, with delta-method standard errors.</summary>
public sealed record ArxFirstOrder(double Gain, double TauSeconds, double DelaySeconds, Dictionary<string, double> StandardErrors);

public sealed record ArxSelected(int Na, int Nb, int Nk, string Estimator, double ValidationSimulationRmse, ArxFirstOrder? FirstOrder);

/// <summary>Result of the ARX / output-error identification (arbitrary excitation, no steady-state start). Refusal carries reasons and actions.</summary>
public sealed record ArxIdentification(string AlgorithmVersion, double SamplePeriodSeconds, string Decision, ArxSelected? Selected,
    IReadOnlyList<ArxCandidate> Candidates, string[]? Reasons = null, string[]? Actions = null)
{
    /// <summary>Operator text: the decision with its evidence, never a tuning. A first-order equivalent is shown only when accepted.</summary>
    public string Describe(string mvUnit, string pvUnit)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"Identification ARX / sortie-erreur · pas d'échantillonnage {SamplePeriodSeconds:G5} s · {Candidates.Count} candidats jugés sur l'essai de validation indépendant.");
        if (Selected is not { } chosen)
        {
            text.AppendLine("REFUS : aucun candidat ne satisfait les critères (" + string.Join(", ", Reasons ?? []) + ").");
            foreach (var action in Actions ?? []) text.AppendLine("  · " + action);
            return text.ToString();
        }
        text.AppendLine($"Modèle retenu : {chosen.Estimator} d'ordre ({chosen.Na},{chosen.Nb}), retard {chosen.Nk} pas ; erreur de simulation libre sur la validation {chosen.ValidationSimulationRmse:G4} {pvUnit}.");
        if (chosen.FirstOrder is { } f)
        {
            text.AppendLine($"Équivalent du premier ordre (maintien d'ordre zéro) : K = {f.Gain:G5} {pvUnit}/{mvUnit} (± {f.StandardErrors.GetValueOrDefault("gain"):G2}) · τ = {f.TauSeconds:G5} s (± {f.StandardErrors.GetValueOrDefault("tauSeconds"):G2}) · θ = {f.DelaySeconds:G5} s (multiple du pas).");
            text.AppendLine("L'exécution est enregistrée comme étude ARX (ADR 0007). Le réglage guidé est possible depuis une étude acceptée du premier ordre (ADR 0008).");
        }
        else text.AppendLine("Second ordre : pas d'équivalent du premier ordre ; MKR ne documente pas de réduction au premier ordre : aucun réglage n'en est déduit.");
        text.AppendLine("Statut : calculé et vérifié sur données synthétiques ; non qualifié sur procédé réel.");
        return text.ToString();
    }
}

/// <summary>A saved ARX / OE run (ADR 0007): inputs, engine version and the full decision, refusals included. Never a tuning.</summary>
public sealed record ArxStudy(Guid Id, Guid TrainingId, Guid ValidationId, StepColumns TrainingColumns, StepColumns ValidationColumns, string MvUnit, string PvUnit,
    double MvTolerance, double MaximumRmse, DateTimeOffset CalculatedAt, string EngineVersion, ArxIdentification Result,
    StepWindow? TrainingWindow = null, StepWindow? ValidationWindow = null)
{
    public bool IsAccepted => Result.Decision == "accepted";
}

/// <summary>Holdout F-test of the validation mean square errors, FOPDT over SOPDT (audit NC-04).</summary>
public sealed record HoldoutTest(double MseRatio, double Critical, double Level);

/// <summary>Criteria proposed from the measured pre-step noise only, before any fit is seen.</summary>
public sealed record CriteriaSuggestion(string AlgorithmVersion, double BaselineNoise, double MaximumRmse,
    double MaximumResidualLagOne, string Basis, double NoiseMultiple);

/// <summary>One open-loop MV step located in a recording; the window is accepted by the estimators.</summary>
public sealed record StepCandidate(int Id, int RowStart, int RowEnd, int StepRow, DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    double MvBefore, double MvAfter, double Amplitude, double PvBaseline, double PvResponse, int PreSamples, int PostSamples,
    double HorizonSeconds, double OnsetIntervalSeconds, bool Settled, double BaselineNoise, string[] Warnings);

public sealed record StepRejection(int StepRow, string Reason);

/// <summary>Two row-disjoint windows (possibly trimmed from the candidates) usable as estimation and validation.</summary>
public sealed record StepPair(int FirstId, int SecondId, double AmplitudeRatio, bool OppositeDirection,
    StepWindow FirstWindow, StepWindow SecondWindow, bool FirstSettled, bool SecondSettled, string[] Notes);

public sealed record StepSearch(string AlgorithmVersion, int SourceRows, StepCandidate[] Candidates,
    StepRejection[] Rejected, StepPair[] Pairs, string? ModeColumn = null, int RowsNotInManual = 0);

/// <summary>
/// Independence of the estimation and validation trials (AGENTS.md §6.3). Two imports with the same bytes are one trial.
/// Two trials of one import are accepted only when both ranges are explicit and share no row.
/// </summary>
public static class TrialIndependence
{
    public static string? Check(ImportSummary training, StepWindow? trainingWindow, ImportSummary validation, StepWindow? validationWindow)
    {
        if (training.Id != validation.Id)
            return training.Sha256 == validation.Sha256 ? "fit_duplicate_source" : null;
        if (trainingWindow is not { IsValid: true } a || validationWindow is not { IsValid: true } b)
            return "fit_same_source_window";
        return a.RowEnd < b.RowStart || b.RowEnd < a.RowStart ? null : "fit_same_source_overlap";
    }
}

/// <summary>One parameter of the stored model against its re-estimation on a new trial.</summary>
public sealed record DriftChange(double Previous, double Current, double RelativeChange, double? StandardError, bool Flagged);

/// <summary>Is a validated FOPDT model still consistent with a new open-loop step? Never changes the stored model.</summary>
public sealed record ModelDriftReport(string AlgorithmVersion, string Verdict, string[] Reasons, string[] DriftedParameters,
    double PredictionRmse, double? ResidualLagOne, double BaselineNoise, Dictionary<string, DriftChange> Changes, string[] Warnings);

public static class ModelDriftText
{
    public static string Describe(this ModelDriftReport report, string mvUnit, string pvUnit)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine(report.Verdict switch
        {
            "consistent" => "Le modèle reste cohérent avec le nouvel essai.",
            "drift" => "DÉRIVE : le modèle ne décrit plus le nouvel essai ; paramètre(s) changé(s) : " + string.Join(", ", report.DriftedParameters.Select(Name)) + ".",
            "parameter_changed_prediction_acceptable" => "Un paramètre a changé de façon significative, mais le modèle prédit encore le nouvel essai dans le bruit.",
            _ => "Le modèle ne prédit plus le nouvel essai dans le bruit, sans qu'un paramètre seul l'explique (structure ou perturbation)."
        });
        text.AppendLine($"Erreur de prédiction (RMSE) : {report.PredictionRmse:G4} {pvUnit} · bruit mesuré : {report.BaselineNoise:G4} {pvUnit}");
        foreach (var (name, change) in report.Changes)
            text.AppendLine($"  {Name(name)} : {change.Previous:G5} → {change.Current:G5} ({change.RelativeChange:+0.0%;-0.0%}" +
                            (change.StandardError is { } e ? $", erreur-type {e:G3}" : ", erreur-type indisponible") + (change.Flagged ? ") ⚠" : ")"));
        if (report.Reasons.Length > 0) text.AppendLine("Motifs de la porte de validation : " + string.Join(", ", report.Reasons));
        text.AppendLine("Un paramètre n'est signalé que s'il change de plus de trois erreurs-types et au-delà de 10 % (gain), 15 % (constante de temps), 30 % (seconde constante de temps) ou 25 % (retard) . Contrôle validé sur simulations seulement ; il ne modifie pas le modèle enregistré.");
        return text.ToString();
    }

    private static string Name(string key) => key switch { "gain" => "gain (pente pour un intégrateur)", "tauSeconds" => "constante de temps (s)", "secondTauSeconds" => "seconde constante de temps (s)", "delaySeconds" => "retard (s)", _ => key };
}

public interface IIdentificationEngine
{
    /// <summary>Predicts a new step with the stored FOPDT model (same acceptance rule as validation) and re-estimates it alone.</summary>
    Task<ModelDriftReport> CheckModelDriftAsync(ModelFamily family, IdentificationResult model, StepImport trial, double mvTolerance, double pvTolerance,
        ComparisonCriteria criteria, CancellationToken token);

    /// <summary>Lists the identifiable open-loop steps of a long recording (no window on the source).</summary>
    Task<StepSearch> FindStepsAsync(StepImport recording, double mvTolerance, double pvTolerance, CancellationToken token);

    Task<ModelComparison> CompareAsync(StepImport training, StepImport validation, double mvTolerance,
        double pvTolerance, double maximumRmse, double maximumResidualLagOne, CancellationToken token);

    /// <summary>ARX / output-error identification of open-loop data with any excitation; refuses closed-loop, constant-input and irregularly sampled data.</summary>
    Task<ArxIdentification> IdentifyArxAsync(StepImport training, StepImport validation, double mvTolerance, double maximumRmse, CancellationToken token);

    Task<TrialPreview> PreviewTrialAsync(StepImport trial, CancellationToken token);

    Task<CriteriaSuggestion> SuggestCriteriaAsync(StepImport training, StepImport validation, double mvTolerance,
        double pvTolerance, CancellationToken token);
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
    IReadOnlyList<ArxStudy> ListArxStudies(Guid projectId);
    void SaveArxStudy(Guid projectId, ArxStudy study);
}
