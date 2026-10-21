# ADR 001: Modular monolith, Clean Architecture, vertical slices

Status: Accepted (Prompt 1, 2026-10-21)

## Context
5-hour build, one developer, must run locally with a few commands. Brief needs claims workflow, identity, reports, and a Kafka integration point.

## Decision
- One deployable (`Claims.Api`) with Clean Architecture layers: Domain, Application, Infrastructure, Api.
- Two modules by folder and port: **Claims** and **Identity**. No separate projects per module.
- **One `DbContext`** (Identity tables included) so a claim change and its outbox row commit in one `SaveChanges`.
- Vertical slices under `Application/Claims/<Feature>/` with MediatR (12.x) and FluentValidation.

## Consequences
- Simple to run, test, and explain. Splitting later means extracting a module plus its tables.
- No module-level data isolation; enforced by convention only.
- Rejected: microservices, API gateway, separate read DB (non-goals; no time).
