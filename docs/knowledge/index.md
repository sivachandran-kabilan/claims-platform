# Knowledge Base Index

Current-state documentation of this project, written for Claude (and humans). **Read this before scanning code.** It describes what exists *now*; the journal (`docs/journal.md`) records history and AI decisions for the assessment panel. They are different documents.

## Files
| File | What it holds | Read when |
|---|---|---|
| `status.md` | What's done, in progress, next, known gaps | Start of every task |
| `domain.md` | Entities, state machine, invariants, glossary | Touching domain rules |
| `api.md` | Every endpoint: role, request, response, errors | Touching API or frontend contract |
| `events.md` | Event catalog: trigger, key, payload, consumers | Touching Kafka/outbox |
| `acceptance-criteria.md` | Testable criteria per feature | Starting or finishing a slice |
| `decisions.md` | One-line summary of each ADR | Questioning an architectural choice |

## Project map
- Root: `global.json` (SDK 8.0.426), `Directory.Build.props` (net8.0, nullable, warnings as errors), `Directory.Packages.props` (all package versions), `.config/dotnet-tools.json` (dotnet-ef 8.0.32), `docker-compose.yml` (Kafka, topic init, Kafka UI).
- `src/Claims.Domain`: `Common/` (exceptions → 409/422, `Roles`, `AggregateRoot` + `IDomainEvent`); `Claims/` (`Claim` aggregate, `ClaimTransitions` action-keyed table, enums, `Market` → currency, `Money`/`ClaimNumber`/`Actor`, child entities, `Events/`).
- `src/Claims.Application`: `AddApplication()` (MediatR + validators), `Common/Behaviors/ValidationBehavior` (→ 400), `Common/Exceptions` (401/404/409), `Common/Paging`, `Common/Security/ICurrentUser`, `Common/Persistence` (`IClaimsDbContext` port, OutboxMessage, IdempotencyRecord, Notification, ProcessedEvent), `Common/Messaging` (EventEnvelope, EventJson, IEventPublisher), `Notifications/` (ClaimEventNotificationHandler, GetMyNotifications), `Reports/` (models, `IReportQueries` port, workload/exposure handlers), `Identity/` slices, `Claims/` (`Common`: ClaimAccess act rules, ClaimReadModel, ClaimRules, responses; `Workflow`: ClaimWorkflow runner + ETags; one folder per action: Submit, GetClaim, ListClaims, Assign, Reassign, RequestInformation, RespondToInformation, Assess, Approve, Reject, RejectForNoResponse, Settle, AddDocument).
- `src/Claims.Infrastructure`: `Persistence/ClaimsDbContext` (IdentityDbContext + IClaimsDbContext; SaveChangesAsync writes the outbox), `OutboxWriter`, `ClaimNumberGenerator`, `Reports/ReportQueries` (GROUP BY SQL, DATEDIFF), `Messaging/` (KafkaOptions, KafkaEventPublisher, KafkaDeadLetterPublisher, OutboxProcessor, MessageDispatcher, OutboxRelay + NotificationConsumer background services), `Persistence/Configurations`, `Persistence/Migrations`, `Identity/` (ApplicationUser, IdentityService, CommonPasswordValidator, DevUserSeeder), `AddInfrastructure()`.
- `src/Claims.Api`: `Program.cs`, `Auth/` (AuthSetup: JWT bearer, policies, rate limiter; JwtTokenGenerator; CurrentUser), `Controllers/` (Auth, Users, Claims, ClaimActions, Notifications, Reports), `Errors/GlobalExceptionHandler`. http://localhost:5000.
- `tests/Claims.Api.IntegrationTests/Infrastructure`: `ClaimsApiFactory` (environment `Testing`, throwaway migrated DB, test JWT key, user/token helpers), `TestTokens` (bad tokens), `ClaimRequests` (submit helpers), `ClaimActions` (act/ETag helpers, `ArrangeClaimAsync(status)`). `RoleMatrixTests.cs` holds the matrix rows; `Claims/` holds claim API + persistence tests; `Messaging/` holds relay, consumer, dispatcher and notification tests (fake publisher, no broker); `Reports/` holds report tests (seeded backdated claims, before/after deltas).
- `web/` (Angular 22): `src/app/api/` generated client (ng-openapi-gen from `docs/openapi.json`; don't edit), `core/` (AuthService: token signal + sessionStorage, interceptor 401/403, roleGuard, NoticeService/ProblemDetails), `features/` (auth, claimant, claims/claim-detail shared by all roles, staff, manager), `app.routes.ts` (lazy routes + guards), `environments/` (apiBaseUrl).

## How to run
See `README.md`: `docker compose up -d`, `dotnet tool restore`, user-secrets (`ConnectionStrings:Claims` → `ClaimsPlatformDev`, `Jwt:Key`, `Seed:*`), `dotnet ef database update`, `dotnet run --project src/Claims.Api`; `cd web && npm ci && npx ng serve`.

## Rules for keeping this accurate
1. Update the affected files in the same commit as the code change.
2. If this KB and the code disagree, the code wins: fix the KB and tell the developer.
3. Keep entries short and factual. Link to code paths instead of copying code.
