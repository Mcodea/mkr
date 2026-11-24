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
