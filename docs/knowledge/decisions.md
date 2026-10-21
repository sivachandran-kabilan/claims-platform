# Decisions (summary; full text in docs/adr/)

| ADR | Decision | Why (one line) |
|---|---|---|
| 001 | Modular monolith, Clean Architecture, vertical slices, one DbContext | Fits 5h budget; outbox needs one transaction; splits later |
| 002 | REST for user interactions; Kafka for 4 domain events | Users need immediate answers; events are facts others react to |
| 003 | Transactional outbox + idempotent consumer with DLT | No lost or phantom events; at-least-once is handled |
| 004 | ASP.NET Core Identity + self-issued HS256 JWT (15 min), fallback auth policy | Real auth without an external IdP (non-goal) |
| 005 | Single private `TransitionTo`; explicit method/command per action | No path can change status without rules and audit |
| 006 | Currency from market; reserve by staff; approved ≤ reserve (manual); single settlement ≤ approved | Simple, explainable money rules; one exposure column |
| 007 | Local SQL Server test DB; MediatR 12.x, FA 7.x; npm OpenAPI generator | No Testcontainers; avoid commercial licences |
