using Claims.Domain.Common;

namespace Claims.Domain.Claims.Events;

// Payloads carry ids, claim number, market, type, status, outcome and amounts only.
// No names, contact details, description or free-text reasons (ADR 002, AC-EVT-4).

/// <summary>An event about one claim; ClaimId becomes the Kafka message key.</summary>
public interface IClaimEvent : IDomainEvent
{
    Guid ClaimId { get; }
}

public sealed record ClaimSubmitted(
    Guid EventId, DateTimeOffset OccurredAt, Guid ClaimId, string ClaimNumber, Guid ClaimantUserId,
    Market Market, ClaimType Type, ClaimStatus Status) : IClaimEvent;

public sealed record InformationRequested(
    Guid EventId, DateTimeOffset OccurredAt, Guid ClaimId, string ClaimNumber, Guid ClaimantUserId,
    Market Market, ClaimType Type, ClaimStatus Status) : IClaimEvent;

public sealed record ClaimDecided(
    Guid EventId, DateTimeOffset OccurredAt, Guid ClaimId, string ClaimNumber, Guid ClaimantUserId,
    Market Market, ClaimType Type, ClaimStatus Status, DecisionOutcome Outcome, Money? ApprovedAmount) : IClaimEvent;

public sealed record ClaimSettled(
    Guid EventId, DateTimeOffset OccurredAt, Guid ClaimId, string ClaimNumber, Guid ClaimantUserId,
    Market Market, ClaimType Type, ClaimStatus Status, Money SettledAmount) : IClaimEvent;
