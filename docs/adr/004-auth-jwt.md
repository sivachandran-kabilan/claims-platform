# ADR 004: ASP.NET Core Identity + self-issued JWT

Status: Accepted (Prompt 1, 2026-10-21)

## Context
Brief requires registration and login. External IdP is a non-goal.

## Decision
- ASP.NET Core Identity on the shared `DbContext` for users, password hashing, and lockout (5 attempts).
- Roles `Claimant`, `ClaimStaff`, `ClaimManager` as constants in one `Roles` class. Register always creates `Claimant`; staff/manager accounts only via `POST /users` (ClaimManager).
- JWT HS256, 15-minute lifetime, claims `sub, email, role, jti, exp`. Key/issuer/audience from user-secrets.
- Validation via `JwtBearer`: issuer, audience, lifetime, signature, `ValidAlgorithms = [HS256]`, `ClockSkew = 1 min`, `MapInboundClaims = false`, `RoleClaimType = "role"`, `NameClaimType = "sub"`.
- Fallback policy requires an authenticated user; every endpoint also declares its role policy. Auth endpoints rate-limited (fixed window, per IP).
- Resource checks in one application helper: non-owning claimant → 404; ClaimStaff not assigned → 403 (staff can read all claims, so existence is not secret).

## Implementation details (slice 2)
- `IdentityUser<Guid>` keys (match domain user ids). One role per user; user + role inserted in one transaction.
- Roles seeded by migration (`HasData`, fixed ids) in every environment. Users seeded only in Development by `DevUserSeeder` from `Seed:Manager|Staff|Claimant` user-secrets; skipped with a warning if migrations are pending. Migrations are applied manually (`dotnet ef database update`).
- Lockout: 5 failures → 15 minutes, via `SignInManager.CheckPasswordSignInAsync(lockoutOnFailure: true)`. Unknown email runs a dummy hash check to equalise timing.
- Passwords: length ≥ 10, no composition rules, small common-password list (`CommonPasswordValidator`).
- Rate limit: 10 requests/min per client IP on register and login (configurable), 429 ProblemDetails.
- Token issuing (`JwtTokenGenerator`) lives in the API next to bearer validation; `JwtOptions` validated at startup (key ≥ 32 bytes, lifetime ≤ 15 min).
- The fallback policy also covers unmatched URLs: anonymous callers get 401 rather than 404, so routes can't be probed.
- The framework's `ExceptionHandlerMiddleware` log is disabled; our handler logs exception type + stack trace only (messages can carry data such as emails).

## Consequences (README gaps)
- No refresh token: session ends after 15 min.
- No revocation: role change or account removal takes effect at token expiry.
- Duplicate registration returns 409 (email enumeration accepted for the assessment). Login timing is equalised for unknown emails but not formally measured.
- Rate limiting is per IP: clients behind one NAT share a budget; no distributed limiter.
- Dev test accounts use password = email (from CLAUDE.md); acceptable only because they are Development-only and configured via user-secrets.
- Production would use an enterprise IdP (Azure AD / OIDC).
