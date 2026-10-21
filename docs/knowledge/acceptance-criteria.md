# Acceptance Criteria (confirmed Prompt 1, 2026-10-21)

Format: Given / When / Then. A slice is done only when its criteria pass as automated tests (or are explicitly marked manual).

## Auth
- **AC-AUTH-1 Register.** Given a valid email and strong password, when `POST /auth/register`, then 201 and the user has role `Claimant`. A supplied role is ignored or rejected. Duplicate email → 409. Weak password → 400 with field errors.
- **AC-AUTH-2 Login.** Given valid credentials, when `POST /auth/login`, then 200 with a JWT (`sub`, `email`, `role`, `jti`, `exp` ≤ 15 min). Wrong email or password → identical generic 401. 5 failed attempts → lockout.
- **AC-AUTH-3 Protection.** Missing, expired, tampered, or wrong-issuer/audience token → 401. Wrong role → 403. Every endpoint matches the role matrix in CLAUDE.md.
- **AC-AUTH-4 Staff accounts.** Only a `ClaimManager` can create `ClaimStaff`/`ClaimManager` users.
- **AC-USR-2 List users.** `GET /users` is paged (max 100) and ClaimManager only. *(cut candidate)*

## Claims
- **AC-CLM-1 Submit.** Given a Claimant and a valid body with an `Idempotency-Key`, when `POST /claims`, then 201 with `Location`, claim number, status `Submitted`, a history row, and a `ClaimSubmitted` outbox row in the same transaction. Currency is derived from market; a client-supplied currency is ignored. Same key + same body → same result, no duplicate. Same key + different body → 422. Future incident date or amount ≤ 0 → 400 with field errors.
- **AC-CLM-2 View.** A Claimant sees only their own claims; another user's claim → 404. Staff and managers see all. `GET` returns an `ETag` and includes history (claimants see actor role, not staff ids).
- **AC-CLM-3 Assign.** ClaimStaff assigns a `Submitted` claim to themselves → `UnderReview`. Staff assigning to someone else → 403. Manager may assign to any ClaimStaff/ClaimManager. Claim not `Submitted` → 409.
- **AC-CLM-4 Request info.** The assigned staff member (or a manager) moves `UnderReview → InfoRequested` with a question; emits `InformationRequested`. Other staff → 403. The claimant can see the request.
- **AC-CLM-5 Respond.** The owning Claimant responds to the open request → `InfoRequested → UnderReview`. No open request / wrong state → 409. Another claimant → 404.
- **AC-CLM-6 Decide.** Approve or reject only from `UnderReview`. Approve requires a reserve (422 otherwise) and 0 < approvedAmount ≤ reserve (422); sets outstanding = approvedAmount. Reject requires a reason and sets outstanding = 0. Both write history and a `ClaimDecided` outbox row.
- **AC-CLM-7 Settle.** Settle only from `Approved`; 0 < amount ≤ approvedAmount (422); sets settled date and outstanding = 0; emits `ClaimSettled`. Domain test proves only `Settle` reaches `Settled`.
- **AC-CLM-8 Terminal.** Any action on `Rejected` or `Settled` → 409.
- **AC-CLM-9 Lists.** Paginated (max 100), filterable by status, market, type, assignee. No unbounded lists.
- **AC-CLM-10 Assess.** Reserve only in `UnderReview` (409 otherwise), > 0, in claim currency; writes history; sets outstanding = reserve.
- **AC-CLM-11 Reassign.** ClaimManager only; reason required (400); target must be ClaimStaff/ClaimManager (422); only in UnderReview/InfoRequested/Approved (409); writes history. *(cut candidate: keep domain test)*
- **AC-CLM-12 Reject no response.** Only from `InfoRequested`; open request < 15 days old → 422 (fake clock); reason fixed "No response received"; outstanding = 0; emits `ClaimDecided`. *(cut candidate: keep domain test)*
- **AC-CLM-13 Documents.** Metadata only; content type allow-listed and size capped (400); terminal → 409; another claimant → 404; unassigned staff → 403. *(cut candidate)*

## Notifications
- **AC-NOT-1** `GET /notifications/mine` returns only the caller's latest 50, newest first.

## Reports (ClaimManager; staff see own workload row + queue)
- **AC-RPT-1 Workload.** Per assignee: open claims (UnderReview, InfoRequested, Approved) by status, average age in days from submission, oldest claim number and age; plus the unassigned (`Submitted`) queue. Staff with zero claims appear for managers. ClaimStaff get only their own row plus the queue.
- **AC-RPT-2 Exposure.** Sum of `OutstandingReserve` on open claims grouped by market, type, status, currency, with claim count and unreserved count. Excludes `Rejected` and `Settled`. Totals per currency only. Built with SQL projections (no entity loading).

## Events
- **AC-EVT-1 Outbox.** Event rows are written in the same transaction as the claim change. If the transaction rolls back, no event exists.
- **AC-EVT-2 Relay.** Automated: with a failing fake publisher, the row stays unprocessed and `Attempts` increments; with a working one, it is marked processed. Manual: real broker down then up, event arrives.
- **AC-EVT-3 Consumer.** Automated: a duplicate `eventId` is processed once. Manual: poison message goes to `claims.events.dlt` (or is clearly logged).
- **AC-EVT-4 Payloads.** The four events carry only ids, claim number, market, type, status, outcome, amounts. No PII or free text.

## Frontend (manual, with written steps)
- **AC-FE-1** Claimant can register, log in, submit a claim, see status and history, and answer an information request.
- **AC-FE-2** Staff can see the queue, assign to self, and request info / approve / reject / settle.
- **AC-FE-3** Manager can see workload and exposure. *(first cut)*
- **AC-FE-4** Unauthorized routes redirect to login; 403 shows a clear message. The API remains the enforcement point.

## Cross-cutting
- **AC-X-1 Concurrency.** Every state-changing call requires `If-Match`: stale → 412, missing → 428.
- **AC-X-2 Invalid transitions.** Every (status × action) pair not in the table → 409, covered by one generated domain `[Theory]`.
- No PII in logs. Errors are ProblemDetails. Build is warning-free and all tests pass.
