# CLAUDE.md: APAC Claims Platform
Claims platform for a multi-market insurer (motor and property, six APAC markets). Claimants report and track claims; claims staff assess and decide; managers see workload and liability exposure.

This is a time-boxed assessment (5-hour cap). Prefer small, correct, explainable code over breadth. Keep designs simple and say when you take a shortcut.

> Defaults were confirmed in the design review (Prompt 1); see `docs/adr/`. Update this file when a decision changes.

## Constraints and non-goals
**Hard constraints**
- 5-hour cap for the whole build. If a task threatens the budget, propose a cut instead of half-building it.
- .NET 8, EF Core, SQL Server, Angular, Kafka via Docker. No new packages without asking.
- Everything must run locally with documented commands (`docker compose up -d`, migrations, `dotnet run`, `ng serve`).
- All code is written in this repo during this build. Don't copy from other projects.
- I must be able to explain every line. Prefer simple, conventional solutions over clever ones.

**Non-goals (do not build; mention in README as "with more time")**
- Real payments or payment gateways; real file storage (store document references only).
- A policy administration engine (policy number is a reference only).
- Per-market business rules, localization, or real FX integration.
- Splitting into multiple services, an API gateway, sagas, or a separate read database.
- External identity provider, MFA, password reset emails, social login.
- Fraud detection, SLA engines, priority scoring, or fast-track rules (not in the brief).

## Knowledge base (read this instead of scanning code)
Current-state docs live in `docs/knowledge/`. They exist so you don't re-read the codebase every prompt.

@docs/knowledge/index.md
@docs/knowledge/status.md

Rules:
1. **Start of every task:** read `index.md` and `status.md` (loaded above), then only the specific KB files relevant to the task (`domain.md`, `api.md`, `events.md`, `acceptance-criteria.md`, `decisions.md`). Open source files only for the area you're about to change.
2. **End of every slice:** update the affected KB files and `status.md` **in the same commit** as the code (new endpoints in `api.md`, new events in `events.md`, rule changes in `domain.md`, decisions in `decisions.md`, done/next/known gaps in `status.md`).
3. **If the KB and the code disagree, the code wins.** Fix the KB and tell me what was stale.
4. Before risky or cross-cutting changes (auth, state machine, outbox), verify the KB against the code first.
5. Keep KB files short and factual. Link to code paths instead of pasting code. Keep `index.md` and `status.md` under ~60 lines because they load every session.
6. The KB is current state for you. `docs/journal.md` is history and AI decisions for the assessment panel. Don't mix them.
7. **End of every slice:**  journal.md should be populated with a concise summary structured around the 4 things the brief explicitly
  asked for:
  1. What you directed the AI to do: (e.g., "Scaffold a .NET 8 Clean Architecture modular monolith with Kafka and outbox pattern").
  2. What you accepted: (e.g., "MediatR vertical slices, FluentValidation pipeline behavior, EF Core configurations").
  3. What you challenged: (e.g., "AI initially suggested a generic 'Status' setter; challenged it to enforce a strict action-keyed state machine").
  4. What you overrode / cut for time: (e.g., "Cut full blob upload storage to metadata references; deferred complex FX conversion across currencies").

## Acceptance criteria and Definition of Done
Feature-level criteria (Given/When/Then) are in `docs/knowledge/acceptance-criteria.md`. Treat them as the spec for each slice: before coding, restate the criteria that apply; after coding, show which tests prove each one. If a criterion is wrong or missing, tell me and propose a change rather than silently deviating.

**A slice is done only when all of these are true:**
- [ ] The applicable acceptance criteria pass as automated tests (or are explicitly marked manual with steps).
- [ ] `dotnet build -warnaserror` and `dotnet test` pass.
- [ ] Authorization is explicit on every new endpoint and matches the role matrix; a role-matrix test row exists.
- [ ] Every new state change goes through the single transition method, and invalid transitions are tested.
- [ ] Errors return ProblemDetails; no PII in logs.
- [ ] Knowledge base and `status.md` are updated (rule 2 above).
- [ ] You listed edge cases or bypasses you may have missed.
- [ ] I've been reminded to add a journal entry and commit (commit format - feature_[meaningful_message]_[COMMITNO]_DDMMYYYY/ bugfix_[meaningful_message]_[COMMITNO]_DDMMYYYY). (e.g., feature_submit_claim_01_21102026)

## Stack
- Backend: .NET 8, ASP.NET Core (MVC controllers with [ApiController], attribute routing), EF Core 8, SQL Server (local instance, connection string in appsettings / user-secrets)
- Patterns: Clean Architecture, vertical slices, CQRS with MediatR, FluentValidation
- Messaging: Kafka (Docker, single node, KRaft), `Confluent.Kafka`
- Auth: ASP.NET Core Identity (user store + password hashing) on the same `DbContext`, JWT bearer tokens issued by our own Identity module
- Frontend: Angular (standalone components), typed client generated from the OpenAPI spec
- Tests: xUnit, FluentAssertions, NSubstitute, `WebApplicationFactory`

## Commands
```bash
dotnet restore
dotnet tool restore           # dotnet-ef (pinned in .config/dotnet-tools.json)
dotnet build -warnaserror
dotnet test
dotnet test tests/Claims.Domain.Tests tests/Claims.Application.Tests   # unit tests only (no DB)
dotnet ef migrations add <Name> -p src/Claims.Infrastructure -s src/Claims.Api -o Persistence/Migrations
dotnet ef database update -p src/Claims.Infrastructure -s src/Claims.Api
docker compose up -d          # Kafka (+ Kafka UI)
dotnet user-secrets set "Jwt:Key" "<32+ byte random string>" --project src/Claims.Api
dotnet user-secrets set "ConnectionStrings:Claims" "Server=localhost;Database=ClaimsPlatformDev;Integrated Security=true;TrustServerCertificate=true" --project src/Claims.Api
dotnet user-secrets set "Seed:Manager:Email" "<email>" --project src/Claims.Api   # + Seed:Manager:Password, Seed:Staff:Email/Password, Seed:Claimant:Email/Password
dotnet run --project src/Claims.Api
cd web && npm ci && npx ng serve   # npm run gen:api regenerates the typed client from docs/openapi.json
```
Run `dotnet build` and `dotnet test` after every change. Fix failures before reporting done.

## Test Accounts [email:password]
Development only. Seeded at startup by `DevUserSeeder` from user-secrets (`Seed:Manager|Staff|Claimant`), not from source.
manager@test.com:manager@test.com
staff@test.com:staff@test.com
user@test.com:user@test.com

## Structure
```
src/
  Claims.Domain/          # Aggregates, value objects, enums, domain events. No dependencies.
  Claims.Application/     # Commands, queries, handlers, validators, DTOs, ports (interfaces)
                          #   Claims/<Feature>/ and Identity/<Feature>/ (Register, Login, ...)
  Claims.Infrastructure/  # EF Core, outbox, Kafka publisher/consumer, repositories
  Claims.Api/             # Endpoints, auth stub, middleware, DI, Program.cs
tests/
  Claims.Domain.Tests/
  Claims.Application.Tests/
  Claims.Api.IntegrationTests/
web/                      # Angular app
docs/
  adr/                    # Architecture decision records
  knowledge/              # Current-state knowledge base (see "Knowledge base")
  journal.md              # AI working journal (see "Journal")
```
Dependencies: Api → Application → Domain. Infrastructure → Application. Domain references nothing.

## Architecture rules
- One vertical slice per feature under `Application/Claims/<Feature>/` (Command/Query, Handler, Validator, Response).
- Business rules live in the domain. Handlers are thin: validate, load, call the aggregate, save, map.
- Controller actions only map HTTP to MediatR requests. No logic in controllers. Each action declares `[Authorize(Policy = …)]` (or `[AllowAnonymous]`), a route `Name` (the OpenAPI operation id) and its success `ProducesResponseType`; controllers are `[Produces("application/json")]`.
- Modular monolith: one deployable. Do not split into services without asking.
- Do not add generic repositories, a separate read database, sagas, or an API gateway. Use `DbContext` directly behind a small port where needed.
- `CancellationToken` on every async method. No `.Result` / `.Wait()`.
- Nullable reference types on. Use `TimeProvider` instead of `DateTime.UtcNow` in domain and application code.

## Domain model (confirmed Prompt 1)
- Aggregate: `Claim` (owns information requests, document references, audit history; notes cut in Prompt 1). Policy is a validated `PolicyNumber` string on `Claim`, not an aggregate (no policy engine).
- `Claim.ClaimantUserId` is the authenticated user's id (JWT `sub`), set server-side on submit. Claimant contact details come from the user profile and the submit request, never used for authorization.
- Users are an Identity concern, not part of the `Claim` aggregate. The domain only holds user ids.
- Value objects: `Money` (decimal + ISO currency), `ClaimNumber`, `Market`.
- Types: Motor, Property. Markets: Singapore, Australia, Hong Kong, Malaysia, Thailand, Philippines (one `Market` field and currency per claim; no per-market rules).
- Money is `decimal` with currency. Never `double`/`float`. All timestamps UTC.

### Claim state machine (confirmed Prompt 1; full rules in `docs/knowledge/domain.md`)
```
Submitted → UnderReview            (officer assigns/picks up)
UnderReview → InfoRequested        (officer asks claimant)
InfoRequested → UnderReview        (claimant responds)
UnderReview → Approved | Rejected  (officer decides; Rejected needs a reason; Approve needs a reserve)
InfoRequested → Rejected           (no response after 15 days; reason fixed "No response received")
Approved → Settled                 (payment recorded)
Terminal: Rejected, Settled
```
Reserve changes (UnderReview only) and manager reassignment (with reason) are not status changes, but each has its own aggregate method and audit entry.
**Single path rule:** every status change goes through one aggregate method that checks the allowed-transition table. Never set `Status` anywhere else. There must be no generic "set status" endpoint, and approve/reject/settle each have their own explicit command. Settling requires Approved.

**Money rules (ADR 006):** currency derived from market, never from the client. Reserve set by staff (UnderReview only). Approve takes a manually entered `ApprovedAmount`, > 0 and ≤ Reserve. Settlement is a single payment, > 0 and ≤ `ApprovedAmount`. `OutstandingReserve` = Reserve after assess, ApprovedAmount after approve, 0 after reject/settle.

Every state change writes a status-history/audit row (who, when, from, to, reason). Actor identity comes from the authenticated principal, never from the request body.

## REST vs Kafka (confirmed Prompt 1, ADR 002–003)
- **REST** for all user-facing commands and queries (needs an immediate answer).
- **Kafka** for domain events other components react to. Events: `ClaimSubmitted`, `InformationRequested`, `ClaimDecided`, `ClaimSettled` (catalog in `docs/knowledge/events.md`). One topic `claims.events`, message key = `claimId` (preserves per-claim ordering), envelope: `eventId`, `type`, `version`, `occurredAt`, `claimId`, `payload`.
- At least one real consumer (notification handler). Consumers must be idempotent (track processed `eventId`), commit offsets after processing, and send poison messages to a dead-letter topic or log clearly.
- **Outbox:** write the event to an `OutboxMessage` table in the **same transaction** as the claim change (single `SaveChanges`). A background relay publishes to Kafka and marks rows processed. Request handlers never call Kafka directly.
- No PII beyond what consumers need in event payloads (no national ID, address, full contact details).

## Reports (ClaimManager only unless noted)
- **Workload:** per staff member: open claims by status, average age, oldest claim; plus the unassigned queue. ClaimStaff may read their own row only; ClaimManager reads all.
- **Liability exposure:** sum of outstanding reserve on **open** claims (not Rejected/Settled), grouped by market, claim type, and status, in native currency. If a USD total is shown, FX rates come from configuration and are labeled illustrative.
- Compute with SQL projections (`Select`, `GroupBy`). Never load full aggregates or audit trails to build a report.

## API conventions
- Routes `/api/v1/...`, plural nouns. Explicit action endpoints for transitions (e.g., `POST /claims/{id}/approve`).
- Every endpoint declares its authorization explicitly. See the endpoint role matrix under "Authentication and authorization".
- POST create requires `Idempotency-Key`. Return 201 + `Location`.
- State-changing calls require `If-Match` with the ETag (rowversion). 412 on mismatch, 428 if missing.
- Lists: pagination (`page`, `pageSize` max 100), filtering, sorting. No unbounded lists.
- Errors: RFC 7807 ProblemDetails. 400 validation (with field errors), 401, 403, 404, 409 (state conflict), 412, 422 (business rule), 500 (no internals leaked).
- Never return EF entities; use DTO records. No debug or event-inspection endpoints in the submitted build. If one exists for development, it is Development-only and ClaimManager-only.
- Generate and commit the OpenAPI spec; the Angular client is generated from it.

## Authentication and authorization (confirmed Prompt 1, ADR 004)

Real registration, login, and JWT validation implemented in an Identity module. Roles: **`Claimant`**, **`ClaimStaff`**, **`ClaimManager`** (define as constants in one place, e.g. `Roles.cs`; never use string literals in attributes).

### Services and endpoints
- `IIdentityService` (Application port) with Register, Login, GetCurrentUser. Implemented in Infrastructure using ASP.NET Core Identity (`UserManager`, `PasswordHasher`). Never write custom password hashing.
- `POST /api/v1/auth/register`: public. Creates a user with role **`Claimant` only**. The request has no role field, and any role supplied is ignored/rejected. Returns 201 with no token; the frontend redirects to login. Register and `POST /users` are exempt from `Idempotency-Key` and `Location` (unique email → 409 on retry; decided slice 2).
- `POST /api/v1/auth/login`: public. Returns a short-lived JWT access token (15 min). Failure always returns the same generic 401 (no user enumeration). Lockout after 5 failed attempts. Rate-limit auth endpoints with the built-in rate limiter.
- `GET /api/v1/auth/me`: any authenticated user. Returns id, display name, role.
- `POST /api/v1/users`: **ClaimManager only.** Creates `ClaimStaff` or `ClaimManager` accounts. This is the only way staff accounts are created at runtime.
- Dev seeding: on startup in Development only, seed one `ClaimManager`, one `ClaimStaff` and one `Claimant` from configuration (user-secrets), never hard-coded in source. Document the seeded accounts in the README without committing real passwords.
- Refresh tokens: **cut candidate**. Skip unless time remains; state the gap in the README.

### JWT
- HS256 signing key from configuration/user-secrets (`Jwt:Key`, 32+ bytes), plus `Jwt:Issuer` and `Jwt:Audience`. Never commit the key.
- Token claims: `sub` (user id), `email`, `role`, `jti`, `exp`. No PII beyond that.
- Validate on every request: issuer, audience, lifetime, signature, `ClockSkew` ≤ 1 minute. Reject `alg=none`. Use `JwtBearer` middleware; do not hand-parse tokens.
- Production note for the README: self-issued JWT is an assessment simplification; production would use an enterprise IdP (Azure AD / OIDC).

### Authorization rules
- Set a **fallback authorization policy requiring an authenticated user**. Only `register`, `login`, and `/health` use `[AllowAnonymous]`.
- Every endpoint also declares its role requirement explicitly (policy or `[Authorize(Roles = ...)]` using the constants).
- Identity for audit and ownership always comes from the validated token (`sub`, role), never from the request body or query string.
- Resource-level checks live in handlers/policies, not only in role attributes:
  - Claimant may access only claims where `ClaimantUserId == sub`. Someone else's claim returns **404**, not 403.
  - ClaimStaff assigning a claim assigns it to themselves. Only ClaimManager may assign or reassign to another person.

### Endpoint role matrix (confirmed Prompt 1)

| Endpoint | Anonymous | Claimant | ClaimStaff | ClaimManager |
|---|---|---|---|---|
| `POST /auth/register`, `POST /auth/login`, `GET /health` | yes | yes | yes | yes |
| `GET /auth/me` | no | yes | yes | yes |
| `POST /users` (create staff/manager), `GET /users` (paged) | no | no | no | yes |
| `POST /claims` (submit) | no | yes | no | no |
| `GET /claims/mine` | no | yes (own) | no | no |
| `GET /claims/{id}` | no | own only | yes | yes |
| `POST /claims/{id}/information` (respond) | no | own only | no | no |
| `POST /claims/{id}/documents` | no | own only | yes (assigned) | yes |
| `GET /claims` and `GET /claims/queue` | no | no | yes | yes |
| `POST /claims/{id}/assign` | no | no | self only | any staff |
| `POST /claims/{id}/reassign` (reason required) | no | no | no | yes |
| `POST /claims/{id}/request-info` | no | no | yes (assigned) | yes |
| `POST /claims/{id}/assess` (reserve) | no | no | yes (assigned) | yes |
| `POST /claims/{id}/approve`, `/reject`, `/reject-no-response`, `/settle` | no | no | yes (assigned) | yes |
| `GET /notifications/mine` | no | yes (own, latest 50) | no | no |
| `GET /reports/workload` | no | no | own row + unassigned queue | yes |
| `GET /reports/exposure` | no | no | no | yes |

Decided in the design review: ClaimStaff act only on claims assigned to them (unassigned staff → 403, since staff can read all claims); ClaimManager may act on any claim. No approval value threshold (README "with more time"). Swagger is Development-only and excluded from the matrix test by name.

### Password and account rules
- Minimum length 10, no composition theatre; reject common/breached passwords if cheap. Normalize and validate email.
- Never log passwords, tokens, or hashes. Never return them in any response.
- Registration duplicate email: acceptable to return 409 for this assessment (note the enumeration trade-off in the README), or a generic response if time allows.

## Data / EF Core
- SQL Server. Fluent API in `IEntityTypeConfiguration<T>`. Use migrations, not `EnsureCreated`.
- `rowversion` concurrency token on `Claim`. Child entity keys set client-side use `ValueGeneratedNever()`.
- Indexes for the real queries (status, assigned officer, market, created date). `AsNoTracking()` for reads.
- Claim numbers from a DB sequence or retry-on-conflict. Never `COUNT + 1`.
- Never edit an applied migration; add a new one. Parameterized SQL only.

## Security and PII
- No secrets in code or committed config (JWT key, seeded passwords, connection strings). Use user-secrets/env vars.
- Set a restrictive CORS policy to the Angular origin only. No `AllowAnyOrigin` with credentials.
- Never log PII (name, email, phone, ID numbers, address). Log claim IDs and numbers.
- Validate all input (FluentValidation): dates not in the future, amounts > 0, required fields, max lengths, enums valid.
- Do not commit `.db` files, `bin/`, `obj/`, `.vs/`, `node_modules/`, `.angular/`, or `dist/`. Keep the root `.gitignore` current.

## Testing
- Domain: every valid transition AND every invalid transition (all paths to a terminal state), money rules, invariants.
- Application: handler tests with mocked ports. One behavior per test.
- API integration: `WebApplicationFactory` + local SQL Server test DB, one database per run (ADR 007; no Testcontainers). Cover happy path, validation 400, 401/403, 404, 409, 412, idempotent retry, and claimant isolation.
- Auth tests: register creates only `Claimant` (a supplied role is ignored/rejected); login success/failure returns generic 401; lockout after repeated failures; expired, tampered, wrong-issuer/audience, and missing tokens are rejected (401).
- **Role matrix test:** a data-driven test (`[Theory]`) that calls every endpoint as anonymous, Claimant, ClaimStaff, and ClaimManager and asserts the expected 200/401/403/404 against the matrix above. Adding an endpoint without a matrix row must fail the build.
- Claimant isolation: user A cannot read, respond to, or upload to user B's claim (404).
- Outbox/consumer: test that an event row is written in the same transaction and that the consumer ignores duplicate `eventId`.
- Names: `Method_Scenario_ExpectedResult`. Tests assert behavior, not implementation. Never delete or weaken a failing test to go green; fix the cause or ask.

## Frontend (Angular)
- Standalone components, typed generated API client, route guards by role, lazy-loaded feature routes.
- Login and Register pages (register creates a claimant only). An HTTP interceptor attaches `Authorization: Bearer <token>`; a 401 clears the session and redirects to login; a 403 shows a clear message.
- Role guards read the role from the decoded token and are UX only. The API enforces authorization. No client-side role switcher.
- Token storage: keep the access token in memory with `sessionStorage` as the reload fallback, and note the XSS trade-off in the README. Never store passwords.
- Minimal styling. Build the claimant flow first (report, track, respond to info request), then the officer queue and actions, then the manager dashboard if time remains.
- No business rules in the frontend beyond showing/hiding actions; the API is the source of truth.
- API base URL from environment config, not hard-coded.

## Workflow for Claude
1. Read this file and the knowledge base first, then only the relevant code. State a short plan, restate the acceptance criteria that apply, and **wait for my OK** before writing code on non-trivial work.
2. One slice or concern at a time. Match existing patterns before inventing new ones. Implement the Identity module (register, login, JWT, fallback policy, role constants) **before** the claim slices so every later endpoint is born protected.
3. Don't add NuGet/npm packages without asking. Verify packages and APIs exist. Approved list in ADR 007 (MediatR 12.x, FluentAssertions 7.x pinned for licensing).
4. After changes: build, test, update the knowledge base, then summarize what changed, which acceptance criteria are now proven by which tests, assumptions, and risks.
5. If a requirement is ambiguous, ask. Don't guess about business or regulatory rules.
6. When a slice is done, list the edge cases and bypasses you may have missed (especially any other path to the same state change).
7. Keep scope tight. If something is a stretch for the time budget, propose cutting it rather than half-building it.

## Journal (for the assessment deliverable)
After each prompt, append to `docs/journal.md`: what was asked, what the AI proposed, what I accepted, what I challenged or overrode and why, and how it was verified. Be honest and specific. Do not rewrite history. Claude should remind me to log when a slice finishes, but must not invent entries.

## Do not
- Set `Status` outside the single transition method, or add a generic status-setting endpoint.
- Call Kafka from request handlers, or save events outside the claim's transaction.
- Trust actor name/role from the request body, or accept a role in the registration payload.
- Leave any endpoint without an explicit authorization rule, or rely on the frontend to enforce roles.
- Write your own password hashing or JWT parsing, or commit the JWT key or seeded passwords.
- Return different errors for "unknown user" vs "wrong password" on login.
- Use `DateTime.Now/UtcNow` directly in domain or application code.
- Load all claims into memory for lists or reports.
- Return 200 for failures or swallow exceptions.
- Commit secrets, real customer data, build output, or database files.
- Scan the whole codebase when the knowledge base answers the question, or leave the knowledge base stale after a change.
- Build anything listed under non-goals, or go beyond the acceptance criteria without asking.
- Copy code from outside this repo (including earlier practice builds) without telling me.
