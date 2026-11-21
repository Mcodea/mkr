namespace Mkr.Core;

public sealed record CsvMapping(string TimeColumn, string ValueColumn, string Role, string Unit);

public sealed record CsvQuality(string AlgorithmVersion, int ValidValues, int MissingValues, int InvalidValues,
    int InvalidTimestamps, int DuplicateTimestamps, int ReversedTimestamps, double? Minimum, double? Maximum,
    double? MinPositiveStepSeconds, double? MaxPositiveStepSeconds);

public sealed record CsvResult(string[] Headers, string[][] Preview, int RowCount, CsvQuality? Quality);

public sealed record ImportSummary(Guid Id, string FileName, string Sha256, DateTimeOffset ImportedAt)
{
    public string Label => $"{FileName} · {ImportedAt.ToLocalTime():g}";
}

public sealed record CsvImport(ImportSummary Summary, string Delimiter, CsvMapping Mapping,
    CsvResult Result, string EngineVersion, byte[] Original, AcquisitionOrigin? Acquisition = null);

public interface ICsvEngine
{
    Task<CsvResult> InspectAsync(byte[] original, string delimiter, CancellationToken token);
    Task<(CsvResult Result, string EngineVersion)> AnalyzeAsync(byte[] original, string delimiter,
        CsvMapping mapping, CancellationToken token);
}

public interface IImportStore
{
    string ReadDescription(Guid projectId);
    void SaveDescription(Guid projectId, string description);
    IReadOnlyList<ImportSummary> ListImports(Guid projectId);
    CsvImport ReadImport(Guid projectId, Guid importId);
    void SaveImport(Guid projectId, CsvImport import);
}

public sealed class CsvInputException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
