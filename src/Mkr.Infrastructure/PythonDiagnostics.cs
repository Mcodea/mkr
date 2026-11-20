using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Mkr.Core;

namespace Mkr.Infrastructure;

public sealed class PythonDiagnostics(string interpreter, string script) : IEngineDiagnostics
{
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
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
            var request = JsonSerializer.Serialize(new { version = 1, requestId, operation = "ping", payload = new Dictionary<string, string>() });
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
                !reply.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object ||
                result.EnumerateObject().Count() != 1 || !result.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String || status.GetString() != "ready" ||
                !reply.TryGetProperty("engineVersion", out var engine) || engine.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(engine.GetString()))
                throw new InvalidDataException("Réponse Python incompatible avec le contrat v1.");

            return engine.GetString()!;
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
