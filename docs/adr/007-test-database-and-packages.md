# ADR 007: Test database and approved packages

Status: Accepted (Prompt 1, 2026-10-21)

## Test database
- Integration tests use `WebApplicationFactory` against the **local SQL Server instance**, one database per test run created by migrations. No Testcontainers.
- Isolation by creating fresh users and claims per test, not by resetting tables (no Respawn).
- Time-dependent rules use a hand-written `FakeTimeProvider` (no `Microsoft.Extensions.TimeProvider.Testing` package).

## Approved packages
Backend: MediatR **12.x** (later versions are commercially licensed), FluentValidation + DependencyInjectionExtensions, EF Core SqlServer + Design (8.x), Identity.EntityFrameworkCore, Authentication.JwtBearer, Confluent.Kafka, Swashbuckle.AspNetCore.
Tests: xUnit, FluentAssertions **7.x** (8+ is commercially licensed), NSubstitute, Mvc.Testing.
Frontend: an npm OpenAPI client generator (e.g. `ng-openapi-gen`), confirmed when the frontend slice starts.

Any other package needs explicit approval.
