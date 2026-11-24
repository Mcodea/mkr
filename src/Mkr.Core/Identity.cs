namespace Mkr.Core;

/// <summary>
/// Who performs an action. On Windows the identity comes from the operating system (domain account and security identifier):
/// MKR has no password of its own and cannot prove the person behind the session, only the account that was logged in.
/// </summary>
public sealed record UserIdentity(string Account, string Sid, string Machine)
{
    public string Label => $"{Account} [{Sid}] @{Machine}";
}

public interface IIdentityProvider
{
    UserIdentity Current { get; }
}

/// <summary>Identity from environment variables only (non-Windows hosts, tests): the security identifier is reported as unavailable.</summary>
public sealed class EnvironmentIdentityProvider : IIdentityProvider
{
    public UserIdentity Current => new($"{Environment.UserDomainName}\\{Environment.UserName}", "SID-indisponible", Environment.MachineName);
}

/// <summary>Process-wide identity source, replaced at startup by the Windows provider and by a fake one in tests.</summary>
public static class CurrentUser
{
    public static IIdentityProvider Provider { get; set; } = new EnvironmentIdentityProvider();
    public static UserIdentity Get() => Provider.Current;
}

public sealed class ReviewSeparationException(string message) : InvalidOperationException(message);

/// <summary>
/// Four-eyes rule: whoever calculated a stage does not verify or approve it. A lone engineer may still self-review, but only by
/// declaring it, and the record says so: the rule makes the situation visible, it does not forbid it.
/// </summary>
public static class ReviewSeparation
{
    public const string SelfReviewMark = " (auto-revue déclarée)";

    public static string ReviewerFor(IReadOnlyList<ReviewEvent> history, ReviewStage stage, ReviewState next, string identity, bool acknowledgeSelfReview)
    {
        if (next is not (ReviewState.Verified or ReviewState.Approved)) return identity;
        var author = history.LastOrDefault(e => e.Stage == stage && e.State == ReviewState.Calculated)?.Reviewer;
        if (author is null || !string.Equals(Strip(author), identity, StringComparison.Ordinal)) return identity;
        if (!acknowledgeSelfReview)
            throw new ReviewSeparationException("Cette étape a été calculée par le même compte : faites-la vérifier par une autre personne, ou déclarez explicitement une auto-revue.");
        return identity + SelfReviewMark;
    }

    private static string Strip(string reviewer) => reviewer.EndsWith(SelfReviewMark, StringComparison.Ordinal) ? reviewer[..^SelfReviewMark.Length] : reviewer;
}
