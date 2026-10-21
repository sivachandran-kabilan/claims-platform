# ADR 007: Test database and approved packages

Status: Accepted (Prompt 1, 2026-10-21)

## Test database
- Integration tests use `WebApplicationFactory` against the **local SQL Server instance**, one database per test run created by migrations. No Testcontainers.
- Isolation by creating fresh users and claims per test, not by resetting tables (no Respawn).
- Time-dependent rules use a hand-written `FakeTimeProvider` (no `Microsoft.Extensions.TimeProvider.Testing` package).

## Approved packages
Backend: MediatR **12.x** (later versions are commercially licensed), FluentValidation + DependencyInjectionExtensions, EF Core SqlServer + Design (8.x), Identity.EntityFrameworkCore, Authentication.JwtBearer, Confluent.Kafka, Swashbuckle.AspNetCore.
Tests: xUnit, FluentAssertions **7.x** (8+ is commercially licensed), NSubstitute, Mvc.Testing, Microsoft.NET.Test.Sdk (approved slice 1). No coverlet.
Frontend: an npm OpenAPI client generator (e.g. `ng-openapi-gen`), confirmed when the frontend slice starts.

Exact versions are pinned once in `Directory.Packages.props` (central package management, transitive pinning on). SDK pinned to 8.0.426 in `global.json` because SDK 10 is also installed.

### Swashbuckle 9.0.6 (decided slice 1)
- 9.0.6 targets net8.0 and is the last line on `Microsoft.OpenApi` 1.x.
- 10.x moves to `Microsoft.OpenApi` 2.x (breaking object model), which would change our small filters (bearer scheme, `If-Match` / `Idempotency-Key` headers) for no gain.
- 6.9.0 was considered first and rejected: superseded line, no advantage.

Any other package needs explicit approval.
