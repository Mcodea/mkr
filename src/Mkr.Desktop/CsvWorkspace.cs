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
    public LoopBrief ReadLoopBrief() => store.ReadLoopBrief(project.Id);
    public void SaveLoopBrief(LoopBrief brief) => store.SaveLoopBrief(project.Id, brief);
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

    public Task<CsvImport> SaveCaptureAsync(IReadOnlyList<AcquiredSample> samples, AcquisitionOrigin origin, CancellationToken token)
        => Mkr.Infrastructure.AcquisitionArchive.SaveAsync(project.Id, store, engine, samples, origin, token);

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
        _original = null;
        return import;
    }
}
