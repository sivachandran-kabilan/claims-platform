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

Exceptions to conventions (approved slice 2): register and POST /users take no `Idempotency-Key` and return 201 with a body but no `Location` (unique email makes retries safe → 409; there is no GET /users/{id}).
Rate limit: policy `auth`, fixed window 10/min per client IP (`RateLimiting:AuthPermitLimit`).
