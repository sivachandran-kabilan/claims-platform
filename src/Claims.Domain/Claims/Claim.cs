using Claims.Domain.Claims.Events;
using Claims.Domain.Common;

namespace Claims.Domain.Claims;

/// <summary>
/// The claim aggregate. Status only changes through <see cref="TransitionTo"/>, which checks
/// <see cref="ClaimTransitions"/> and writes the history row (ADR 005).
/// Money rules from ADR 006: all amounts are in the claim's single <see cref="Currency"/>.
/// Who may act (owner, assigned staff, manager) is checked in the application layer, not here.
/// </summary>
public sealed class Claim : AggregateRoot
{
    public const int NoResponseDays = 15;
    public const string NoResponseReason = "No response received";
    public const long MaxDocumentBytes = 20 * 1024 * 1024;

    private readonly List<InformationRequest> _informationRequests = [];
    private readonly List<DocumentReference> _documents = [];
    private readonly List<ClaimHistoryEntry> _history = [];

    private Claim() { } // EF

    public Guid Id { get; private set; }
    public ClaimNumber Number { get; private set; } = null!;
    public Guid ClaimantUserId { get; private set; }
    public ClaimType Type { get; private set; }
    public Market Market { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string PolicyNumber { get; private set; } = string.Empty;
    public DateOnly IncidentDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string ContactPhone { get; private set; } = string.Empty;

    /// <summary>The claimant's estimate. Informational only; never drives exposure.</summary>
    public decimal ClaimedAmount { get; private set; }
    public decimal? Reserve { get; private set; }
    public decimal? ApprovedAmount { get; private set; }

    /// <summary>What is still expected to be paid: reserve, then approved amount, then 0 once closed.</summary>
    public decimal OutstandingReserve { get; private set; }
    public decimal? SettledAmount { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }
    public string? RejectionReason { get; private set; }

    public ClaimStatus Status { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    public IReadOnlyCollection<InformationRequest> InformationRequests => _informationRequests;
    public IReadOnlyCollection<DocumentReference> Documents => _documents;
    public IReadOnlyCollection<ClaimHistoryEntry> History => _history;

    public InformationRequest? OpenInformationRequest => _informationRequests.SingleOrDefault(r => r.IsOpen);

    /// <summary>The only way to create a claim.</summary>
    public static Claim Submit(
        ClaimNumber number, Actor claimant, ClaimType type, Market market, string policyNumber,
        DateOnly incidentDate, string description, string contactPhone, decimal claimedAmount, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactPhone);

        if (claimant.Role != Roles.Claimant)
        {
            throw new BusinessRuleException("Only a claimant can submit a claim.");
        }

        // Markets are ahead of UTC (up to +11); allow "today" in any of them without per-market time zones.
        if (incidentDate > DateOnly.FromDateTime(now.UtcDateTime).AddDays(1))
        {
            throw new BusinessRuleException("Incident date cannot be in the future.");
        }

        Amounts.EnsurePositive(claimedAmount, "Claimed amount");

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            Number = number,
            ClaimantUserId = claimant.UserId,
            Type = type,
            Market = market,
            Currency = market.Currency(),
            PolicyNumber = policyNumber.Trim(),
            IncidentDate = incidentDate,
            Description = description.Trim(),
            ContactPhone = contactPhone.Trim(),
            ClaimedAmount = claimedAmount,
            Status = ClaimStatus.Submitted,
            SubmittedAt = now,
        };

        claim.Record(ClaimAction.Submit, claimant, now, from: null, to: ClaimStatus.Submitted);
        claim.Raise(new ClaimSubmitted(
            Guid.NewGuid(), now, claim.Id, number.Value, claim.ClaimantUserId, market, type, claim.Status));
        return claim;
    }

    // ---- Status changes -------------------------------------------------------------------------

    public void Assign(Guid assigneeUserId, Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.Assign);
        EnsureUser(assigneeUserId);

        AssignedToUserId = assigneeUserId;
        TransitionTo(ClaimAction.Assign, actor, now, targetUserId: assigneeUserId);
    }

    public void RequestInformation(string question, Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.RequestInformation);
        EnsureText(question, "A question");
        if (OpenInformationRequest is not null)
        {
            throw new BusinessRuleException("An information request is already open.");
        }

        _informationRequests.Add(new InformationRequest(question.Trim(), actor.UserId, now));
        TransitionTo(ClaimAction.RequestInformation, actor, now);
        Raise(new InformationRequested(Guid.NewGuid(), now, Id, Number.Value, ClaimantUserId, Market, Type, Status));
    }

    public void RespondToInformation(string response, Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.RespondToInformation);
        EnsureText(response, "A response");
        var open = OpenInformationRequest
            ?? throw new InvalidTransitionException("There is no open information request.");

        open.Respond(response.Trim(), actor.UserId, now);
        TransitionTo(ClaimAction.RespondToInformation, actor, now);
    }

    public void Approve(decimal approvedAmount, Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.Approve);
        if (Reserve is null)
        {
            throw new BusinessRuleException("A reserve must be set before the claim can be approved.");
        }

        Amounts.EnsurePositive(approvedAmount, "Approved amount");
        if (approvedAmount > Reserve)
        {
            throw new BusinessRuleException("Approved amount cannot exceed the reserve.");
        }

        ApprovedAmount = approvedAmount;
        OutstandingReserve = approvedAmount;
        DecidedAt = now;
        TransitionTo(ClaimAction.Approve, actor, now, amount: approvedAmount);
        RaiseDecided(DecisionOutcome.Approved, now, new Money(approvedAmount, Currency));
    }

    public void Reject(string reason, Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.Reject);
        EnsureText(reason, "A rejection reason");

        Close(reason.Trim(), now);
        TransitionTo(ClaimAction.Reject, actor, now, reason: RejectionReason);
        RaiseDecided(DecisionOutcome.Rejected, now, approvedAmount: null);
    }

    public void RejectForNoResponse(Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.RejectForNoResponse);
        var open = OpenInformationRequest
            ?? throw new InvalidTransitionException("There is no open information request.");
        if (now < open.RequestedAt.AddDays(NoResponseDays))
        {
            throw new BusinessRuleException($"The claimant has {NoResponseDays} days to respond.");
        }

        Close(NoResponseReason, now);
        TransitionTo(ClaimAction.RejectForNoResponse, actor, now, reason: NoResponseReason);
        RaiseDecided(DecisionOutcome.RejectedNoResponse, now, approvedAmount: null);
    }

    public void Settle(decimal amount, Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.Settle);
        Amounts.EnsurePositive(amount, "Settlement amount");
        if (ApprovedAmount is not { } approved || amount > approved)
        {
            throw new BusinessRuleException("Settlement amount cannot exceed the approved amount.");
        }

        SettledAmount = amount;
        SettledAt = now;
        OutstandingReserve = 0;
        TransitionTo(ClaimAction.Settle, actor, now, amount: amount);
        Raise(new ClaimSettled(
            Guid.NewGuid(), now, Id, Number.Value, ClaimantUserId, Market, Type, Status, new Money(amount, Currency)));
    }

    // ---- Actions that do not change status --------------------------------------------------------

    public void SetReserve(decimal amount, Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.SetReserve);
        Amounts.EnsurePositive(amount, "Reserve");

        Reserve = amount;
        OutstandingReserve = amount;
        Record(ClaimAction.SetReserve, actor, now, amount: amount);
    }

    public void Reassign(Guid newAssigneeUserId, string reason, Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.Reassign);
        EnsureUser(newAssigneeUserId);
        EnsureText(reason, "A reassignment reason");
        if (newAssigneeUserId == AssignedToUserId)
        {
            throw new BusinessRuleException("The claim is already assigned to this user.");
        }

        AssignedToUserId = newAssigneeUserId;
        Record(ClaimAction.Reassign, actor, now, reason: reason.Trim(), targetUserId: newAssigneeUserId);
    }

    public DocumentReference AddDocument(
        string fileName, string contentType, long sizeBytes, string storageRef, Actor actor, DateTimeOffset now)
    {
        EnsureAllowed(ClaimAction.AddDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRef);
        if (sizeBytes is <= 0 or > MaxDocumentBytes)
        {
            throw new BusinessRuleException($"Document size must be between 1 byte and {MaxDocumentBytes / (1024 * 1024)} MB.");
        }

        // A claimant's upload while information is requested belongs to that request.
        var linkedRequestId = actor.UserId == ClaimantUserId ? OpenInformationRequest?.Id : null;
        var document = new DocumentReference(fileName.Trim(), contentType.Trim(), sizeBytes, storageRef, actor.UserId, now, linkedRequestId);
        _documents.Add(document);
        Record(ClaimAction.AddDocument, actor, now);
        return document;
    }

    // ---- The single status path -----------------------------------------------------------------

    private void TransitionTo(
        ClaimAction action, Actor actor, DateTimeOffset now,
        string? reason = null, decimal? amount = null, Guid? targetUserId = null)
    {
        if (!ClaimTransitions.TryGetTarget(action, Status, out var target))
        {
            throw new InvalidTransitionException($"{action} is not allowed when the claim is {Status}.");
        }

        var from = Status;
        Status = target;
        Record(action, actor, now, from, target, reason, amount, targetUserId);
    }

    private void EnsureAllowed(ClaimAction action)
    {
        if (!ClaimTransitions.IsAllowed(action, Status))
        {
            throw new InvalidTransitionException($"{action} is not allowed when the claim is {Status}.");
        }
    }

    private void Record(
        ClaimAction action, Actor actor, DateTimeOffset now, ClaimStatus? from = null, ClaimStatus? to = null,
        string? reason = null, decimal? amount = null, Guid? targetUserId = null) =>
        _history.Add(new ClaimHistoryEntry(_history.Count + 1, action, from, to, actor, now, reason, amount, targetUserId));

    private void Close(string reason, DateTimeOffset now)
    {
        RejectionReason = reason;
        OutstandingReserve = 0;
        DecidedAt = now;
    }

    private void RaiseDecided(DecisionOutcome outcome, DateTimeOffset now, Money? approvedAmount) =>
        Raise(new ClaimDecided(
            Guid.NewGuid(), now, Id, Number.Value, ClaimantUserId, Market, Type, Status, outcome, approvedAmount));

    private static void EnsureText(string value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException($"{what} is required.");
        }
    }

    private static void EnsureUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }
    }
}
