namespace Claims.Domain.Claims;

public enum ClaimStatus
{
    Submitted,
    UnderReview,
    InfoRequested,
    Approved,
    Rejected,
    Settled,
}

public enum ClaimType
{
    Motor,
    Property,
}

public enum DecisionOutcome
{
    Approved,
    Rejected,
    RejectedNoResponse,
}

/// <summary>Every action on a claim; also the "Action" recorded in the history.</summary>
public enum ClaimAction
{
    Submit,
    Assign,
    RequestInformation,
    RespondToInformation,
    Approve,
    Reject,
    RejectForNoResponse,
    Settle,
    SetReserve,
    Reassign,
    AddDocument,
}
