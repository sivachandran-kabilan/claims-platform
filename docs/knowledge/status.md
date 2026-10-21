# Status

Last updated: 2026-10-21 08:05 +05:30 after slice 1 (scaffold)

## Done
- Prompt 1: design review. ADRs 001–007, domain, acceptance criteria, events catalog (planned).
- Slice 1: scaffold. Solution with 4 src and 3 test projects; central package versions; SDK pinned; ProblemDetails error handling; validation pipeline; `/health`; Docker Compose with Kafka, topic init and UI; integration test host with a throwaway DB. 4 tests green.

## In progress
- (none)

## Next (in order; estimates)
1. Identity: register, login, me, users, JWT, fallback policy, roles, seeding, lockout, rate limit, first migration, role-matrix harness (40m)
2. Claim domain + state machine + domain tests (30m)
3. Submit (idempotency), get (ETag), mine, list, queue (30m)
4. Workflow commands with If-Match filter (40m)
5. Outbox, relay, Kafka, notification consumer, notifications/mine (35m)
6. Reports: workload, exposure (20m)
7. OpenAPI spec commit, Swagger filters (bearer, If-Match, Idempotency-Key), README (10m)
8. Angular: auth + claimant flow, then staff queue/actions (50m)

## Cut order if behind
1. Staff frontend → 2. `GET /users` → 3. Documents → 4. Reassign endpoint → 5. DLT (log only) → 6. Reject-no-response endpoint.
Never cut: auth + fallback policy, single transition method, outbox, role-matrix test, claimant isolation, reports.

## Known gaps and shortcuts (feed the README)
- Already cut: refresh tokens, USD exposure total, disabling accounts, no-response background sweep, manager UI.
- No token revocation (changes apply at 15-min expiry). Duplicate-registration 409 allows email enumeration.
- Notifications are in-app only; consumer runs in the same process.
- Documents are metadata only; policy number is an unvalidated reference.
- Outbox rows never cleaned up.
- `/health` is liveness only (a DB check needs an unapproved package).
- Fallback auth policy not active yet; arrives with JWT in slice 2 (only `/health` exists today).

## Open questions
- (none)

## Time used
Planned vs actual per prompt: see journal.
