using Claims.Application.Common.Persistence;
using Claims.Application.Common.Security;
using Claims.Domain.Claims;
using Microsoft.EntityFrameworkCore;

namespace Claims.Application.Claims.Common;

/// <summary>
/// Read-side projections shared by GET, the submit response and idempotent replay.
/// Queries project straight from SQL (no tracking); the claimant view is applied afterwards.
/// </summary>
public static class ClaimReadModel
{
    private static readonly ClaimAction[] InternalActions = [ClaimAction.SetReserve, ClaimAction.Reassign];

    public static async Task<ClaimDetailResult?> GetDetailAsync(
        IClaimsDbContext db, Guid claimId, ICurrentUser viewer, CancellationToken cancellationToken)
    {
        var row = await db.Claims
            .AsNoTracking() // split queries are the default (Infrastructure), so 3 collections don't multiply rows
            .VisibleTo(viewer)
            .Where(c => c.Id == claimId)
            .Select(c => new
            {
                Detail = new ClaimDetailResponse(
                    c.Id, c.Number.Value, c.Status, c.Type, c.Market, c.Currency, c.PolicyNumber, c.IncidentDate,
                    c.Description, c.ContactPhone, c.ClaimedAmount, c.Reserve, c.ApprovedAmount, c.OutstandingReserve,
                    c.SettledAmount, c.SettledAt, c.RejectionReason, c.AssignedToUserId, c.SubmittedAt, c.DecidedAt,
                    c.InformationRequests
                        .OrderBy(r => r.RequestedAt)
                        .Select(r => new InformationRequestResponse(
                            r.Id, r.Question, r.RequestedAt, r.Response, r.RespondedAt,
                            r.RespondedAt != null ? InformationRequestState.Answered : InformationRequestState.Open))
                        .ToList(),
                    c.Documents
                        .OrderBy(d => d.UploadedAt)
                        .Select(d => new DocumentResponse(
                            d.Id, d.FileName, d.ContentType, d.SizeBytes, d.StorageRef, d.UploadedAt,
                            d.InformationRequestId, d.UploadedByUserId))
                        .ToList(),
                    c.History
                        .OrderBy(h => h.Sequence)
                        .Select(h => new HistoryEntryResponse(
                            h.Sequence, h.Action, h.FromStatus, h.ToStatus, h.ActorRole, h.ActorUserId,
                            h.OccurredAt, h.Reason, h.Amount, h.TargetUserId))
                        .ToList()),
                RowVersion = EF.Property<byte[]>(c, ClaimsDbConstants.RowVersion),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var detail = MarkUnanswered(row.Detail);
        return new ClaimDetailResult(viewer.IsClaimant() ? ForClaimant(detail) : detail, ToETag(row.RowVersion));
    }

    /// <summary>Summary projection for lists (translated to SQL).</summary>
    public static IQueryable<ClaimSummaryResponse> ToSummaries(this IQueryable<Claim> claims) =>
        claims.Select(c => new ClaimSummaryResponse(
            c.Id, c.Number.Value, c.Status, c.Type, c.Market, c.Currency, c.ClaimedAmount,
            c.OutstandingReserve, c.AssignedToUserId, c.SubmittedAt));

    public static ClaimSummaryResponse ForClaimant(ClaimSummaryResponse summary) =>
        summary with { OutstandingReserve = null, AssignedToUserId = null };

    public static string ToETag(byte[] rowVersion) => $"\"{Convert.ToBase64String(rowVersion)}\"";

    // Insurer-internal data is hidden from claimants (approved slice 4): reserve, outstanding reserve,
    // staff ids, and reserve/reassignment history entries.
    private static ClaimDetailResponse ForClaimant(ClaimDetailResponse detail) => detail with
    {
        Reserve = null,
        OutstandingReserve = null,
        AssignedToUserId = null,
        Documents = detail.Documents.Select(d => d with { UploadedByUserId = null }).ToList(),
        History = detail.History
            .Where(h => !InternalActions.Contains(h.Action))
            .Select(h => h with { ActorUserId = null, TargetUserId = null })
            .ToList(),
    };

    private static ClaimDetailResponse MarkUnanswered(ClaimDetailResponse detail)
    {
        if (!ClaimTransitions.IsTerminal(detail.Status))
        {
            return detail;
        }

        return detail with
        {
            InformationRequests = detail.InformationRequests
                .Select(r => r.State == InformationRequestState.Open ? r with { State = InformationRequestState.Unanswered } : r)
                .ToList(),
        };
    }
}
