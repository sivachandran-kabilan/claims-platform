# ADR 005: Single transition method on the Claim aggregate

Status: Accepted (Prompt 1, 2026-10-21)

## Context
Status must not be changeable by any path that skips the rules or the audit trail.

## Decision
- `Status` has a private setter. Claims are created only via `Claim.Submit(...)`.
- Each action is an explicit aggregate method (`Assign`, `RequestInformation`, `RespondToInformation`, `Approve`, `Reject`, `RejectForNoResponse`, `Settle`), and each has its own command and endpoint. No generic "set status".
- All of them call one private `TransitionTo(action, actor, ...)` that looks up `(action, current status)` in `ClaimTransitions`, writes a `ClaimHistoryEntry`, and the method raises the domain event where applicable.
- **Action-keyed, not edge-keyed (amended slice 3).** The table maps `(action, from) → to`. A `(from, to)` table is bypassable because actions share edges: `Assign` on an InfoRequested claim would ride `RespondToInformation`'s edge to UnderReview (skipping the claimant), and `Reject` on an InfoRequested claim would ride `RejectForNoResponse`'s edge (free-text reject before 15 days).
- Non-status actions (`SetReserve`, `Reassign`, `AddDocument`) have their own methods, audit entries, and an allowed-status set in the same file.
- Status is checked first: not allowed → `InvalidTransitionException` → 409. Valid state, rule broken (amount, 15 days, no reserve) → `BusinessRuleException` → 422.
- The domain does not decide *who* may act; the application layer does (ADR 004).
- No claim seeding with non-`Submitted` status.

## Consequences
- EF can still write the backing field if someone bypasses the aggregate; guarded by convention and review, not by a technical lock.
- Tests check every (action × status) pair against an expected table written independently of the production table (`tests/Claims.Domain.Tests/Claims/TransitionMatrixTests.cs`). Re-adding either bypass fails three tests (verified by mutation in slice 3).
