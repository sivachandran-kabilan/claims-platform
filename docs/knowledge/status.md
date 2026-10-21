# Status

Last updated: 2026-10-21 after Prompt 1 (design review)

## Done
- Prompt 1: design review. ADRs 001–007, domain, acceptance criteria, events catalog (planned) written. No code yet.

## In progress
- (none)

## Next (in order; estimates)
1. Scaffold solution, DI, ProblemDetails, docker compose, test projects, baseline migration (25m)
2. Identity: register, login, me, users, JWT, fallback policy, roles, seeding, lockout, rate limit, role-matrix harness (40m)
3. Claim domain + state machine + domain tests (30m)
4. Submit (idempotency), get (ETag), mine, list, queue (30m)
5. Workflow commands with If-Match filter (40m)
6. Outbox, relay, Kafka, notification consumer, notifications/mine (35m)
7. Reports: workload, exposure (20m)
8. OpenAPI spec commit, README (10m)
9. Angular: auth + claimant flow, then staff queue/actions (50m)

## Cut order if behind
1. Staff frontend → 2. `GET /users` → 3. Documents → 4. Reassign endpoint → 5. DLT (log only) → 6. Reject-no-response endpoint.
Never cut: auth + fallback policy, single transition method, outbox, role-matrix test, claimant isolation, reports.

## Known gaps and shortcuts (feed the README)
- Already cut: refresh tokens, USD exposure total, disabling accounts, no-response background sweep, manager UI.
- No token revocation (changes apply at 15-min expiry). Duplicate-registration 409 allows email enumeration.
- Notifications are in-app only; consumer runs in the same process.
- Documents are metadata only; policy number is an unvalidated reference.
- Outbox rows never cleaned up.

## Open questions
- (none)

## Time used
Planned vs actual per prompt: see journal.
