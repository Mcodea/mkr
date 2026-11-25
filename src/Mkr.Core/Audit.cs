namespace Mkr.Core;

/// <summary>
/// One entry of a project's append-only audit log. Each entry carries the hash of the previous one, so removing or editing
/// an entry in the middle of the log breaks the chain at that point. The hash chain does NOT stop someone with write access to the
/// whole database from recomputing every hash: it detects accidental or partial alteration, and the head hash can be noted or
/// printed elsewhere to detect a full rewrite.
/// </summary>
public sealed record AuditEntry(long Sequence, DateTimeOffset At, string Actor, string Action, string Subject, string Detail,
    string PreviousHash, string Hash);

public sealed record AuditVerification(bool Intact, int Entries, long? FirstBrokenSequence, string HeadHash);

public interface IAuditLog
{
    /// <summary>Appends an entry. A failure is never swallowed: the caller must know that an action could not be traced.</summary>
    AuditEntry Record(Guid projectId, string actor, string action, string subject, string detail);
    IReadOnlyList<AuditEntry> List(Guid projectId);
    AuditVerification Verify(Guid projectId);
}

public static class AuditHash
{
    public const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>SHA-256 (hex) of the canonical text of an entry; fields are length-prefixed so no value can imitate a separator.</summary>
    public static string Compute(Guid projectId, long sequence, DateTimeOffset at, string actor, string action, string subject,
        string detail, string previousHash)
    {
        static string Field(string value) => value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value;
        var text = string.Join("|", Field(projectId.ToString()), Field(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Field(at.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture)), Field(actor), Field(action), Field(subject), Field(detail),
            Field(previousHash));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}

/// <summary>
/// Export of a project's audit log as JSON Lines: a header line (project, export time, entry count, head hash) then one line per entry.
/// The file can be kept outside the project and verified later WITHOUT MKR (scripts/verify-audit-export.py recomputes the same hashes),
/// which closes the gap left by the in-database chain: a complete rewrite of the project file no longer matches an exported head hash.
/// </summary>
public static class AuditExport
{
    public const string Format = "mkr-audit-export-v1";
    private static readonly System.Text.Json.JsonSerializerOptions Json = new() { WriteIndented = false };

    public static string Write(Guid projectId, string projectName, IReadOnlyList<AuditEntry> entries, DateTimeOffset exportedAt)
    {
        var text = new System.Text.StringBuilder();
        text.Append(System.Text.Json.JsonSerializer.Serialize(new
        {
            format = Format, projectId = projectId.ToString(), projectName,
            exportedAtUtc = exportedAt.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            entries = entries.Count, headHash = entries.Count > 0 ? entries[^1].Hash : AuditHash.Genesis
        }, Json)).Append('\n');
        foreach (var e in entries)
            text.Append(System.Text.Json.JsonSerializer.Serialize(new
            {
                sequence = e.Sequence, at = e.At.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                actor = e.Actor, action = e.Action, subject = e.Subject, detail = e.Detail, previousHash = e.PreviousHash, hash = e.Hash
            }, Json)).Append('\n');
        return text.ToString();
    }

    /// <summary>Recomputes the chain of an exported file; any malformed line or inconsistency is reported, never skipped.</summary>
    public static AuditVerification Verify(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return new AuditVerification(false, 0, 0, AuditHash.Genesis);
        try
        {
            using var header = System.Text.Json.JsonDocument.Parse(lines[0]);
            var root = header.RootElement;
            if (root.GetProperty("format").GetString() != Format) return new AuditVerification(false, lines.Length - 1, 0, AuditHash.Genesis);
            var projectId = Guid.Parse(root.GetProperty("projectId").GetString()!);
            var declared = root.GetProperty("entries").GetInt32();
            var declaredHead = root.GetProperty("headHash").GetString()!;
            var previous = AuditHash.Genesis;
            for (var i = 1; i < lines.Length; i++)
            {
                using var document = System.Text.Json.JsonDocument.Parse(lines[i]);
                var e = document.RootElement;
                var sequence = e.GetProperty("sequence").GetInt64();
                var at = DateTimeOffset.Parse(e.GetProperty("at").GetString()!, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                var hash = AuditHash.Compute(projectId, sequence, at, e.GetProperty("actor").GetString()!, e.GetProperty("action").GetString()!,
                    e.GetProperty("subject").GetString()!, e.GetProperty("detail").GetString()!, e.GetProperty("previousHash").GetString()!);
                if (sequence != i || e.GetProperty("previousHash").GetString() != previous || e.GetProperty("hash").GetString() != hash)
                    return new AuditVerification(false, lines.Length - 1, i, previous);
                previous = hash;
            }
            var intact = declared == lines.Length - 1 && declaredHead == previous;
            return new AuditVerification(intact, lines.Length - 1, intact ? null : declared == lines.Length - 1 ? lines.Length - 1 : lines.Length, previous);
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            return new AuditVerification(false, lines.Length - 1, 0, AuditHash.Genesis);
        }
    }
}
