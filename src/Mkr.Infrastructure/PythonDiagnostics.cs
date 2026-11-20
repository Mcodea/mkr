using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Mkr.Core;

namespace Mkr.Infrastructure;

public sealed class PythonDiagnostics(string interpreter, string script) : IEngineDiagnostics, ICsvEngine, IIdentificationEngine, IPiEngine
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
        static object Source(StepImport source) => new
        {
            raw = source.Import.Original,
            source.Import.Delimiter,
            source.Columns.TimeColumn,
            source.Columns.MvColumn,
            source.Columns.PvColumn
        };
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
            scenario.MaximumMv, scenario.Disturbance, scenario.DisturbanceAtSeconds
        };
        var (result, version) = await CallAsync("simulate_pi", payload, token).ConfigureAwait(false);
        var simulation = result.Deserialize<PiSimulationResult>(JsonOptions);
        if (simulation is null || string.IsNullOrEmpty(simulation.AlgorithmVersion) || simulation.Warnings is null ||
            simulation.Imc?.Points is not { Length: >= 3 and <= 5001 } imc ||
            simulation.Simc?.Points is not { Length: >= 3 and <= 5001 } simc ||
            imc.Length != simc.Length || imc[^1].Seconds != scenario.DurationSeconds ||
            simc[^1].Seconds != scenario.DurationSeconds)
            throw new InvalidDataException("Résultat de simulation incompatible avec le contrat.");
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
