namespace Mkr.Core;

public enum ReviewStage { Data, Model, Strategy, Block, Tuning, Trials }
public enum ReviewState { Calculated, Verified, Approved, Rejected, NotApplicable }

public sealed record ReviewEvent(Guid ProjectId, Guid RunId, ReviewStage Stage,
    ReviewState State, string Reviewer, string Reason, string Evidence, DateTimeOffset RecordedAt);

public static class StudyReview
{
    public static void Validate(ReviewState? previous, ReviewState next,
        string reviewer, string reason, string evidence)
    {
        if (string.IsNullOrWhiteSpace(reviewer) || string.IsNullOrWhiteSpace(reason) ||
            string.IsNullOrWhiteSpace(evidence))
            throw new ArgumentException("Renseignez le validateur, le motif et la référence de preuve.");
        bool allowed = (previous, next) switch
        {
            (null, ReviewState.Calculated or ReviewState.NotApplicable) => true,
            (ReviewState.Calculated, ReviewState.Verified or ReviewState.Rejected) => true,
            (ReviewState.Verified, ReviewState.Approved or ReviewState.Rejected) => true,
            (ReviewState.Approved, ReviewState.Rejected) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException("Transition de revue non autorisée.");
    }
}
