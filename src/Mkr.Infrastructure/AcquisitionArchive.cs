using System.Security.Cryptography;
using Mkr.Core;

namespace Mkr.Infrastructure;

public static class AcquisitionArchive
{
    public static async Task<CsvImport> SaveAsync(Guid projectId, IImportStore store, ICsvEngine engine, IReadOnlyList<AcquiredSample> samples, AcquisitionOrigin origin, CancellationToken token)
    {
        if (samples.Count == 0) throw new InvalidOperationException("Aucun échantillon reçu.");
        var csv = new System.Text.StringBuilder("timestamp,SP,PV,MV\n");
        foreach (var point in samples)
            csv.Append(FormattableString.Invariant($"{point.ReceivedAt.UtcDateTime:yyyy-MM-ddTHH:mm:ss.ffffffZ},{point.Sp:R},{point.Pv:R},{point.Mv:R}\n"));
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        if (bytes.Length > 1_048_576) throw new CsvInputException("csv_limit");
        var mapping = new CsvMapping("timestamp", "PV", "PV", origin.Source.ProcessUnit);
        var (result, version) = await engine.AnalyzeAsync(bytes, ",", mapping, token);
        token.ThrowIfCancellationRequested();
        var summary = new ImportSummary(Guid.NewGuid(), $"acquisition-{samples[0].ReceivedAt:yyyyMMdd-HHmmss}.csv",
            Convert.ToHexString(SHA256.HashData(bytes)), DateTimeOffset.UtcNow);
        var import = new CsvImport(summary, ",", mapping, result, version, bytes, origin);
        store.SaveImport(projectId, import);
        return import;
    }

}
