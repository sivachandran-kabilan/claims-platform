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
