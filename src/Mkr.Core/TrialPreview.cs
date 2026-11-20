namespace Mkr.Core;

/// <summary>Unaltered source-row coordinates used only for visual inspection.</summary>
public sealed record TrialPreviewPoint(int RowIndex, double Seconds, double Mv, double Pv);

public sealed record TrialPreview(string AlgorithmVersion, int SourceRows, int DisplayedRows,
    bool DisplayDecimated, DateTimeOffset FirstUtc, DateTimeOffset LastUtc, TrialPreviewPoint[] Points);
