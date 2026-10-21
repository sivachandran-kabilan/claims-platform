# ADR 003: Transactional outbox and idempotent consumer

Status: Accepted (Prompt 1, 2026-10-21)

## Context
Publishing to Kafka inside a request risks a committed claim with no event (or an event for a rolled-back claim).

## Decision
- Domain events raised by the `Claim` aggregate are written to `OutboxMessage` in the **same `SaveChanges`** as the claim. Handlers never call Kafka.
- Relay: `BackgroundService` polling every 2 s, batch of 50 ordered by `OccurredAt`. Producer `acks=all`, idempotence on. On failure: increment `Attempts`, store `LastError`, stop the batch (keeps per-claim order). Success sets `ProcessedAt`.
- Consumer: dedupes on `eventId` via `ProcessedEvent`; writes `Notification` + `ProcessedEvent` in one transaction; commits offset after. After 3 failures, publish to `claims.events.dlt` (cut fallback: log clearly with eventId).
- Message handling logic is separate from the consume loop so it is unit-testable without a broker.

## Implementation (slice 4)
- The outbox write lives in `ClaimsDbContext.SaveChangesAsync` (override): events from tracked `AggregateRoot`s become `OutboxMessage` rows in the same save. Events are cleared only after success; on failure the rows are detached so a retry can't duplicate them. Synchronous `SaveChanges` throws.
- The table write was moved forward from the relay slice so AC-CLM-1 / AC-EVT-1 pass as soon as claims can be submitted.

## Consequences
- At-least-once delivery; duplicates are expected and handled.
- Up to ~2 s publish latency. Outbox rows are never deleted in this build (README: cleanup job with more time).
