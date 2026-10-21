# Journal

History of prompts and AI decisions for the assessment panel. Append only; do not rewrite earlier entries.

---

## PROMPT1: Design review (2026-10-21)

**Asked:** Act as a senior .NET architect. List ambiguities, propose entities, review the state machine for gaps and bypasses, decide REST vs Kafka per interaction, confirm or challenge the monolith, outbox, role matrix and JWT design, review the acceptance criteria, define workload and exposure as queries, propose a build order with a cut order, and name the risks. Ask clarifying questions first.

**Process:** The AI read the knowledge base and asked 13 clarifying questions, each with a suggested default. I answered. The AI wrote the full design in chat without touching files. After I approved it, the AI wrote the ADRs and KB updates.

### 1. What I directed the AI to do
- Produce a full design review of the claims platform before any code, following CLAUDE.md and the knowledge base.
- Ask clarifying questions first and wait for answers. Then write the design without editing files. Only after approval, write ADRs 001–007, update the KB, and start this journal.

### 2. What I accepted
- Modular monolith with Claims and Identity modules, one `DbContext` (so the outbox shares the claim's transaction), MediatR vertical slices.
- REST for every user interaction. Kafka for four events: `ClaimSubmitted`, `InformationRequested`, `ClaimDecided`, `ClaimSettled`. No events for assign, reassign, reserve or documents, because no consumer needs them.
- Transactional outbox with a polling relay. Idempotent in-process notification consumer writing in-app notifications, with a DLT.
- Single private `TransitionTo` with an allowed-transition table. 409 for an invalid transition, 422 for a broken business rule.
- Money rules: currency derived from market; reserve empty until staff assess; `OutstandingReserve` stored as one column; single settlement ≤ approved amount.
- One `ClaimHistoryEntry` audit table for both status and non-status actions.
- 15-day no-response rejection as a manual staff action checked by the domain, with no scheduler.
- Policy number as a string, not an aggregate (changes CLAUDE.md).
- Local SQL Server test DB instead of Testcontainers. Hand-written `FakeTimeProvider`.
- The AI's corrections to the acceptance criteria: AC-CLM-5 had a non-existent request id; concurrency moved to cross-cutting; added AC-CLM-10..13, AC-NOT-1, AC-USR-2, AC-EVT-4; AC-EVT-2/3 and frontend made partly manual.
- Role matrix additions: `GET /users` (manager), unassigned staff get 403 rather than 404, staff workload includes the unassigned queue.
- Build order (~300 min) and cut order.

### 3. What I challenged
- **Approved amount.** The AI proposed that approve should lock `ApprovedAmount = Reserve`. I challenged this: the approved amount is entered manually and must be ≤ the reserve. The AI then flagged that "less than" was ambiguous (strict or not), and I confirmed ≤.
- **Raised by the AI and accepted by me:**
  - The KB state machine was missing `InfoRequested → Rejected` (no response), "approve needs a reserve", assess and reassign.
  - MediatR 13+ and FluentAssertions 8+ are commercially licensed, so versions are pinned.
  - An Angular client generator is a new npm package that needs approval.

### 4. What I overrode / cut for time
- **Overrode:** the approved-amount default (see above). For the Angular client, chose an npm OpenAPI generator over hand-written typed services.
- **Packages:** pinned MediatR 12.x and FluentAssertions 7.x.
- **Cut for time:** refresh tokens, USD exposure total (FX), disabling staff accounts, a background job for the 15-day rejection, the manager dashboard UI, partial settlements, Testcontainers.
- **Cut order if behind:** staff UI → `GET /users` → documents → reassign endpoint → DLT → reject-no-response endpoint.

**Verification:** Design only, so there's no build or test yet. Each ADR and KB file was cross-checked against CLAUDE.md, and the CLAUDE.md conflicts were updated in the same change.

**Time:** _(fill in actual time spent)_
