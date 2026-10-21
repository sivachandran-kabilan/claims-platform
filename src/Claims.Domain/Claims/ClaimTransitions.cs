namespace Claims.Domain.Claims;

/// <summary>
/// The single source of "what is allowed when" (ADR 005).
/// Status changes are keyed by (action, current status), not by (from, to): several actions share an edge
/// (Assign and RespondToInformation both lead to UnderReview; Reject and RejectForNoResponse both lead to
/// Rejected), so an edge-keyed table would let one action use another's path.
/// </summary>
public static class ClaimTransitions
{
    private static readonly Dictionary<(ClaimAction Action, ClaimStatus From), ClaimStatus> StatusChanges = new()
    {
        [(ClaimAction.Assign, ClaimStatus.Submitted)] = ClaimStatus.UnderReview,
        [(ClaimAction.RequestInformation, ClaimStatus.UnderReview)] = ClaimStatus.InfoRequested,
        [(ClaimAction.RespondToInformation, ClaimStatus.InfoRequested)] = ClaimStatus.UnderReview,
        [(ClaimAction.Approve, ClaimStatus.UnderReview)] = ClaimStatus.Approved,
        [(ClaimAction.Reject, ClaimStatus.UnderReview)] = ClaimStatus.Rejected,
        [(ClaimAction.RejectForNoResponse, ClaimStatus.InfoRequested)] = ClaimStatus.Rejected,
        [(ClaimAction.Settle, ClaimStatus.Approved)] = ClaimStatus.Settled,
    };

    // Actions that do not change status, and the statuses in which they are allowed.
    private static readonly Dictionary<ClaimAction, ClaimStatus[]> OtherActions = new()
    {
        [ClaimAction.SetReserve] = [ClaimStatus.UnderReview],
        [ClaimAction.Reassign] = [ClaimStatus.UnderReview, ClaimStatus.InfoRequested, ClaimStatus.Approved],
        [ClaimAction.AddDocument] = [ClaimStatus.Submitted, ClaimStatus.UnderReview, ClaimStatus.InfoRequested, ClaimStatus.Approved],
    };

    public static bool TryGetTarget(ClaimAction action, ClaimStatus from, out ClaimStatus to) =>
        StatusChanges.TryGetValue((action, from), out to);

    public static bool IsAllowed(ClaimAction action, ClaimStatus status) =>
        StatusChanges.ContainsKey((action, status))
        || (OtherActions.TryGetValue(action, out var statuses) && statuses.Contains(status));

    public static bool IsTerminal(ClaimStatus status) => status is ClaimStatus.Rejected or ClaimStatus.Settled;
}
