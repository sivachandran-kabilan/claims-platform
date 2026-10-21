# Events

> **Raised by the domain (slice 3, `src/Claims.Domain/Claims/Events/ClaimEvents.cs`); not yet persisted or published.** Outbox write: slice 4. Relay/Kafka/consumer: slice 6.
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
- `OutboxMessage` written in the same `SaveChanges` as the claim.
- Relay: every 2 s, batch 50 by `OccurredAt`, `acks=all`, idempotent producer; on failure increments `Attempts`, stores `LastError`, stops the batch.
- Consumer: dedupes via `ProcessedEvent`; writes `Notification` + `ProcessedEvent` in one transaction; commits offset after; 3 failures → DLT.
