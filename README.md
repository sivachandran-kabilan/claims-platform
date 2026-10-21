# APAC Claims Platform

Claims platform for a multi-market insurer (motor and property, six APAC markets). Built as a 5-hour assessment.
Design decisions: `docs/adr/`. Current state: `docs/knowledge/status.md`. AI working journal: `docs/journal.md`.

## Prerequisites
| Tool | Version used |
|---|---|
| .NET SDK | 8.0.426 (pinned in `global.json`; newer SDKs may be installed side by side) |
| SQL Server | Local default instance (`localhost`), Windows authentication. Developed on SQL Server 2025 Developer |
| Docker Desktop | Engine 29.x, Compose v2+ (Linux containers) |
| Node.js / npm | 24.x / 11.x (frontend only) |

## Run locally
```bash
# 1. Kafka (+ topics claims.events / claims.events.dlt, Kafka UI on http://localhost:8080)
docker compose up -d

# 2. Tools and packages
dotnet tool restore
dotnet restore

# 3. Configuration (user-secrets; nothing secret is committed)
dotnet user-secrets set "ConnectionStrings:Claims" "Server=localhost;Database=ClaimsPlatformDev;Integrated Security=true;TrustServerCertificate=true" --project src/Claims.Api
dotnet user-secrets set "Jwt:Key" "<random string, at least 32 bytes>" --project src/Claims.Api
#    Dev accounts seeded at startup (Development only); repeat for Staff and Claimant:
dotnet user-secrets set "Seed:Manager:Email" "manager@test.com" --project src/Claims.Api
dotnet user-secrets set "Seed:Manager:Password" "<password, 10+ chars>" --project src/Claims.Api

# 4. Database (creates the schema and the three roles)
dotnet ef database update -p src/Claims.Infrastructure -s src/Claims.Api

# 5. API on http://localhost:5000 (Swagger UI at /swagger in Development; use "Authorize" with the login token)
dotnet run --project src/Claims.Api
```

### Dev accounts
In Development the API seeds one account per role from the `Seed:*` user-secrets, and never overwrites existing users.
The assessment test accounts are listed in `CLAUDE.md` (`manager@test.com`, `staff@test.com`, `user@test.com`; password = email).
They are for local development only. Self-registration always creates a Claimant; staff and managers are created by a manager via `POST /api/v1/users`.

The API refuses to start without a `Jwt:Key` of at least 32 bytes. If migrations are pending, seeding is skipped with a warning.

## Tests
```bash
dotnet build -warnaserror
dotnet test                                                        # all tests
dotnet test tests/Claims.Domain.Tests tests/Claims.Application.Tests   # unit tests only (no DB)
```
Integration tests create a throwaway database `ClaimsPlatform_Tests_<guid>` on `localhost` and drop it at the end of the run.
Set `CLAIMS_TEST_SQLSERVER` to use a different server (for example `(localdb)\MSSQLLocalDB`).

## Security notes
- Access tokens: self-issued HS256 JWT, 15 minutes, no refresh token. Production would use an enterprise IdP (Azure AD / OIDC).
- No token revocation: a role change takes effect when the current token expires.
- Duplicate registration returns 409, which reveals that an email is registered (accepted trade-off for the assessment).
- Login: identical 401 for every failure, lockout after 5 failures (15 min), 10 requests/min per IP on register and login.

## Known gaps / with more time
Kept current in `docs/knowledge/status.md` ("Known gaps and shortcuts"); summarised here at the end of the build.
