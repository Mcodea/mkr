using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Mkr.Core;

namespace Mkr.Infrastructure;

public sealed class PythonDiagnostics(string interpreter, string script) : IEngineDiagnostics, ICsvEngine, IIdentificationEngine, IPiEngine, IPidPrototypeEngine, IOpenLoopEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        var (result, version) = await CallAsync("ping", new { }, cancellationToken).ConfigureAwait(false);
        if (result.EnumerateObject().Count() != 1 || !result.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String || status.GetString() != "ready")
            throw new InvalidDataException("Réponse Python incompatible avec le diagnostic v1.");
        return version;
    }

    public async Task<CsvResult> InspectAsync(byte[] original, string delimiter, CancellationToken token)
    {
        var (result, _) = await CallAsync("inspect_csv", new { raw = original, delimiter }, token).ConfigureAwait(false);
        return ReadCsvResult(result, expectQuality: false);
    }

    public async Task<(CsvResult Result, string EngineVersion)> AnalyzeAsync(byte[] original,
        string delimiter, CsvMapping mapping, CancellationToken token, CsvFormat? format = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["raw"] = original, ["delimiter"] = delimiter, ["timeColumn"] = mapping.TimeColumn,
            ["valueColumn"] = mapping.ValueColumn, ["role"] = mapping.Role, ["unit"] = mapping.Unit
        };
        if (format is { IsDefault: false }) payload["format"] = format;
        var (result, version) = await CallAsync("analyze_csv", payload, token).ConfigureAwait(false);
        return (ReadCsvResult(result, expectQuality: true), version);
    }

    private static object Source(StepImport source)
    {
        var fields = new Dictionary<string, object>
        {
            ["raw"] = source.Import.Original,
            ["delimiter"] = source.Import.Delimiter,
            ["timeColumn"] = source.Columns.TimeColumn,
            ["mvColumn"] = source.Columns.MvColumn,
            ["pvColumn"] = source.Columns.PvColumn
        };
        if (source.Import.Format is { IsDefault: false } format)
            fields["format"] = format;
        if (source.Window is { } window)
        {
            if (!window.IsValid)
                throw new CsvInputException("fit_window");
            fields["rowStart"] = window.RowStart;
            fields["rowEnd"] = window.RowEnd;
        }
        return fields;
    }

    public async Task<ModelComparison> CompareAsync(StepImport training, StepImport validation,
        double mvTolerance, double pvTolerance, double maximumRmse, double maximumResidualLagOne, CancellationToken token)
    {
        var trials = new { training = Source(training), validation = Source(validation), mvTolerance, pvTolerance };
        var (json, _) = await CallAsync("compare_models", new { trials, maximumRmse, maximumResidualLagOne }, token);
        var report = json.Deserialize<ModelComparison>(JsonOptions);
        if (report?.Candidates is not { Count: 2 } candidates ||
            !candidates.TryGetValue("FOPDT", out var first) || !candidates.TryGetValue("SOPDT", out var second) ||
            candidates.Values.Any(c => c?.RejectionReasons is null || !double.IsFinite(c.ValidationRmse) || c.ValidationRmse < 0))
            throw new InvalidDataException("Comparaison de modèles incomplète.");
        bool firstAccepted = first.RejectionReasons.Length == 0, secondAccepted = second.RejectionReasons.Length == 0;
        bool higherOrder = report.Decision == "significantly_better_higher_order";
        string? expected = higherOrder ? "SOPDT" : firstAccepted ? "FOPDT" : secondAccepted ? "SOPDT" : null;
        if (string.IsNullOrWhiteSpace(report.AlgorithmVersion) || report.Criteria != new ComparisonCriteria(maximumRmse, maximumResidualLagOne) ||
            report.Selected != expected ||
            (higherOrder && (!firstAccepted || !secondAccepted || report.HoldoutTest is not { } test || !(test.MseRatio > test.Critical))) ||
            (!higherOrder && report.Decision != (expected is null ? "insufficient_evidence" : "lowest_acceptable_order")))
            throw new InvalidDataException("Décision de comparaison incohérente.");
        return report;
    }

    public async Task<StepSearch> FindStepsAsync(StepImport recording, double mvTolerance, double pvTolerance, CancellationToken token)
    {
        if (recording.Window is not null)
            throw new CsvInputException("fit_window");
        var payload = new { source = Source(recording), mvTolerance, pvTolerance };
        var (json, _) = await CallAsync("find_steps", payload, token).ConfigureAwait(false);
        var search = json.Deserialize<StepSearch>(JsonOptions);
        if (search?.Candidates is null || search.Rejected is null || search.Pairs is null ||
            string.IsNullOrWhiteSpace(search.AlgorithmVersion) || search.SourceRows < 2)
            throw new InvalidDataException("Recherche d’essais incompatible avec le contrat.");
        var ids = search.Candidates.Select(c => c.Id).ToHashSet();
        if (ids.Count != search.Candidates.Length ||
            search.Candidates.Any(c => c.RowStart < 0 || c.RowEnd <= c.RowStart || c.RowEnd >= search.SourceRows ||
                c.StepRow <= c.RowStart || c.StepRow > c.RowEnd || !double.IsFinite(c.Amplitude) || c.Amplitude == 0 ||
                c.Warnings is null) ||
            search.Pairs.Any(p => !ids.Contains(p.FirstId) || !ids.Contains(p.SecondId) || p.FirstId == p.SecondId ||
                p.FirstWindow is not { IsValid: true } || p.SecondWindow is not { IsValid: true } ||
                p.FirstWindow.RowEnd >= p.SecondWindow.RowStart || p.SecondWindow.RowEnd >= search.SourceRows || p.Notes is null))
            throw new InvalidDataException("Essais détectés incohérents avec l’enregistrement.");
        return search;
    }

    public async Task<CriteriaSuggestion> SuggestCriteriaAsync(StepImport training, StepImport validation,
        double mvTolerance, double pvTolerance, CancellationToken token)
    {
        var payload = new { training = Source(training), validation = Source(validation), mvTolerance, pvTolerance };
        var (json, _) = await CallAsync("suggest_criteria", payload, token).ConfigureAwait(false);
        var suggestion = json.Deserialize<CriteriaSuggestion>(JsonOptions);
        if (suggestion is null || string.IsNullOrWhiteSpace(suggestion.AlgorithmVersion) ||
            !double.IsFinite(suggestion.BaselineNoise) || suggestion.BaselineNoise < 0 ||
            !(suggestion.MaximumRmse > 0) || suggestion.MaximumResidualLagOne is not (> 0 and <= 1) ||
            suggestion.Basis is not ("noise" or "pv_tolerance"))
            throw new InvalidDataException("Proposition de critères incompatible avec le contrat.");
        return suggestion;
    }

    public async Task<(IdentificationResult Result, string EngineVersion)> IdentifyAsync(ModelFamily family, StepImport training,
        StepImport validation, double mvTolerance, double pvTolerance, CancellationToken token)
    {
        var payload = new { training = Source(training), validation = Source(validation), mvTolerance, pvTolerance };
        var operation = family switch
        {
            ModelFamily.FOPDT => "identify_fopdt",
            ModelFamily.BroidaFOPDT => "identify_broida",
            ModelFamily.SOPDT => "identify_sopdt",
            ModelFamily.Integrateur => "identify_integrator",
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        var (result, version) = await CallAsync(operation, payload, token).ConfigureAwait(false);
        return (CheckFit(family, result.Deserialize<IdentificationResult>(JsonOptions)), version);
    }

    public async Task<(IdentificationResult Result, ValidationAssessment Assessment, string EngineVersion)> AssessAsync(
        ModelFamily family, StepImport training, StepImport validation, double mvTolerance, double pvTolerance,
        ComparisonCriteria criteria, CancellationToken token)
    {
        var trials = new { training = Source(training), validation = Source(validation), mvTolerance, pvTolerance };
        var payload = new
        {
            family = family.ToString(), trials, maximumRmse = criteria.MaximumRmse,
            maximumResidualLagOne = criteria.MaximumResidualLagOne
        };
        var (result, version) = await CallAsync("assess_model", payload, token).ConfigureAwait(false);
        var fit = CheckFit(family, result.GetProperty("report").Deserialize<IdentificationResult>(JsonOptions));
        var assessment = result.GetProperty("assessment").Deserialize<ValidationAssessment>(JsonOptions);
        if (assessment is null || string.IsNullOrWhiteSpace(assessment.AlgorithmVersion) || assessment.Reasons is null ||
            assessment.Criteria != criteria || assessment.Accepted != (assessment.Reasons.Length == 0))
            throw new InvalidDataException("Décision de validation incompatible avec le contrat.");
        return (fit, assessment, version);
    }

    private static IdentificationResult CheckFit(ModelFamily family, IdentificationResult? fit)
    {
        if (fit is null || string.IsNullOrEmpty(fit.AlgorithmVersion) ||
            fit.DelaySeconds < 0 || fit.Gain == 0 || fit.Warnings is null ||
            fit.Training?.Points is not { Length: >= 2 } || fit.Validation?.Points is not { Length: >= 2 })
            throw new InvalidDataException("Résultat d’identification incompatible avec le contrat.");
        var validTimeConstants = family switch
        {
            ModelFamily.FOPDT => fit.TauSeconds is > 0 && fit.SecondTauSeconds is null,
            ModelFamily.BroidaFOPDT => fit.TauSeconds is > 0 && fit.SecondTauSeconds is null &&
                fit.BroidaEvidence is { } ev && ev.Crossing28Seconds > 0 &&
                ev.Crossing40Seconds > ev.Crossing28Seconds &&
                ev.TerminalSamples == 6 && ev.ThresholdFractions is { Length: 2 } fractions &&
                fractions[0] == 0.28 && fractions[1] == 0.40 &&
                ev.TimeOrigin == "mv_step_first_changed_sample" &&
                double.IsFinite(ev.TerminalPv) &&
                fit.StandardErrors is null,
            ModelFamily.SOPDT => fit.TauSeconds is > 0 && fit.SecondTauSeconds is > 0 && fit.SecondTauSeconds <= fit.TauSeconds,
            ModelFamily.Integrateur => fit.TauSeconds is null && fit.SecondTauSeconds is null,
            _ => false
        };
        if (!validTimeConstants)
            throw new InvalidDataException("Constantes de temps incompatibles avec la famille de modèle.");
        return fit;
    }

    public async Task<TrialPreview> PreviewTrialAsync(StepImport trial, CancellationToken token)
    {
        var payload = new Dictionary<string, object>
        {
            ["raw"] = trial.Import.Original,
            ["delimiter"] = trial.Import.Delimiter,
            ["timeColumn"] = trial.Columns.TimeColumn,
            ["mvColumn"] = trial.Columns.MvColumn,
            ["pvColumn"] = trial.Columns.PvColumn
        };
        if (trial.Import.Format is { IsDefault: false } format)
            payload["format"] = format;
        var (result, _) = await CallAsync("preview_trial", payload, token).ConfigureAwait(false);
        var preview = result.Deserialize<TrialPreview>(JsonOptions);
        if (preview is null || preview.SourceRows < 2 || preview.DisplayedRows < 2 ||
            preview.DisplayedRows > 600 || preview.Points is null ||
            preview.Points.Length != preview.DisplayedRows ||
            preview.FirstUtc >= preview.LastUtc ||
            preview.Points[0].RowIndex != 0 ||
            preview.Points[^1].RowIndex != preview.SourceRows - 1 ||
            preview.Points.Any(p => !double.IsFinite(p.Seconds) ||
                !double.IsFinite(p.Mv) || !double.IsFinite(p.Pv)) ||
            preview.Points.Zip(preview.Points.Skip(1),
                (a, b) => b.RowIndex > a.RowIndex && b.Seconds > a.Seconds).Any(valid => !valid))
            throw new InvalidDataException("Aperçu de signaux incompatible avec le contrat.");
        return preview;
    }

    public async Task<SopdtPid> CalculateSopdtPidAsync(IdentificationResult model, double responseSeconds, CancellationToken token)
    {
        var payload = new { model.Gain, model.TauSeconds, model.DelaySeconds, model.SecondTauSeconds, responseSeconds };
        var (json, _) = await CallAsync("calculate_simc_pid", payload, token).ConfigureAwait(false);
        var pid = json.Deserialize<SopdtPid>(JsonOptions);
        if (pid is not { Method: "SIMC_SOPDT", ControllerForm: "continuous_ideal_unfiltered_error", TimeUnit: "s",
            Series: { TiSeconds: > 0, TdSeconds: > 0 }, Ideal: { TiSeconds: > 0, TdSeconds: > 0 }, Warnings: not null } ||
            !ValidMargins(pid.Margins) ||
            string.IsNullOrWhiteSpace(pid.AlgorithmVersion) || Math.Sign(pid.Series.Kc) != Math.Sign(model.Gain) ||
            Math.Sign(pid.Ideal.Kp) != Math.Sign(model.Gain))
            throw new InvalidDataException("Réglage PID SOPDT incompatible avec le contrat.");
        return pid;
    }

    public async Task<IntegratorPi> CalculateIntegratorPiAsync(double slope, double delaySeconds, double responseSeconds, CancellationToken token)
    {
        var payload = new { gain = slope, delaySeconds, responseSeconds };
        var (json, _) = await CallAsync("calculate_integrator_pi", payload, token).ConfigureAwait(false);
        var pi = json.Deserialize<IntegratorPi>(JsonOptions);
        if (pi is not { Rule: "SIMC_integrating", TiSeconds: > 0, Warnings: not null, Margins: not null } ||
            !double.IsFinite(pi.Kp) || pi.Kp == 0 || Math.Sign(pi.Kp) != Math.Sign(slope) || pi.ResponseSeconds != responseSeconds ||
            !double.IsFinite(pi.Margins.PeakSensitivity) || pi.Margins.PeakSensitivity < 1 - 1e-9 || string.IsNullOrEmpty(pi.AlgorithmVersion))
            throw new InvalidDataException("Réglage PI d'intégrateur incompatible avec le contrat.");
        return pi;
    }

    public async Task<BlockConversionResult> ConvertToPidffAsync(double kp, double tiSeconds, double tdSeconds,
        double? filterSeconds, CancellationToken token)
    {
        var parameters = new Dictionary<string, object> { ["kp"] = kp, ["tiSeconds"] = tiSeconds, ["tdSeconds"] = tdSeconds };
        if (filterSeconds is { } filter) parameters["filterSeconds"] = filter;
        var payload = new { name = "PIDFF", target = new { }, parameters };
        var (json, _) = await CallAsync("convert_block_settings", payload, token).ConfigureAwait(false);
        var result = new BlockConversionResult(json.GetProperty("converted").GetBoolean(),
            json.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null,
            json.TryGetProperty("conversion", out var conversion) && conversion.ValueKind == JsonValueKind.Object
                ? conversion.Deserialize<BlockConversion>(JsonOptions) : null);
        // Safety invariants of the contract: a conversion is documentary, never qualified and never ready to load.
        if (result.Converted != (result.Conversion is not null) ||
            (result.Conversion is { } c && (c.Qualification != "non_qualified" || c.ReadyToLoad || c.Block != "PIDFF" ||
                c.Structure != "mixed" || !(c.Parameters.Kp > 0) || c.Parameters.Ti is not { Seconds: > 0 } ||
                c.Parameters.MixPar || c.Warnings is null || c.ToDetermine is null || c.Equations is null || c.Equations.Length == 0)))
            throw new InvalidDataException("Conversion de bloc incompatible avec le contrat.");
        return result;
    }

    public async Task<BlockSettingsResult> ConvertToBlockAsync(string block, IReadOnlyDictionary<string, object> parameters, CancellationToken token)
    {
        if (block is not ("PID_INT" or "PID_Compact")) throw new ArgumentException("Bloc non pris en charge par cette conversion.", nameof(block));
        var payload = new { name = block, target = new { }, parameters };
        var (json, _) = await CallAsync("convert_block_settings", payload, token).ConfigureAwait(false);
        var result = new BlockSettingsResult(json.GetProperty("converted").GetBoolean(),
            json.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null,
            json.TryGetProperty("conversion", out var conversion) && conversion.ValueKind == JsonValueKind.Object
                ? conversion.Deserialize<BlockSettings>(JsonOptions) : null);
        // Safety invariants: documentary only, never qualified, never ready to load.
        if (result.Converted != (result.Settings is not null) ||
            (result.Settings is { } c && (c.Qualification != "non_qualified" || c.ReadyToLoad || c.Block != block || c.Parameters is not { Count: > 0 } ||
                c.Warnings is null || c.ToDetermine is null || c.Equations is null || c.Equations.Length == 0)))
            throw new InvalidDataException("Conversion de bloc incompatible avec le contrat.");
        return result;
    }

    private static bool ValidMargins(LoopMargins? margins) => margins is null || (
        margins.Warnings is not null && double.IsFinite(margins.PeakSensitivity) && margins.PeakSensitivity >= 1 - 1e-9 &&
        (margins.GainMargin is null || margins.GainMargin > 0) && (margins.PhaseMarginDegrees is null || double.IsFinite(margins.PhaseMarginDegrees.Value)) &&
        // A nominal-unstable loop must say so: no margin may be positive while the warning is absent.
        (margins.Warnings.Contains("nominal_unstable") || (margins.GainMargin ?? 2) >= 1 && (margins.PhaseMarginDegrees ?? 1) >= 0));

    public async Task<(PiComparison Result, string EngineVersion)> CalculatePiAsync(
        double gain, double tauSeconds, double delaySeconds, double responseSeconds, CancellationToken token)
    {
        var payload = new { gain, tauSeconds, delaySeconds, responseSeconds };
        var (result, version) = await CallAsync("calculate_pi", payload, token).ConfigureAwait(false);
        var comparison = result.Deserialize<PiComparison>(JsonOptions);
        if (comparison is null || string.IsNullOrEmpty(comparison.AlgorithmVersion) ||
            comparison.ResponseSeconds != responseSeconds || comparison.Warnings is null ||
            !Valid(comparison.Imc) || !Valid(comparison.Simc))
            throw new InvalidDataException("Résultat PI incompatible avec le contrat.");
        return (comparison, version);

        static bool Valid(PiCoefficients? pi) => pi is { TiSeconds: > 0 } && pi.Kp != 0 && pi.ParallelKi != 0 && ValidMargins(pi.Margins);
    }

    public async Task<PiTransferResult> SimulateTransferAsync(IdentificationResult model, PiCoefficients coefficients,
        PiScenario scenario, double manualMv, double automaticAtSeconds, CancellationToken token)
    {
        if (scenario.Disturbance != 0)
            throw new InvalidOperationException("Pour cet essai de transfert, renseignez une perturbation nulle.");
        var payload = new
        {
            model.Gain, model.TauSeconds, model.DelaySeconds, coefficients.Kp, coefficients.TiSeconds,
            scenario.SampleSeconds, scenario.DurationSeconds, scenario.Setpoint, scenario.MinimumMv,
            scenario.MaximumMv, scenario.MaximumMvRate, manualMv, automaticAtSeconds
        };
        var (json, _) = await CallAsync("simulate_pi_transition", payload, token).ConfigureAwait(false);
        var result = json.Deserialize<PiTransferResult>(JsonOptions);
        if (result?.Points is not { Length: >= 3 and <= 5001 } points || result.Warnings is null ||
            string.IsNullOrWhiteSpace(result.AlgorithmVersion) || points.Any(p => p is null) ||
            points[^1].Seconds != scenario.DurationSeconds || points[^1].Mv is not null)
            throw new InvalidDataException("Résultat de transfert PI incomplet.");
        int index = Array.FindIndex(points, p => p.Mode == "automatic");
        if (index < 1 || index >= points.Length - 1 ||
            Math.Abs(points[index].Seconds - automaticAtSeconds) > scenario.SampleSeconds * 1e-7 ||
            points[index - 1].Mode != "manual" || points[index - 1].Mv is null ||
            points[index].Mv is null || points[index].Cv is null)
            throw new InvalidDataException("Échantillons de bascule PI incohérents.");
        return result;
    }

    public async Task<(PiSimulationResult Result, string EngineVersion)> SimulatePiAsync(
        IdentificationResult model, PiComparison tuning, PiScenario scenario, CancellationToken token)
    {
        var payload = new
        {
            model.Gain, model.TauSeconds, model.DelaySeconds,
            imc = new { tuning.Imc.Kp, tuning.Imc.TiSeconds },
            simc = new { tuning.Simc.Kp, tuning.Simc.TiSeconds },
            scenario.SampleSeconds, scenario.DurationSeconds, scenario.Setpoint, scenario.MinimumMv,
            scenario.MaximumMv, scenario.Disturbance, scenario.DisturbanceAtSeconds, scenario.MaximumMvRate
        };
        var (result, version) = await CallAsync("simulate_pi", payload, token).ConfigureAwait(false);
        var simulation = result.Deserialize<PiSimulationResult>(JsonOptions);
        if (simulation is null || string.IsNullOrEmpty(simulation.AlgorithmVersion) || simulation.Warnings is null ||
            simulation.Imc?.Points is not { Length: >= 3 and <= 5001 } imc ||
            simulation.Simc?.Points is not { Length: >= 3 and <= 5001 } simc ||
            simulation.Imc.SetpointResponse is null || simulation.Simc.SetpointResponse is null ||
            imc.Length != simc.Length || imc[^1].Seconds != scenario.DurationSeconds ||
            simc[^1].Seconds != scenario.DurationSeconds)
            throw new InvalidDataException("Résultat de simulation incompatible avec le contrat.");
        return (simulation, version);
    }

    public async Task<(PidPrototypeResult Result, string EngineVersion)> SimulatePidPrototypeAsync(
        IdentificationResult model, PidPrototypeCoefficients coefficients,
        PiScenario scenario, CancellationToken token, double[]? measurementNoise = null)
    {
        if (model.TauSeconds is not > 0)
            throw new InvalidDataException("Le prototype PID requiert un modèle FOPDT.");
        // Never convert invalid or out-of-grid sensor fixtures into valid samples.
        if (measurementNoise is not null &&
            (measurementNoise.Length != (int)Math.Round(scenario.DurationSeconds / scenario.SampleSeconds) ||
             measurementNoise.Any(x => !double.IsFinite(x) || Math.Abs(x) > 1e12)))
            throw new CsvInputException("pid_noise");
        var payload = new Dictionary<string, object?>
        {
            ["gain"] = model.Gain,
            ["tauSeconds"] = model.TauSeconds,
            ["delaySeconds"] = model.DelaySeconds,
            ["controller"] = new
            {
                coefficients.Kp, coefficients.TiSeconds,
                coefficients.TdSeconds, coefficients.FilterSeconds
            },
            ["sampleSeconds"] = scenario.SampleSeconds,
            ["durationSeconds"] = scenario.DurationSeconds,
            ["setpoint"] = scenario.Setpoint,
            ["minimumMv"] = scenario.MinimumMv,
            ["maximumMv"] = scenario.MaximumMv,
            ["disturbance"] = scenario.Disturbance,
            ["disturbanceAtSeconds"] = scenario.DisturbanceAtSeconds,
            ["maximumMvRate"] = scenario.MaximumMvRate
        };
        if (measurementNoise is not null)
            payload["measurementNoise"] = measurementNoise;
        var (result, version) = await CallAsync("simulate_pid_experimental", payload, token)
            .ConfigureAwait(false);
        var simulation = result.Deserialize<PidPrototypeResult>(JsonOptions);
        if (simulation is null ||
            simulation.AlgorithmVersion != "0.2.0" ||
            simulation.ControllerForm != "ideal_dependent_pid_derivative_on_pv" ||
            simulation.Warnings is null ||
            simulation.Points is not { Length: >= 3 and <= 5001 } points ||
            simulation.SetpointResponse is null ||
            points[0].Seconds != 0 ||
            points[^1].Seconds != scenario.DurationSeconds ||
            points[^1].Cv is not null || points[^1].Mv is not null ||
            points[^1].Derivative is not null || points[^1].MeasuredPv is not null ||
            !double.IsFinite(simulation.Iae) || simulation.Iae < 0 ||
            !double.IsFinite(simulation.Ise) || simulation.Ise < 0 ||
            !double.IsFinite(simulation.FinalError) ||
            !double.IsFinite(simulation.SaturationSeconds) ||
            !double.IsFinite(simulation.RateLimitedSeconds) ||
            points.Any(p => !double.IsFinite(p.Seconds) || !double.IsFinite(p.Pv) ||
                (p.Cv is { } cv && !double.IsFinite(cv)) ||
                (p.Mv is { } mv && !double.IsFinite(mv)) ||
                (p.Derivative is { } derivative && !double.IsFinite(derivative)) ||
                (p.MeasuredPv is { } measured && !double.IsFinite(measured))) ||
            points.Zip(points.Skip(1), (a, b) => b.Seconds > a.Seconds).Any(ok => !ok) ||
            !IsValidPidGrid(points, scenario))
            throw new InvalidDataException("Simulation PID expérimentale incompatible avec le contrat.");
        return (simulation, version);
    }

    private static bool IsValidPidGrid(PidPrototypePoint[] points, PiScenario scenario)
    {
        if (scenario.SampleSeconds <= 0 || !double.IsFinite(scenario.SampleSeconds) ||
            !double.IsFinite(scenario.DurationSeconds))
            return false;
        var intervals = scenario.DurationSeconds / scenario.SampleSeconds;
        if (!double.IsFinite(intervals) || intervals < 2 || intervals > 5000.00000001 ||
            Math.Abs(intervals - Math.Round(intervals)) > 1e-8 ||
            points.Length != (int)Math.Round(intervals) + 1)
            return false;
        var tolerance = 1e-8 * Math.Max(1.0, scenario.DurationSeconds);
        double previousMv = 0;
        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            if (Math.Abs(point.Seconds - i * scenario.SampleSeconds) > tolerance)
                return false;
            if (i == points.Length - 1)
                return point.Cv is null && point.Mv is null &&
                    point.Derivative is null && point.Disturbance is null;
            if (point.Cv is null || point.Mv is not { } mv ||
                point.Derivative is null || point.Disturbance is null ||
                point.MeasuredPv is null ||
                mv < scenario.MinimumMv - tolerance || mv > scenario.MaximumMv + tolerance)
                return false;
            if (scenario.MaximumMvRate is { } limit &&
                Math.Abs(mv - previousMv) > limit * scenario.SampleSeconds + tolerance)
                return false;
            previousMv = mv;
        }
        return true;
    }

    public async Task<(PiRobustnessResult Result, string EngineVersion)> AnalyzeCombinedAsync(
        IdentificationResult model, PiComparison tuning, PiScenario scenario,
        double variationPercent, CancellationToken token)
    {
        if (model.TauSeconds is not > 0 || !double.IsFinite(variationPercent) ||
            variationPercent <= 0 || variationPercent > 50)
            throw new CsvInputException("combined_domain");

        var payload = new
        {
            model.Gain, model.TauSeconds, model.DelaySeconds,
            imc = new { tuning.Imc.Kp, tuning.Imc.TiSeconds },
            simc = new { tuning.Simc.Kp, tuning.Simc.TiSeconds },
            scenario.SampleSeconds, scenario.DurationSeconds, scenario.Setpoint,
            scenario.MinimumMv, scenario.MaximumMv, scenario.Disturbance,
            scenario.DisturbanceAtSeconds, scenario.MaximumMvRate, variationPercent
        };
        var (result, version) = await CallAsync("simulate_pi_combined", payload, token)
            .ConfigureAwait(false);
        var combined = result.Deserialize<PiRobustnessResult>(JsonOptions);
        if (combined is null || string.IsNullOrWhiteSpace(combined.AlgorithmVersion) ||
            combined.VariationPercent != variationPercent || combined.Warnings is null ||
            combined.Cases is not { Length: 27 } cases || combined.Envelope is not { } envelope ||
            envelope.Seconds is not { Length: >= 3 and <= 5001 } times ||
            envelope.Imc is null || envelope.Simc is null ||
            times[^1] != scenario.DurationSeconds ||
            times[0] != 0 || times.Any(t => !double.IsFinite(t)) ||
            times.Zip(times.Skip(1), (a, b) => b > a).Any(ok => !ok) ||
            !ValidBounds(envelope.Imc, times.Length) ||
            !ValidBounds(envelope.Simc, times.Length) ||
            cases.Select(c => c.Key).Distinct().Count() != 27 ||
            cases.Any(c => c is null || string.IsNullOrWhiteSpace(c.Key) ||
                c.Gain == 0 || c.TauSeconds <= 0 || c.DelaySeconds < 0 ||
                !double.IsFinite(c.Gain) || !double.IsFinite(c.TauSeconds) ||
                !double.IsFinite(c.DelaySeconds) || c.Warnings is null ||
                !ValidMetrics(c.Imc) || !ValidMetrics(c.Simc)))
            throw new InvalidDataException("Résultat de sensibilité combinée incompatible avec le contrat.");
        return (combined, version);

        static bool ValidBounds(PiPvBounds bounds, int length) =>
            bounds.MinPv is not null && bounds.MaxPv is not null &&
            bounds.MinPv.Length == length && bounds.MaxPv.Length == length &&
            bounds.MinPv.Zip(bounds.MaxPv, (low, high) =>
                double.IsFinite(low) && double.IsFinite(high) && low <= high).All(ok => ok);

        static bool ValidMetrics(PiSensitivityMetrics? m) =>
            m is not null && double.IsFinite(m.Iae) && m.Iae >= 0 &&
            double.IsFinite(m.Ise) && m.Ise >= 0 &&
            double.IsFinite(m.FinalError) &&
            double.IsFinite(m.SaturationSeconds) && m.SaturationSeconds >= 0 &&
            double.IsFinite(m.RateLimitedSeconds) && m.RateLimitedSeconds >= 0 &&
            m.FrozenSteps >= 0 &&
            (m.OvershootPv is null || double.IsFinite(m.OvershootPv.Value)) &&
            (m.SettlingSeconds is null || double.IsFinite(m.SettlingSeconds.Value));
    }

    public async Task<(PiRobustnessResult Result, string EngineVersion)> AnalyzeRobustnessAsync(
        IdentificationResult model, PiComparison tuning, PiScenario scenario,
        double variationPercent, CancellationToken token)
    {
        if (model.TauSeconds is not > 0 || variationPercent is <= 0 or > 50 ||
            !double.IsFinite(variationPercent))
            throw new CsvInputException("robust_domain");
        var payload = new
        {
            model.Gain, model.TauSeconds, model.DelaySeconds,
            imc = new { tuning.Imc.Kp, tuning.Imc.TiSeconds },
            simc = new { tuning.Simc.Kp, tuning.Simc.TiSeconds },
            scenario.SampleSeconds, scenario.DurationSeconds, scenario.Setpoint,
            scenario.MinimumMv, scenario.MaximumMv, scenario.Disturbance,
            scenario.DisturbanceAtSeconds, scenario.MaximumMvRate, variationPercent
        };
        var (result, version) = await CallAsync("simulate_pi_robustness", payload, token).ConfigureAwait(false);
        var sensitivity = result.Deserialize<PiRobustnessResult>(JsonOptions);
        string[] keys = ["nominal", "gain_minus", "gain_plus", "tau_minus",
            "tau_plus", "delay_minus", "delay_plus"];
        if (sensitivity is null || string.IsNullOrWhiteSpace(sensitivity.AlgorithmVersion) ||
            sensitivity.VariationPercent != variationPercent ||
            sensitivity.Warnings is null || sensitivity.Cases is not { Length: 7 } cases ||
            cases.Where((item, index) => item is null || item.Key != keys[index] ||
                !double.IsFinite(item.Gain) || !double.IsFinite(item.TauSeconds) ||
                !double.IsFinite(item.DelaySeconds) || item.TauSeconds <= 0 ||
                item.Gain == 0 || item.DelaySeconds < 0 ||
                item.Warnings is null || !ValidMetrics(item.Imc) || !ValidMetrics(item.Simc)).Any())
            throw new InvalidDataException("Résultat de robustesse PI incompatible avec le contrat.");
        return (sensitivity, version);

        static bool ValidMetrics(PiSensitivityMetrics? m) =>
            m is not null && double.IsFinite(m.Iae) && m.Iae >= 0 &&
            double.IsFinite(m.Ise) && m.Ise >= 0 &&
            double.IsFinite(m.FinalError) &&
            double.IsFinite(m.SaturationSeconds) && m.SaturationSeconds >= 0 &&
            double.IsFinite(m.RateLimitedSeconds) && m.RateLimitedSeconds >= 0 &&
            m.FrozenSteps >= 0 &&
            (m.OvershootPv is null || double.IsFinite(m.OvershootPv.Value)) &&
            (m.SettlingSeconds is null || double.IsFinite(m.SettlingSeconds.Value));
    }

    public async Task<(OpenLoopResult Result, string EngineVersion)> SimulateOpenLoopAsync(
        IdentificationResult model, OpenLoopScenario scenario, CancellationToken token)
    {
        var payload = new
        {
            model.Gain, model.TauSeconds, model.DelaySeconds, scenario.SampleSeconds, scenario.DurationSeconds,
            scenario.StepMv, scenario.StepAtSeconds, scenario.MinimumMv, scenario.MaximumMv, scenario.MaximumMvRate
        };
        var (result, version) = await CallAsync("simulate_open_loop", payload, token).ConfigureAwait(false);
        var simulation = result.Deserialize<OpenLoopResult>(JsonOptions);
        if (simulation is null || string.IsNullOrEmpty(simulation.AlgorithmVersion) || simulation.Warnings is null ||
            simulation.Points is not { Length: >= 3 and <= 5001 } points ||
            points[^1] is not { RequestedMv: null, AppliedMv: null } || points[^1].Seconds != scenario.DurationSeconds ||
            points.Take(points.Length - 1).Any(point => point is not { RequestedMv: not null, AppliedMv: not null }))
            throw new InvalidDataException("Résultat de simulation en boucle ouverte incompatible avec le contrat.");
        return (simulation, version);
    }

    private static CsvResult ReadCsvResult(JsonElement result, bool expectQuality)
    {
        var csv = result.Deserialize<CsvResult>(JsonOptions);
        if (csv is null || csv.Headers is null || csv.Preview is null || csv.RowCount < 1 ||
            csv.Preview.Length != Math.Min(200, csv.RowCount) ||
            csv.Preview.Any(row => row is null || row.Length != csv.Headers.Length) ||
            expectQuality != (csv.Quality is not null) ||
            (csv.SuggestedFormat is { } suggestion && suggestion.Decimal is not ("." or ",")))
            throw new InvalidDataException("Résultat CSV incompatible avec le contrat.");
        return csv;
    }

    private async Task<(JsonElement Result, string Version)> CallAsync(
        string operation, object payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(operation.StartsWith("identify_", StringComparison.Ordinal) ||
            operation is "assess_model" or "compare_models" or "suggest_criteria" or "find_steps" ? 30 : 10));
        var token = deadline.Token;
        var utf8 = new UTF8Encoding(false, true);
        var start = new ProcessStartInfo(interpreter)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = utf8,
            StandardOutputEncoding = utf8
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add(script);
        using var process = Process.Start(start) ?? throw new IOException("Démarrage de Python impossible.");
        try
        {
            Task diagnostics = process.StandardError.BaseStream.CopyToAsync(Stream.Null, token);
            var output = process.StandardOutput.ReadToEndAsync(token);
            var requestId = Guid.NewGuid().ToString("N");
            var request = JsonSerializer.Serialize(new { version = 1, requestId, operation, payload }, JsonOptions);
            await process.StandardInput.WriteAsync(request.AsMemory(), token).ConfigureAwait(false);
            process.StandardInput.Close();
            await Task.WhenAll(process.WaitForExitAsync(token), output, diagnostics).ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new IOException($"Le moteur Python s’est arrêté (code {process.ExitCode}).");

            using var json = JsonDocument.Parse(await output.ConfigureAwait(false));
            var reply = json.RootElement;
            if (reply.ValueKind != JsonValueKind.Object || reply.EnumerateObject().Count() != 4 ||
                !reply.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out var protocol) || protocol != 1 ||
                !reply.TryGetProperty("requestId", out var id) || id.ValueKind != JsonValueKind.String ||
                id.GetString() != requestId ||
                !reply.TryGetProperty("engineVersion", out var engine) || engine.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(engine.GetString()))
                throw new InvalidDataException("Réponse Python incompatible avec le contrat v1.");

            if (reply.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
                throw new CsvInputException(code.GetString()!);
            if (!reply.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Résultat Python absent.");
            return (result.Clone(), engine.GetString()!);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Le moteur Python ne répond pas dans le délai prévu.");
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
        }
    }
}
