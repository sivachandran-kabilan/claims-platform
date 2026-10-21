# Status

Last updated: 2026-10-21 08:22 +05:30 after slice 2 (identity)

## Done
- Prompt 1: design review. ADRs 001–007, domain, acceptance criteria, events catalog (planned).
- Slice 1: scaffold. Solution with 4 src and 3 test projects; central package versions; SDK pinned; ProblemDetails error handling; validation pipeline; `/health`; Docker Compose with Kafka, topic init and UI; integration test host with a throwaway DB.
- Slice 2: identity. Register, login, me, POST/GET users; JWT bearer + fallback policy + role policies; lockout; rate limit; roles seeded by migration `InitialIdentity`; dev users seeded from user-secrets; role-matrix test with endpoint-coverage guard. 68 tests green (AC-AUTH-1..4, AC-USR-2).

## In progress
- (none)

## Next (in order; estimates)
1. Claim domain + state machine + domain tests (30m)
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
- Register and POST /users: no Idempotency-Key, no Location header (approved exception).
- Rate limit is per IP, in memory; lockout 15 min. Common-password list is tiny (no breach corpus).
- Dev DB is `ClaimsPlatformDev` because a pre-existing `TEST` database on the local instance owns `ClaimsPlatform.mdf`.

## Open questions
- (none)

## Time used
Planned vs actual per prompt: see journal.
