using Claims.Domain.Claims;

namespace Claims.Application.Claims.Common;

public enum InformationRequestState
{
    Open,
    Answered,

    /// <summary>Never answered and the claim is closed (e.g. rejected for no response).</summary>
    Unanswered,
}

/// <summary>
/// Claim detail. Internal fields (reserve, outstanding reserve, staff ids) are null in the claimant view.
/// All amounts are in <see cref="Currency"/>.
/// </summary>
public sealed record ClaimDetailResponse(
    Guid Id,
    string Number,
    ClaimStatus Status,
    ClaimType Type,
    Market Market,
    string Currency,
    string PolicyNumber,
    DateOnly IncidentDate,
    string Description,
    string ContactPhone,
    decimal ClaimedAmount,
    decimal? Reserve,
    decimal? ApprovedAmount,
    decimal? OutstandingReserve,
    decimal? SettledAmount,
    DateTimeOffset? SettledAt,
    string? RejectionReason,
    Guid? AssignedToUserId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt,
    IReadOnlyList<InformationRequestResponse> InformationRequests,
    IReadOnlyList<DocumentResponse> Documents,
    IReadOnlyList<HistoryEntryResponse> History);

public sealed record InformationRequestResponse(
    Guid Id, string Question, DateTimeOffset RequestedAt, string? Response, DateTimeOffset? RespondedAt,
    InformationRequestState State);

public sealed record DocumentResponse(
    Guid Id, string FileName, string ContentType, long SizeBytes, string StorageRef, DateTimeOffset UploadedAt,
    Guid? InformationRequestId, Guid? UploadedByUserId);

public sealed record HistoryEntryResponse(
    int Sequence, ClaimAction Action, ClaimStatus? FromStatus, ClaimStatus? ToStatus, string ActorRole,
    Guid? ActorUserId, DateTimeOffset OccurredAt, string? Reason, decimal? Amount, Guid? TargetUserId);

public sealed record ClaimSummaryResponse(
    Guid Id, string Number, ClaimStatus Status, ClaimType Type, Market Market, string Currency,
    decimal ClaimedAmount, decimal? OutstandingReserve, Guid? AssignedToUserId, DateTimeOffset SubmittedAt);

/// <summary>Detail plus the ETag (quoted base64 rowversion) for the response header.</summary>
public sealed record ClaimDetailResult(ClaimDetailResponse Claim, string ETag);
