using System.IO;
using System.Security.Cryptography;
using Mkr.Core;

namespace Mkr.Desktop;

internal sealed class CsvWorkspace(Project project, IImportStore store, ICsvEngine engine)
{
    private const int MaxBytes = 1_048_576;
    private byte[]? _original;
    private string? _fileName;
    private string? _delimiter;

    public string ProjectName => project.Name;

    private void Audit(string action, string subject, string detail)
    {
        if (store is IAuditLog log) log.Record(project.Id, Environment.UserName, action, subject, detail);
    }

    public bool HasAuditLog => store is IAuditLog;
    public IReadOnlyList<AuditEntry> AuditEntries() => store is IAuditLog log ? log.List(project.Id) : [];
    public AuditVerification? VerifyAudit() => store is IAuditLog log ? log.Verify(project.Id) : null;
    public LoopBrief ReadLoopBrief() => store.ReadLoopBrief(project.Id);
    public void SaveLoopBrief(LoopBrief brief)
    {
        store.SaveLoopBrief(project.Id, brief);
        Audit("loop.brief.saved", brief.Tag, $"grandeur « {brief.ControlledQuantity} », objectif « {brief.Objective} »");
    }
    public IReadOnlyList<ImportSummary> List() => store.ListImports(project.Id);
    public CsvImport Open(Guid id) => store.ReadImport(project.Id, id);

    public async Task<CsvResult> InspectAsync(string path, string delimiter, CancellationToken token)
    {
        _original = null;
        await using var stream = File.OpenRead(path);
        var buffer = new byte[MaxBytes + 1];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, false, token);
        if (read > MaxBytes)
            throw new CsvInputException("csv_limit");
        var original = buffer[..read];
        var result = await engine.InspectAsync(original, delimiter, token);
        _original = original;
        _fileName = Path.GetFileName(path);
        _delimiter = delimiter;
        return result;
    }

    public async Task<CsvImport> SaveCaptureAsync(IReadOnlyList<AcquiredSample> samples, AcquisitionOrigin origin, CancellationToken token)
    {
        var import = await Mkr.Infrastructure.AcquisitionArchive.SaveAsync(project.Id, store, engine, samples, origin, token);
        Audit(origin.Session is null ? "acquisition.saved" : "acquisition.segment.saved", import.Summary.FileName,
            $"{import.Summary.Sha256[..12]} · {samples.Count} mesures · {origin.Describe()}");
        return import;
    }

    public async Task<CsvImport> AnalyzeAndSaveAsync(CsvMapping mapping, CancellationToken token, CsvFormat? format = null)
    {
        var original = _original ?? throw new InvalidOperationException("Aucun CSV chargé.");
        if (format is { Decimal: "," } && _delimiter == ",")
            throw new CsvInputException("csv_format");
        format = format is { IsDefault: true } ? null : format;
        var (result, version) = await engine.AnalyzeAsync(original, _delimiter!, mapping, token, format);
        token.ThrowIfCancellationRequested();
        var summary = new ImportSummary(Guid.NewGuid(), _fileName!, Convert.ToHexString(SHA256.HashData(original)), DateTimeOffset.UtcNow);
        var import = new CsvImport(summary, _delimiter!, mapping, result, version, original, Format: format);
        store.SaveImport(project.Id, import);
        Audit("import.saved", summary.FileName, $"{summary.Sha256[..12]} · colonne {mapping.ValueColumn} ({mapping.Role}, {mapping.Unit})");
        _original = null;
        return import;
    }
}
