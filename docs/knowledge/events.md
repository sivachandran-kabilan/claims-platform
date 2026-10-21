# Events

> Raised by the domain (`src/Claims.Domain/Claims/Events/ClaimEvents.cs`) and **written to `OutboxMessages` in the same SaveChanges (slice 4)**. Relay/Kafka/consumer: not built yet.
> Each event record also carries `EventId` and `OccurredAt` (become envelope fields).

Topic: `claims.events` (key = claimId). Dead-letter: `claims.events.dlt`. Envelope: eventId, type, version, occurredAt, claimId, payload.
Common payload: claimId, claimNumber, claimantUserId, market, type, status. No names, email, phone, description, or free-text reasons (ADR 002).

| Event | Raised when | Extra payload | Consumers |
|---|---|---|---|
| ClaimSubmitted | `Claim.Submit` | none | notification handler |
| InformationRequested | `RequestInformation` | none | notification handler |
| ClaimDecided | `Approve`, `Reject`, `RejectForNoResponse` | outcome (Approved/Rejected/RejectedNoResponse), approvedAmount? | notification handler |
| ClaimSettled | `Settle` | settledAmount (Money: amount + currency) | notification handler |

Payload field list is pinned by `EventPayloadTests` (AC-EVT-4).

## Outbox and relay (ADR 003)
- Implemented: `ClaimsDbContext.SaveChangesAsync` turns tracked aggregates' events into `OutboxMessage` rows (`Id`=eventId, `Type`=record name, `Version`=1, `ClaimId`, `Payload`=camelCase JSON with enum names, `OccurredAt`) and saves them with the claim in one transaction; events cleared only after success; outbox rows detached on failure. Sync `SaveChanges` throws (would skip the outbox). Code: `src/Claims.Infrastructure/Persistence/{ClaimsDbContext,OutboxWriter}.cs`.
- Planned:
- Relay: every 2 s, batch 50 by `OccurredAt`, `acks=all`, idempotent producer; on failure increments `Attempts`, stores `LastError`, stops the batch.
- Consumer: dedupes via `ProcessedEvent`; writes `Notification` + `ProcessedEvent` in one transaction; commits offset after; 3 failures → DLT.
