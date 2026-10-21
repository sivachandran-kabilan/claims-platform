# Status

Last updated: 2026-10-21 09:12 +05:30 after slice 5 (workflow actions)

## Done
- Prompt 1: design review. ADRs 001–007, domain, acceptance criteria, events catalog (planned).
- Slice 1: scaffold. Solution with 4 src and 3 test projects; central package versions; SDK pinned; ProblemDetails error handling; validation pipeline; `/health`; Docker Compose with Kafka, topic init and UI; integration test host with a throwaway DB.
- Slice 2: identity. Register, login, me, POST/GET users; JWT bearer + fallback policy + role policies; lockout; rate limit; roles seeded by migration `InitialIdentity`; dev users seeded from user-secrets; role-matrix test with endpoint-coverage guard. 68 tests green (AC-AUTH-1..4, AC-USR-2).
- Slice 3: claim domain. `Claim` aggregate, action-keyed transition table, money rules, history, 4 domain events. 120 domain tests (188 total), including every action × status pair against an independent expected table.
- Slice 4: claims persistence + API. EF mapping (rowversion shadow, ClaimNumbers sequence, indexes), outbox write in SaveChangesAsync, idempotent submit, GET by id (ETag, claimant view), mine, list (filters/sort/paging), queue. Migration `AddClaims`. 254 tests green (AC-CLM-1, AC-CLM-2, AC-CLM-9, AC-EVT-1).
- Slice 5: workflow. 10 action endpoints over one `ClaimWorkflow` runner (428 → 404 → 403 → 412 → domain), act rules, assignee check, `Claim.UpdatedAt` (ETag always changes), migration `AddClaimUpdatedAt`. 380 tests green (AC-CLM-3..8, 10..13, AC-X-1).

## In progress
- (none)

## Next (in order; estimates)
1. Outbox relay, Kafka, notification consumer, notifications/mine (30m)
2. Reports: workload, exposure (20m)
3. OpenAPI spec commit, Swagger filters (If-Match, Idempotency-Key), README (10m)
4. Angular: auth + claimant flow, then staff queue/actions (50m)

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
- Idempotency records are never cleaned up; keys are per user.
- The load-to-save race (EF rowversion check → 412) is not exercised by a deterministic test; sequential stale-ETag cases are.
- If-Match `*` is not supported (412).
- Handlers are covered by integration tests, not mocked unit tests (no in-memory EF provider approved).
- Register and POST /users: no Idempotency-Key, no Location header (approved exception).
- Rate limit is per IP, in memory; lockout 15 min. Common-password list is tiny (no breach corpus).
- Incident date allows UTC today + 1 day (markets ahead of UTC); no per-market time zones.
- Dev DB is `ClaimsPlatformDev` because a pre-existing `TEST` database on the local instance owns `ClaimsPlatform.mdf`.

## Open questions
- (none)

## Time used
Planned vs actual per prompt: see journal.
