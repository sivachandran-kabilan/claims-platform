# Domain

> Source of truth: `src/Claims.Domain/Claims/`. EF mapping: `src/Claims.Infrastructure/Persistence/Configurations/ClaimConfigurations.cs`.

## Entities
- **Claim** (`Claim.cs`, aggregate root): Id, Number (`ClaimNumber`, `CLM-YYYY-NNNNNN`, sequence from DB in slice 4), ClaimantUserId (JWT `sub`), Type (Motor|Property), Market, Currency (from market), PolicyNumber (string), IncidentDate (`DateOnly`), Description, ContactPhone, ClaimedAmount, Reserve?, ApprovedAmount?, OutstandingReserve, SettledAmount?, SettledAt?, RejectionReason?, Status (private set), AssignedToUserId?, SubmittedAt, DecidedAt?. RowVersion is an EF shadow property (ETag / concurrency), not on the class. Number comes from the `ClaimNumbers` DB sequence (global, gaps possible).
  - Amounts are plain `decimal` (2 dp) in the claim's single `Currency`; `Money` (`ValueObjects.cs`) is used when an amount leaves the claim (events, DTOs, reports). ADR 006.
  - **InformationRequest** (`ClaimChildren.cs`): Id, Question, RequestedByUserId, RequestedAt, Response?, RespondedByUserId?, RespondedAt?. `IsOpen` = not responded. At most one open.
  - **DocumentReference**: Id, FileName, ContentType, SizeBytes (≤ 20 MB), StorageRef, UploadedByUserId, UploadedAt, InformationRequestId? (set when the claimant uploads while a request is open). Metadata only.
  - **ClaimHistoryEntry**: Id, Action (`ClaimAction`), FromStatus?, ToStatus?, ActorUserId, ActorRole, OccurredAt, Reason?, Amount?, TargetUserId? (assignee for Assign/Reassign), Sequence (1..n per claim, orders same-timestamp rows). One row per action.
- **Actor** (`UserId`, `Role`): passed into every method; built from the token by the application layer.
- **Users** live in Identity (`ApplicationUser`: Id, Email, DisplayName, one role). Domain holds user ids only.
- Persistence records (`Application/Common/Persistence`): OutboxMessage, IdempotencyRecord. Later: ProcessedEvent, Notification.
- Tables: Claims, ClaimInformationRequests, ClaimDocuments, ClaimHistory, OutboxMessages, IdempotencyRecords (enums stored as strings, amounts decimal(18,2)).

## State machine (`ClaimTransitions.cs`, action-keyed; ADR 005)
Table is keyed by **(action, current status) → new status**. Several actions share an edge, so a (from, to) table would let one action use another's path.

| Action | From | To | Rules (422 if broken) | Event |
|---|---|---|---|---|
| Assign | Submitted | UnderReview | assignee id required | none |
| RequestInformation | UnderReview | InfoRequested | question required; no open request | InformationRequested |
| RespondToInformation | InfoRequested | UnderReview | response required; closes open request | none |
| Approve | UnderReview | Approved | reserve set; 0 < amount ≤ Reserve; Outstanding := amount; DecidedAt | ClaimDecided(Approved) |
| Reject | UnderReview | Rejected | reason required; Outstanding := 0; DecidedAt | ClaimDecided(Rejected) |
| RejectForNoResponse | InfoRequested | Rejected | now ≥ open request + 15 days (exactly 15 allowed); reason "No response received"; Outstanding := 0 | ClaimDecided(RejectedNoResponse) |
| Settle | Approved | Settled | 0 < amount ≤ ApprovedAmount; SettledAt; Outstanding := 0 | ClaimSettled |

Actions that don't change status (allowed statuses): **SetReserve** UnderReview (> 0, repeatable, Outstanding := Reserve); **Reassign** UnderReview/InfoRequested/Approved (reason required; same assignee → 422); **AddDocument** any non-terminal.
Terminal: Rejected, Settled. Wrong status → `InvalidTransitionException` (409), checked before any business rule.

## Invariants
- All amounts > 0 where entered, at most 2 decimals, in the claim currency; currency from market, never from the client. Timestamps UTC (`DateTimeOffset now` passed in from `TimeProvider`).
- `Submit` only by a Claimant actor; incident date ≤ UTC today + 1 day (markets are ahead of UTC; no per-market time zones).
- ClaimedAmount is informational; only Reserve / ApprovedAmount drive exposure.
- Who may act (owner, assigned staff, manager) is enforced in the application layer: non-owning claimant → 404; unassigned ClaimStaff → 403.

## Glossary
- **Reserve**: insurer's estimate of what it will pay, set by staff.
- **Outstanding reserve**: amount still expected to be paid on an open claim.
- **Exposure**: sum of outstanding reserve on open claims (not Rejected/Settled).
- **Settlement**: the single recorded payment.
- **Open (workload)**: UnderReview, InfoRequested, Approved. **Queue**: Submitted (unassigned).
