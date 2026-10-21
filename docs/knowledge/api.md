# API

Base: `/api/v1`. Errors: RFC 7807 ProblemDetails (`application/problem+json`). State-changing calls need `If-Match`. POST create needs `Idempotency-Key`.

Error mapping (`src/Claims.Api/Errors/GlobalExceptionHandler.cs`): FluentValidation `ValidationException` → 400 with field errors; `InvalidTransitionException` → 409; `BusinessRuleException` → 422; anything else → generic 500 (exception type logged only). Bare status codes (404/405) become ProblemDetails via `UseStatusCodePages`.

## Implemented
| Method | Route | Roles | Request | Success | Errors |
|---|---|---|---|---|---|
| GET | /health | anonymous | n/a | 200 `Healthy` (liveness only; no DB check) | n/a |

## Planned (from the role matrix in CLAUDE.md; move rows up as they are built)
| Method | Route | Roles | Request | Success | Errors |
|---|---|---|---|---|---|
| POST | /auth/register | anonymous | email, password, name | 201 | 400, 409 |
| POST | /auth/login | anonymous | email, password | 200 token | 401, 429 |
| GET | /auth/me | any | n/a | 200 | 401 |
