namespace Claims.Domain.Claims;

/// <summary>A question from staff to the claimant. At most one is open per claim.</summary>
public sealed class InformationRequest
{
    private InformationRequest() { } // EF

    internal InformationRequest(string question, Guid requestedByUserId, DateTimeOffset requestedAt)
    {
        Id = Guid.NewGuid();
        Question = question;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
    }

    public Guid Id { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public Guid RequestedByUserId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public string? Response { get; private set; }
    public Guid? RespondedByUserId { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    public bool IsOpen => RespondedAt is null;

    internal void Respond(string response, Guid respondedByUserId, DateTimeOffset respondedAt)
    {
        Response = response;
        RespondedByUserId = respondedByUserId;
        RespondedAt = respondedAt;
    }
}

/// <summary>Metadata for an uploaded document. File storage itself is a non-goal; StorageRef is opaque.</summary>
public sealed class DocumentReference
{
    private DocumentReference() { } // EF

    internal DocumentReference(
        string fileName, string contentType, long sizeBytes, string storageRef,
        Guid uploadedByUserId, DateTimeOffset uploadedAt, Guid? informationRequestId)
    {
        Id = Guid.NewGuid();
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageRef = storageRef;
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
        InformationRequestId = informationRequestId;
    }

    public Guid Id { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string StorageRef { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public Guid? InformationRequestId { get; private set; }
}

/// <summary>One audit row per action: status changes (From/To set) and other actions (From/To null).</summary>
public sealed class ClaimHistoryEntry
{
    private ClaimHistoryEntry() { } // EF

    internal ClaimHistoryEntry(
        ClaimAction action, ClaimStatus? fromStatus, ClaimStatus? toStatus, Actor actor,
        DateTimeOffset occurredAt, string? reason, decimal? amount, Guid? targetUserId)
    {
        Id = Guid.NewGuid();
        Action = action;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorUserId = actor.UserId;
        ActorRole = actor.Role;
        OccurredAt = occurredAt;
        Reason = reason;
        Amount = amount;
        TargetUserId = targetUserId;
    }

    public Guid Id { get; private set; }
    public ClaimAction Action { get; private set; }
    public ClaimStatus? FromStatus { get; private set; }
    public ClaimStatus? ToStatus { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string ActorRole { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Reason { get; private set; }
    public decimal? Amount { get; private set; }

    /// <summary>The assignee for Assign / Reassign.</summary>
    public Guid? TargetUserId { get; private set; }
}
