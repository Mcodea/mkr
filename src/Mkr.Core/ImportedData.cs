namespace Mkr.Core;

public sealed record CsvMapping(string TimeColumn, string ValueColumn, string Role, string Unit);

public sealed record CsvQuality(string AlgorithmVersion, int ValidValues, int MissingValues, int InvalidValues,
    int InvalidTimestamps, int DuplicateTimestamps, int ReversedTimestamps, double? Minimum, double? Maximum,
    double? MinPositiveStepSeconds, double? MaxPositiveStepSeconds);

/// <summary>
/// Local date and time pattern (a whitelisted strptime pattern) and the IANA time zone it is expressed in.
/// A local time that does not exist or is ambiguous at a daylight-saving change is refused, never guessed.
/// </summary>
public sealed record CsvTimeFormat(string Pattern, string Zone);

/// <summary>
/// Declared reading format of a CSV (decimal separator and timestamp notation). The original bytes are never
/// changed: the format is stored with the import and applied each time the file is read.
/// </summary>
public sealed record CsvFormat(string Decimal, CsvTimeFormat? Time)
{
    public static readonly CsvFormat Default = new(".", null);

    /// <summary>Patterns the engine accepts, with the label shown to the user.</summary>
    public static readonly IReadOnlyList<(string Pattern, string Label)> Patterns =
    [
        ("%d/%m/%Y %H:%M:%S", "jj/mm/aaaa hh:mm:ss"),
        ("%d/%m/%Y %H:%M:%S.%f", "jj/mm/aaaa hh:mm:ss.ffffff"),
        ("%d.%m.%Y %H:%M:%S", "jj.mm.aaaa hh:mm:ss"),
        ("%Y-%m-%d %H:%M:%S", "aaaa-mm-jj hh:mm:ss"),
        ("%Y-%m-%d %H:%M:%S.%f", "aaaa-mm-jj hh:mm:ss.ffffff")
    ];

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDefault => Decimal == "." && Time is null;

    public string Describe() => (Decimal == "," ? "décimale : virgule" : "décimale : point") + " · " +
        (Time is { } t ? $"heure locale {t.Zone}, {Patterns.FirstOrDefault(p => p.Pattern == t.Pattern).Label ?? t.Pattern}"
                       : "ISO 8601 avec décalage UTC");
}

/// <summary>Proposal from the first rows. Nothing is applied until the user confirms it.</summary>
public sealed record CsvFormatSuggestion(string Decimal, string? TimeColumn, string? TimePattern, bool IsoWithOffset);

public sealed record CsvResult(string[] Headers, string[][] Preview, int RowCount, CsvQuality? Quality,
    CsvFormatSuggestion? SuggestedFormat = null);

public sealed record ImportSummary(Guid Id, string FileName, string Sha256, DateTimeOffset ImportedAt)
{
    public string Label => $"{FileName} · {ImportedAt.ToLocalTime():g}";
}

public sealed record CsvImport(ImportSummary Summary, string Delimiter, CsvMapping Mapping,
    CsvResult Result, string EngineVersion, byte[] Original, AcquisitionOrigin? Acquisition = null,
    CsvFormat? Format = null);

public interface ICsvEngine
{
    Task<CsvResult> InspectAsync(byte[] original, string delimiter, CancellationToken token);
    Task<(CsvResult Result, string EngineVersion)> AnalyzeAsync(byte[] original, string delimiter,
        CsvMapping mapping, CancellationToken token, CsvFormat? format = null);
}

public sealed record LoopBrief(string Tag, string ControlledQuantity, string OperatingPoint, string Objective, string Description);

public interface IImportStore
{
    LoopBrief ReadLoopBrief(Guid projectId);
    void SaveLoopBrief(Guid projectId, LoopBrief brief);
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
