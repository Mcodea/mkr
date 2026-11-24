using System.Security.Cryptography;
using Mkr.Core;

namespace Mkr.Infrastructure;

public static class AcquisitionArchive
{
    /// <summary>
    /// Archives the received samples unchanged. Raw values and round-trip times are kept next to the
    /// scaled values so the mapping can be audited. Missed samples are not written: gaps live in the origin.
    /// </summary>
    public static async Task<CsvImport> SaveAsync(Guid projectId, IImportStore store, ICsvEngine engine, IReadOnlyList<AcquiredSample> samples, AcquisitionOrigin origin, CancellationToken token)
    {
        if (samples.Count == 0) throw new InvalidOperationException("Aucun échantillon reçu.");
        bool detailed = origin.Target is not null;
        var csv = new System.Text.StringBuilder(detailed
            ? "timestamp,SP,PV,MV,rtt_ms,SP_raw,PV_raw,MV_raw\n"
            : "timestamp,SP,PV,MV\n");
        foreach (var point in samples)
        {
            csv.Append(FormattableString.Invariant($"{point.ReceivedAt.UtcDateTime:yyyy-MM-ddTHH:mm:ss.ffffffZ},{point.Sp:R},{point.Pv:R},{point.Mv:R}"));
            if (detailed)
                csv.Append(FormattableString.Invariant($",{point.RoundTripMs:0.###},{point.RawSp:R},{point.RawPv:R},{point.RawMv:R}"));
            csv.Append('\n');
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        if (bytes.Length > 1_048_576) throw new CsvInputException("csv_limit");
        var mapping = new CsvMapping("timestamp", "PV", "PV", origin.ProcessUnit);
        var (result, version) = await engine.AnalyzeAsync(bytes, ",", mapping, token);
        token.ThrowIfCancellationRequested();
        var name = origin.Session is { } session
            ? $"historique-{session.SessionId.ToString()[..8]}-{session.SegmentIndex:D3}.csv"
            : $"acquisition-{samples[0].ReceivedAt:yyyyMMdd-HHmmss}.csv";
        var summary = new ImportSummary(Guid.NewGuid(), name,
            Convert.ToHexString(SHA256.HashData(bytes)), DateTimeOffset.UtcNow);
        var import = new CsvImport(summary, ",", mapping, result, version, bytes, origin);
        store.SaveImport(projectId, import);
        return import;
    }
}
