# API

Base: `/api/v1`. Errors: RFC 7807 ProblemDetails (`application/problem+json`). State-changing calls need `If-Match`. POST create needs `Idempotency-Key` (claims only; see exceptions below).

Error mapping (`src/Claims.Api/Errors/GlobalExceptionHandler.cs`): `ValidationException` → 400 with field errors; `AuthenticationFailedException` → 401 "Invalid credentials." (no detail); `NotFoundException` → 404; `ConflictException` / `InvalidTransitionException` → 409; `BusinessRuleException` → 422; anything else → generic 500 (type + stack trace logged, never the message). Bare 401/403/404/405/429 become ProblemDetails via `UseStatusCodePages`.

Auth: JWT bearer (`src/Claims.Api/Auth/AuthSetup.cs`). Fallback policy requires an authenticated user, **including unmatched URLs** (anonymous → 401, authenticated → 404). Policies (`Auth/Policies.cs`): `AnyUser`, `Claimant`, `Staff` (ClaimStaff or ClaimManager), `Manager`. Role matrix test: `tests/Claims.Api.IntegrationTests/RoleMatrixTests.cs`.

## Implemented
| Method | Route | Policy | Request | Success | Errors |
|---|---|---|---|---|---|
| GET | /health | anonymous | n/a | 200 `Healthy` (liveness only) | n/a |
| POST | /auth/register | anonymous, rate-limited | `{email, password, displayName}` (any `role` ignored) | 201 `UserResponse {id, email, displayName, role=Claimant}`, no token | 400, 409, 429 |
| POST | /auth/login | anonymous, rate-limited | `{email, password}` | 200 `{accessToken, expiresAtUtc, tokenType="Bearer"}` | 400, 401 (identical for every failure), 429 |
| GET | /auth/me | AnyUser | n/a | 200 `UserResponse` | 401 |
| POST | /users | Manager | `{email, password, displayName, role}` role ∈ ClaimStaff/ClaimManager | 201 `UserResponse` | 400, 401, 403, 409 |
| GET | /users | Manager | `?page=1&pageSize=20(≤100)&role=` sorted by email | 200 `PagedResult<UserResponse> {items, page, pageSize, totalCount}` | 400, 401, 403 |
| POST | /claims | Claimant | header `Idempotency-Key` (≤100); body `{type, market, policyNumber, incidentDate (yyyy-MM-dd), description, contactPhone, claimedAmount}` (no currency; any extra field ignored) | 201 `ClaimDetailResponse` + `Location` + `ETag`; same key + same body → same claim (201) | 400, 401, 403, 422 (key reused with different body) |
| GET | /claims/{id} | AnyUser (claimant: own only) | n/a | 200 `ClaimDetailResponse` + `ETag` | 401, 404 (missing **or another claimant's**) |
| GET | /claims/mine | Claimant | `?page&pageSize&status` newest first | 200 `PagedResult<ClaimSummaryResponse>` | 400, 401, 403 |
| GET | /claims | Staff | `?page&pageSize&status&market&type&assignedTo&unassigned&sort=submittedAt\|-submittedAt` (default newest first) | 200 `PagedResult<ClaimSummaryResponse>` | 400, 401, 403 |
| GET | /claims/queue | Staff | `?page&pageSize`; Submitted + unassigned, oldest first | 200 `PagedResult<ClaimSummaryResponse>` | 400, 401, 403 |
| POST | /claims/{id}/assign | Staff (staff: self only; manager: anyone) | `{assigneeUserId?}` (omitted = me) | 200 detail + ETag | 400, 403, 404, 409, 412, 422 (assignee not staff/manager), 428 |
| POST | /claims/{id}/reassign | Manager | `{assigneeUserId, reason}` | 200 | 400, 403, 404, 409, 412, 422 (invalid/same assignee), 428 |
| POST | /claims/{id}/request-info | Staff (assigned) / Manager | `{question}` | 200 | 400, 403, 404, 409, 412, 428 |
| POST | /claims/{id}/information | Claimant (own) | `{response}` | 200 | 400, 403, 404, 409, 412, 428 |
| POST | /claims/{id}/assess | Staff (assigned) / Manager | `{reserve}` | 200 | 400, 403, 404, 409, 412, 428 |
| POST | /claims/{id}/approve | Staff (assigned) / Manager | `{approvedAmount}` (≤ reserve) | 200 | 400, 403, 404, 409, 412, 422, 428 |
| POST | /claims/{id}/reject | Staff (assigned) / Manager | `{reason}` | 200 | 400, 403, 404, 409, 412, 428 |
| POST | /claims/{id}/reject-no-response | Staff (assigned) / Manager | none | 200 | 403, 404, 409, 412, 422 (< 15 days), 428 |
| POST | /claims/{id}/settle | Staff (assigned) / Manager | `{amount}` (≤ approved) | 200 | 400, 403, 404, 409, 412, 422, 428 |
| POST | /claims/{id}/documents | Owner / assigned staff / manager | `{fileName, contentType (pdf/jpeg/png), sizeBytes (≤20 MB), storageRef}` | 201 detail + `Location` (claim) + ETag | 400, 403, 404, 409, 412, 428 |

Claims (`src/Claims.Api/Controllers/ClaimsController.cs`, read model `src/Claims.Application/Claims/Common/ClaimReadModel.cs`):
- Enums are strings in JSON (`"Motor"`, `"Singapore"`, `"UnderReview"`).
- `ETag` = `"<base64 rowversion>"` (strong). Every action needs `If-Match` with it: missing → 428; stale, weak `W/`, `*` or malformed → 412. Every action changes the ETag (`Claim.UpdatedAt` is stamped on every action).
- **Action check order** (`src/Claims.Application/Claims/Workflow/ClaimWorkflow.cs`): 400 body → 428 → 404 (not visible) → 403 (act rule, `ClaimAccess`) → 412 → 409/422 (domain) → save (EF rowversion check, race → 412). Actions return 200 with the updated detail (viewer's view) + new ETag.
- **Claimant view** (approved slice 4): `reserve`, `outstandingReserve`, `assignedToUserId`, actor/target/uploader ids are null; `SetReserve` and `Reassign` history entries are hidden. Information requests on a closed claim show state `Unanswered`.

Exceptions to conventions: register and POST /users take no `Idempotency-Key` and return 201 with a body but no `Location` (slice 2; unique email makes retries safe → 409). POST /claims/{id}/documents takes `If-Match` instead of `Idempotency-Key` (slice 5; a blind retry gets 412, not a duplicate) and its `Location` is the claim.
Rate limit: policy `auth`, fixed window 10/min per client IP (`RateLimiting:AuthPermitLimit`).
