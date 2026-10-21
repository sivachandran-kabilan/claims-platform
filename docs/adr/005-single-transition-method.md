# ADR 005: Single transition method on the Claim aggregate

Status: Accepted (Prompt 1, 2026-10-21)

## Context
Status must not be changeable by any path that skips the rules or the audit trail.

## Decision
- `Status` has a private setter. Claims are created only via `Claim.Submit(...)`.
- Each action is an explicit aggregate method (`Assign`, `RequestInformation`, `RespondToInformation`, `Approve`, `Reject`, `RejectForNoResponse`, `Settle`), and each has its own command and endpoint. No generic "set status".
- All of them call one private `TransitionTo(to, actor, reason)` that checks a static allowed-transition table, writes a `ClaimHistoryEntry`, and raises the domain event where applicable.
- Non-status actions (`SetReserve`, `Reassign`, `AddDocument`) have their own methods and audit entries.
- Not in table → `InvalidTransitionException` → 409. Valid state, rule broken (amount, 15 days, no reserve) → `BusinessRuleException` → 422.
- No claim seeding with non-`Submitted` status.

## Consequences
- EF can still write the backing field if someone bypasses the aggregate; guarded by convention and review, not by a technical lock.
- Every invalid transition is tested by one generated `[Theory]` (statuses × actions).
