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
dotnet user-secrets set "ConnectionStrings:Claims" "Server=localhost;Database=ClaimsPlatform;Integrated Security=true;TrustServerCertificate=true" --project src/Claims.Api

# 4. Database (once migrations exist; added in the Identity slice)
dotnet ef database update -p src/Claims.Infrastructure -s src/Claims.Api

# 5. API on http://localhost:5000 (Swagger UI at /swagger in Development)
dotnet run --project src/Claims.Api
```

## Tests
```bash
dotnet build -warnaserror
dotnet test                                                        # all tests
dotnet test tests/Claims.Domain.Tests tests/Claims.Application.Tests   # unit tests only (no DB)
```
Integration tests create a throwaway database `ClaimsPlatform_Tests_<guid>` on `localhost` and drop it at the end of the run.
Set `CLAIMS_TEST_SQLSERVER` to use a different server (for example `(localdb)\MSSQLLocalDB`).

## Known gaps / with more time
Kept current in `docs/knowledge/status.md` ("Known gaps and shortcuts"); summarised here at the end of the build.
