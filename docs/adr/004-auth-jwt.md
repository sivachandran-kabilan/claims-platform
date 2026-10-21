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

## Consequences (README gaps)
- No refresh token: session ends after 15 min.
- No revocation: role change or account removal takes effect at token expiry.
- Duplicate registration returns 409 (email enumeration accepted for the assessment). Login timing may differ for unknown users.
- Production would use an enterprise IdP (Azure AD / OIDC).
