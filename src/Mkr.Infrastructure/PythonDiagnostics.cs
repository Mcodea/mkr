using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Mkr.Core;

namespace Mkr.Infrastructure;

public sealed class PythonDiagnostics(string interpreter, string script) : IEngineDiagnostics, ICsvEngine, IIdentificationEngine, IPiEngine, IOpenLoopEngine
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
        string delimiter, CsvMapping mapping, CancellationToken token)
    {
        var payload = new { raw = original, delimiter, mapping.TimeColumn, mapping.ValueColumn, mapping.Role, mapping.Unit };
        var (result, version) = await CallAsync("analyze_csv", payload, token).ConfigureAwait(false);
        return (ReadCsvResult(result, expectQuality: true), version);
    }

    public async Task<(IdentificationResult Result, string EngineVersion)> IdentifyAsync(ModelFamily family, StepImport training,
        StepImport validation, double mvTolerance, double pvTolerance, CancellationToken token)
    {
        static object Source(StepImport source)
        {
            var fields = new Dictionary<string, object>
            {
                ["raw"] = source.Import.Original,
                ["delimiter"] = source.Import.Delimiter,
                ["timeColumn"] = source.Columns.TimeColumn,
                ["mvColumn"] = source.Columns.MvColumn,
                ["pvColumn"] = source.Columns.PvColumn
            };
            if (source.Window is { } window)
            {
                if (!window.IsValid)
                    throw new CsvInputException("fit_window");
                fields["rowStart"] = window.RowStart;
                fields["rowEnd"] = window.RowEnd;
            }
            return fields;
        }
        var payload = new { training = Source(training), validation = Source(validation), mvTolerance, pvTolerance };
        var operation = family switch
        {
            ModelFamily.FOPDT => "identify_fopdt",
            ModelFamily.SOPDT => "identify_sopdt",
            ModelFamily.Integrateur => "identify_integrator",
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        var (result, version) = await CallAsync(operation, payload, token).ConfigureAwait(false);
        var fit = result.Deserialize<IdentificationResult>(JsonOptions);
        if (fit is null || string.IsNullOrEmpty(fit.AlgorithmVersion) ||
            fit.DelaySeconds < 0 || fit.Gain == 0 || fit.Warnings is null ||
            fit.Training?.Points is not { Length: >= 2 } || fit.Validation?.Points is not { Length: >= 2 })
            throw new InvalidDataException("Résultat d’identification incompatible avec le contrat.");
        var validTimeConstants = family switch
        {
            ModelFamily.FOPDT => fit.TauSeconds is > 0 && fit.SecondTauSeconds is null,
            ModelFamily.SOPDT => fit.TauSeconds is > 0 && fit.SecondTauSeconds is > 0 && fit.SecondTauSeconds <= fit.TauSeconds,
            ModelFamily.Integrateur => fit.TauSeconds is null && fit.SecondTauSeconds is null,
            _ => false
        };
        if (!validTimeConstants)
            throw new InvalidDataException("Constantes de temps incompatibles avec la famille de modèle.");
        return (fit, version);
    }

    public async Task<TrialPreview> PreviewTrialAsync(StepImport trial, CancellationToken token)
    {
        var payload = new
        {
            raw = trial.Import.Original,
            trial.Import.Delimiter,
            trial.Columns.TimeColumn,
            trial.Columns.MvColumn,
            trial.Columns.PvColumn
        };
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

        static bool Valid(PiCoefficients? pi) => pi is { TiSeconds: > 0 } && pi.Kp != 0 && pi.ParallelKi != 0;
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
            expectQuality != (csv.Quality is not null))
            throw new InvalidDataException("Résultat CSV incompatible avec le contrat.");
        return csv;
    }

    private async Task<(JsonElement Result, string Version)> CallAsync(
        string operation, object payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(operation.StartsWith("identify_", StringComparison.Ordinal) ? 30 : 10));
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
