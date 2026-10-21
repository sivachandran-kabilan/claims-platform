using Claims.Domain.Common;

namespace Claims.Domain.Claims.Events;

// Payloads carry ids, claim number, market, type, status, outcome and amounts only.
// No names, contact details, description or free-text reasons (ADR 002, AC-EVT-4).

public sealed record ClaimSubmitted(
    Guid EventId, DateTimeOffset OccurredAt, Guid ClaimId, string ClaimNumber, Guid ClaimantUserId,
    Market Market, ClaimType Type, ClaimStatus Status) : IDomainEvent;

public sealed record InformationRequested(
    Guid EventId, DateTimeOffset OccurredAt, Guid ClaimId, string ClaimNumber, Guid ClaimantUserId,
    Market Market, ClaimType Type, ClaimStatus Status) : IDomainEvent;

public sealed record ClaimDecided(
    Guid EventId, DateTimeOffset OccurredAt, Guid ClaimId, string ClaimNumber, Guid ClaimantUserId,
    Market Market, ClaimType Type, ClaimStatus Status, DecisionOutcome Outcome, Money? ApprovedAmount) : IDomainEvent;

public sealed record ClaimSettled(
    Guid EventId, DateTimeOffset OccurredAt, Guid ClaimId, string ClaimNumber, Guid ClaimantUserId,
    Market Market, ClaimType Type, ClaimStatus Status, Money SettledAmount) : IDomainEvent;
