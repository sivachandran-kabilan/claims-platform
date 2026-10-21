# ADR 002: REST for user interactions, Kafka for domain events

Status: Accepted (Prompt 1, 2026-10-21)

## Context
The brief requires both REST and Kafka and asks us to justify which carries what.

## Decision
- **REST** for every user-facing command and query: the user needs an immediate answer (201, 409, 422, data).
- **Kafka** for facts that other components react to asynchronously. Topic `claims.events`, key = `claimId` (per-claim ordering), envelope `eventId, type, version, occurredAt, claimId, payload`.
- Events: `ClaimSubmitted`, `InformationRequested`, `ClaimDecided` (outcome Approved / Rejected / RejectedNoResponse), `ClaimSettled`.
- No events for assign, reassign, reserve, or documents: no consumer needs them; the audit table records them.
- Consumer: in-process notification handler writing in-app `Notification` rows.
- Payloads carry ids, claim number, market, type, status, outcome, amounts. No names, email, phone, description, or free-text reasons.

## Consequences
- The consumer lives in the same deployable, so it demonstrates the integration seam and at-least-once handling, not distribution.
- Adding a consumer later (payments, analytics) needs no change to request handlers.
