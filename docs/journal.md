# Journal

History of prompts and AI decisions for the assessment panel. Append only; do not rewrite earlier entries.

---

## PROMPT1: Design review (2026-10-21)

**Asked:** Act as a senior .NET architect. List ambiguities, propose entities, review the state machine for gaps and bypasses, decide REST vs Kafka per interaction, confirm or challenge the monolith, outbox, role matrix and JWT design, review the acceptance criteria, define workload and exposure as queries, propose a build order with a cut order, and name the risks. Ask clarifying questions first.

**Process:** The AI read the knowledge base and asked 13 clarifying questions, each with a suggested default. I answered. The AI wrote the full design in chat without touching files. After I approved it, the AI wrote the ADRs and KB updates.

### 1. What I directed the AI to do
- Produce a full design review of the claims platform before any code, following CLAUDE.md and the knowledge base.
- Ask clarifying questions first and wait for answers. Then write the design without editing files. Only after approval, write ADRs 001–007, update the KB, and start this journal.

### 2. What I accepted
- Modular monolith with Claims and Identity modules, one `DbContext` (so the outbox shares the claim's transaction), MediatR vertical slices.
- REST for every user interaction. Kafka for four events: `ClaimSubmitted`, `InformationRequested`, `ClaimDecided`, `ClaimSettled`. No events for assign, reassign, reserve or documents, because no consumer needs them.
- Transactional outbox with a polling relay. Idempotent in-process notification consumer writing in-app notifications, with a DLT.
- Single private `TransitionTo` with an allowed-transition table. 409 for an invalid transition, 422 for a broken business rule.
- Money rules: currency derived from market; reserve empty until staff assess; `OutstandingReserve` stored as one column; single settlement ≤ approved amount.
- One `ClaimHistoryEntry` audit table for both status and non-status actions.
- 15-day no-response rejection as a manual staff action checked by the domain, with no scheduler.
- Policy number as a string, not an aggregate (changes CLAUDE.md).
- Local SQL Server test DB instead of Testcontainers. Hand-written `FakeTimeProvider`.
- The AI's corrections to the acceptance criteria: AC-CLM-5 had a non-existent request id; concurrency moved to cross-cutting; added AC-CLM-10..13, AC-NOT-1, AC-USR-2, AC-EVT-4; AC-EVT-2/3 and frontend made partly manual.
- Role matrix additions: `GET /users` (manager), unassigned staff get 403 rather than 404, staff workload includes the unassigned queue.
- Build order (~300 min) and cut order.

### 3. What I challenged
- **Approved amount.** The AI proposed that approve should lock `ApprovedAmount = Reserve`. I challenged this: the approved amount is entered manually and must be ≤ the reserve. The AI then flagged that "less than" was ambiguous (strict or not), and I confirmed ≤.
- **Raised by the AI and accepted by me:**
  - The KB state machine was missing `InfoRequested → Rejected` (no response), "approve needs a reserve", assess and reassign.
  - MediatR 13+ and FluentAssertions 8+ are commercially licensed, so versions are pinned.
  - An Angular client generator is a new npm package that needs approval.

### 4. What I overrode / cut for time
- **Overrode:** the approved-amount default (see above). For the Angular client, chose an npm OpenAPI generator over hand-written typed services.
- **Packages:** pinned MediatR 12.x and FluentAssertions 7.x.
- **Cut for time:** refresh tokens, USD exposure total (FX), disabling staff accounts, a background job for the 15-day rejection, the manager dashboard UI, partial settlements, Testcontainers.
- **Cut order if behind:** staff UI → `GET /users` → documents → reassign endpoint → DLT → reject-no-response endpoint.

**Verification:** Design only, so there's no build or test yet. Each ADR and KB file was cross-checked against CLAUDE.md, and the CLAUDE.md conflicts were updated in the same change.

**Time:** _(20 mins)_

---

## PROMPT2: Slice 1, scaffold (2026-10-21)

**Started:** 2026-10-21 07:49 +05:30 · **Finished:** 2026-10-21 08:04 +05:30 · **Time spent:** 15 min (planned 25 min)

**Asked:** Check the machine for every dependency and runtime version before scaffolding, show a detailed scaffold plan, then build it.

### 1. What I directed the AI to do
- Check the environment before writing anything: .NET SDKs and runtimes, SQL Server, Docker, Node, ports, registry access, and package versions inside the approved major versions.
- Produce a detailed scaffold plan and wait for approval. Then build the solution, Docker Compose, test host and docs.

### 2. What I accepted
- `global.json` pinning SDK 8.0.426. SDK 10 is also installed and would otherwise build and template everything as net10.
- Central package management (`Directory.Packages.props`) with transitive pinning, and `TreatWarningsAsErrors` in `Directory.Build.props`.
- `Microsoft.NET.Test.Sdk` 18.11.0 added to the approved list. `coverlet` dropped.
- No empty baseline migration: the first real migration comes with Identity in slice 2. `/health` is liveness only, because a DB health check needs an unapproved package.
- `GlobalExceptionHandler`: validation → 400, invalid transition → 409, business rule → 422, anything else → a generic 500 that logs the exception type only. Bare 404/405 responses return ProblemDetails.
- Kafka 4.3.1 in KRaft mode with separate host and in-Docker listeners, auto topic creation off, and a one-off `kafka-init` container that creates `claims.events` (3 partitions) and `claims.events.dlt`.
- Integration tests run against a throwaway `ClaimsPlatform_Tests_<guid>` database on the local instance in the `Testing` environment (no Swagger, no user-secrets), dropped after the run.

### 3. What I challenged
- **Swashbuckle version.** The AI proposed 6.9.0 as "the .NET 8-era line". I asked why. The AI checked the package metadata and admitted the reason was weak. It recommended 9.0.6 instead: built for net8 and the last line on Microsoft.OpenApi 1.x, whereas 10.x moves to the breaking 2.x model. Accepted and recorded in ADR 007.

### 4. What I overrode / cut for time
- **Overrode:** Swashbuckle 6.9.0 → 9.0.6.
- **Deferred:** the fallback auth policy moves to slice 2 together with the JWT scheme. Until then `/health` is the only endpoint.
- **Cut:** DB health check (needs a package), coverage tooling.

**Issues hit during the build (fixed):**
- A shell heredoc quoting error. The AI rewrote those files with the file tool.
- Test project reference paths were mangled by escaping. Fixed.
- MediatR 12.5's `RequestHandlerDelegate` takes a `CancellationToken`. The behavior now passes the token on, and the tests were updated.

**Verification:**
- `dotnet restore` reported no vulnerability warnings, and `dotnet build -warnaserror` succeeded.
- `dotnet test`: 4 passed (2 application, 2 integration). The domain test project is empty until slice 3.
- `docker compose up -d`: Kafka healthy, both topics listed, Kafka UI returns 200 on :8080.
- `dotnet ef dbcontext info` resolves the context against `localhost/ClaimsPlatform`.
- `dotnet run`: `/health` returns 200 `Healthy`, an unknown route returns 404 `application/problem+json`, and Swagger JSON returns 200 in Development.

---

## PROMPT3: Slice 2, identity (2026-10-21)

**Started:** 2026-10-21 08:07 +05:30 · **Finished:** 2026-10-21 08:21 +05:30 · **Time spent:** ~16 min (planned 45 min)

**Asked:** Plan the Identity module, seeding the CLAUDE.md test accounts (manager, staff, user) as dev users. After approval, build it.

### 1. What I directed the AI to do
- Use the CLAUDE.md test credentials to seed manager, staff and claimant accounts in Development.
- Present an Identity plan (register, login, me, users, JWT, fallback policy, lockout, rate limit, role-matrix test), then build it after my "proceed".

### 2. What I accepted
- Seed credentials stay out of source. `DevUserSeeder` reads `Seed:Manager|Staff|Claimant` from user-secrets, and the AI set those secrets to the CLAUDE.md accounts. A Claimant seed was added, since CLAUDE.md only mentioned manager and staff.
- Roles seeded by migration (`HasData`, fixed ids) in every environment. Users seeded only in Development. Migrations applied manually; the seeder skips with a warning if they're pending.
- Register and `POST /users` are exempt from `Idempotency-Key` and `Location`, because the unique email makes retries safe (409).
- Rate limit of 10 requests/min per IP on register and login. Lockout of 15 min after 5 failures.
- Guid Identity keys. User and role inserted in one transaction. `SignInManager.CheckPasswordSignInAsync` for lockout-aware checks. A dummy hash check for unknown emails (timing). A small common-password list.
- Token issuing in the API next to bearer validation. `JwtOptions` validated at startup (key ≥ 32 bytes, lifetime ≤ 15 min). `ValidAlgorithms = [HS256]` rejects `alg=none`.
- Role-matrix test as data, plus `EveryEndpoint_HasMatrixRow`, which fails the build if an endpoint has no row.

### 3. What I challenged
- Nothing this round; I accepted the plan as presented.
- **Raised by the AI during the build:**
  - The CLAUDE.md test passwords equal their emails, so a "password ≠ email" rule can't be added. Recorded as a dev-only trade-off in ADR 004.
  - Adding the fallback policy changed a slice-1 test: anonymous requests to unknown URLs now get 401, not 404. The AI split the test (anonymous → 401, authenticated → 404) rather than weakening it, and recorded the behaviour in ADR 004.

### 4. What I overrode / cut for time
- **Dev DB renamed to `ClaimsPlatformDev`.** A pre-existing `TEST` database on the local instance owns `ClaimsPlatform.mdf`. The AI left `TEST` untouched and changed our database name instead (CLAUDE.md, README and status updated).
- **Cut / deferred:** refresh tokens, breached-password corpus, distributed rate limiting, `GET /users/{id}`.

**Issues hit during the build (fixed):**
- `dotnet user-secrets init` had left the Api csproj messy; rewritten cleanly.
- The first manual run returned 500 on login. EF couldn't translate a filter applied after projecting into a record constructor; fixed by filtering on entity columns and projecting last.
- The same run showed ASP.NET's `ExceptionHandlerMiddleware` logging full exception messages, which can contain data such as emails. That log is disabled, and our handler logs type + stack trace only.
- A `.Result` call slipped into a test and was replaced with `await` (CLAUDE.md forbids `.Result`).

**Verification:**
- `dotnet build -warnaserror` succeeded.
- `dotnet test`: 68 passed (13 unit, 55 integration). The throwaway test databases were dropped afterwards (0 left).
- Mutation check: deleting the `GET /users` matrix row makes `EveryEndpoint_HasMatrixRow` fail and name the endpoint; the row was then restored.
- Manual run: all three seeded accounts log in, `/auth/me` returns the right role, the manager lists users (200), staff get 403, and an anonymous `/me` gets 401. The API log has no failures and no emails.

---

## PROMPT4: Slice 3, claim domain and state machine (2026-10-21)

**Started:** 2026-10-21 08:26 +05:30 (plan requested) · **Finished:** 2026-10-21 08:36 +05:30 · **Time spent:** ~10 min (planned 30 min)

**Asked:** Present the slice 3 plan (the Claim aggregate, state machine, money rules, events, domain tests). After my OK, build it.

### 1. What I directed the AI to do
- Plan, then build, the pure domain: the `Claim` aggregate with one method per action, the single status path, money rules, history and the four domain events. No API or database yet.
- Prove every valid and invalid transition with tests.

### 2. What I accepted
- **Action-keyed transition table** `(action, current status) → new status` in `ClaimTransitions`, plus allowed-status sets for the actions that don't change status. All "what's allowed when" lives in one file.
- **Amount storage:** one `Currency` column and plain 2-dp `decimal` amounts on the claim. `Money` is used only where an amount leaves the claim (events, DTOs, reports). Mixing currencies within a claim is impossible by construction; EF Core 8 can't map optional `Money` complex types cleanly.
- Domain methods take `DateTimeOffset now` (supplied by the handler from `TimeProvider`), which keeps them pure and the tests clock-free.
- The domain enforces status and money rules only. *Who* may act stays in the application layer (`ClaimAccess`, next slices).
- The outbox **table write** moves forward to slice 4, so AC-CLM-1 passes completely there. The relay and Kafka stay later.
- Reassigning to the current assignee → 422. The 15-day boundary counts exactly 15 days as allowed. Status is checked before business rules (409 takes precedence over 422).

### 3. What I challenged
- Nothing this round; I accepted the plan as presented.
- **Raised by the AI in the plan:** the Prompt 1 design (ADR 005, `domain.md`) described the table as `(from → to)` pairs, which is bypassable:
  - `Assign` on an InfoRequested claim would ride the claimant-response edge to UnderReview.
  - `Reject` on an InfoRequested claim would ride the no-response edge, rejecting with free text before 15 days.
  - Fixed with the action-keyed table; ADR 005 amended.

### 4. What I overrode / cut for time
- **Assumption added (flag for review):** the incident date may be up to UTC today + 1 day, because all markets are ahead of UTC (up to +11) and there are no per-market time zones. Otherwise a Sydney claimant reporting "today" in the morning would be rejected as a future date.
- **Cut / deferred:** partial settlements, per-market rules, a document content-type allow-list (validator in the documents slice), RowVersion (an EF concern in slice 4).

**Verification:**
- `dotnet build -warnaserror` succeeded.
- `dotnet test`: 188 passed (120 domain, 13 application, 55 integration).
- Every action × status pair (10 × 6) is checked against an expected table written independently of the production table.
- Mutation check: re-adding the `(Reject, InfoRequested)` bypass made 3 tests fail (`Reject_FromInfoRequested_IsInvalidTransition`, the matrix case, `ProductionTable_MatchesExpectedTable`); the table was then restored.

---

## PROMPT5: Slice 4, claims persistence + submit/read API (2026-10-21)

**Started:** 2026-10-21 08:40 +05:30 (plan requested) · **Finished:** 2026-10-21 08:55 +05:30 · **Time spent:** ~15 min (planned 40 min)

**Asked:** Present the slice 4 plan (EF mapping, outbox write, submit/get/mine/list/queue). After my answers, build it.

### 1. What I directed the AI to do
- Persist the claim aggregate and write domain events to the outbox in the same transaction.
- Expose idempotent submit, claim detail with ETag, the claimant's own list, the staff list with filters and paging, and the unassigned queue, all protected by the role matrix and claimant isolation.

### 2. What I accepted
- **`IClaimsDbContext` port in Application:** handlers query EF directly, so each slice keeps its queries. Application now references `Microsoft.EntityFrameworkCore` 8.0.32.
- **Claimant view hides insurer-internal data:** reserve, outstanding reserve, staff/actor ids, and reserve/reassignment history entries.
- **Idempotency inside the submit handler:** keys per user, max 100 characters, SHA-256 of the request; same key + different body → 422; a concurrent duplicate falls back to the winner's claim. Records are never cleaned up (gap).
- **Data choices:**
  - Enums stored and sent as strings.
  - A global `ClaimNumbers` sequence (gaps possible).
  - Sorting on `submittedAt` only; the queue is oldest first.
- **Persistence design:**
  - The outbox write is a `ClaimsDbContext.SaveChangesAsync` override: events go out only after a successful save, and outbox rows are detached on failure. Sync `SaveChanges` throws.
  - The rowversion is an EF shadow property (the ETag source), keeping the domain persistence-free.
  - Split queries are the global default.

### 3. What I challenged
- Nothing this round; I answered the plan's questions (data-access port, claimant view) and accepted the defaults.
- **Raised by the AI during the build:**
  - `AsSplitQuery` needs the relational EF package in Application. Instead of adding a package, split queries were made the global default in Infrastructure.
  - History rows written in one request share a timestamp, so a `Sequence` column was added to `ClaimHistoryEntry` for a stable audit order (small domain change).
  - Child-only changes (e.g. AddDocument) won't bump the claim's rowversion, so the ETag wouldn't change. Logged as a gap for the workflow slice.

### 4. What I overrode / cut for time
- **Cut / deferred:** idempotency record cleanup; sorting beyond `submittedAt`; mocked handler unit tests (handlers are covered end to end against SQL Server, since no in-memory provider is approved).

**Verification:**
- `dotnet build -warnaserror` succeeded.
- `dotnet test`: 254 passed (120 domain, 26 application, 108 integration).
- **Mutation checks:**
  - Removing the outbox `AddRange` made 4 tests fail (outbox row, no-PII payload, idempotent replay count, full lifecycle).
  - Removing the claimant filter in `VisibleTo` made 2 tests fail (other claimant gets 404, mine shows only own).
  - Both files were restored.
- **Manual run:** the seeded `user@test.com` submitted an Australia claim → 201, `Location`, `ETag`, AUD currency, claimant view with internal fields null; `staff@test.com` saw it in the queue. The API log had no failures, emails or phone numbers.
