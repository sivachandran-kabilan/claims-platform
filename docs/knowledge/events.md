# Events

> **Implemented end to end** (slices 3–6): raised by the domain (`src/Claims.Domain/Claims/Events/ClaimEvents.cs`), written to `OutboxMessages` in the same SaveChanges, published by the relay, consumed by the notification handler.
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

## Wire format
Key = claimId (string). Value = `EventEnvelope` JSON (`src/Claims.Application/Common/Messaging/EventEnvelope.cs`), camelCase, enums as names:
`{eventId, type, version (1), occurredAt, claimId, payload: {...event record...}}`. Verified on the real topic: no PII.

## Outbox → Kafka (ADR 003)
- Write: `ClaimsDbContext.SaveChangesAsync` adds outbox rows with the claim change (one transaction); events cleared only after success.
- Relay: `OutboxRelay` (BackgroundService) runs `OutboxProcessor.ProcessBatchAsync` every `Kafka:RelayIntervalSeconds` (2). Up to 50 unprocessed rows, oldest first; publish one at a time; success → `ProcessedAt`; failure → `Attempts+1`, `LastError`, stop batch (order kept), retry next pass. No attempt cap (failures are broker-level).
- Producer (`KafkaEventPublisher`): `acks=all`, idempotence on, 10 s message timeout; awaits the delivery report.

## Consumer (`NotificationConsumer`, group `claims-notifications`)
- Manual commit after each message is handled or dead-lettered (at-least-once).
- `MessageDispatcher`: unparseable → `claims.events.dlt` at once; handler failure → retry after 1 s, 2 s → DLT. DLT keeps key/value; headers `x-error-type`, `x-original-topic`.
- `ClaimEventNotificationHandler`: skips seen `eventId` (`ProcessedEvents`); writes `Notification` + `ProcessedEvent` in one SaveChanges; a racing duplicate's PK violation is treated as already processed. Unknown types are recorded without a notification.
- Notifications go to the claimant (`claimantUserId` from the payload); text uses the claim number only.

## Switches
`Kafka:Enabled=false` (integration tests) registers no producer, relay or consumer. With Kafka stopped, the API still works and the outbox catches up on restart (verified manually).
