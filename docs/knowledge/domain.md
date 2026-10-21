# Domain

> Design confirmed in Prompt 1; **no code yet**. Once `Claims.Domain` exists, the code is the source of truth.

## Entities
- **Claim** (aggregate root): Id, ClaimNumber (DB sequence, `CLM-YYYY-NNNNNN`), ClaimantUserId (JWT `sub`), Type (Motor|Property), Market + Currency (derived), PolicyNumber (string, no aggregate), IncidentDate, Description, ContactPhone, ClaimedAmount, Reserve?, ApprovedAmount?, OutstandingReserve, SettledAmount?, SettledAt?, RejectionReason?, Status (private set), AssignedToUserId?, SubmittedAt, DecidedAt?, RowVersion.
  - **InformationRequest**: Id, Question, RequestedBy, RequestedAt, Response?, RespondedAt?. At most one open.
  - **DocumentReference**: Id, FileName, ContentType, SizeBytes, StorageRef, UploadedBy, UploadedAt, InformationRequestId?. Metadata only.
  - **ClaimHistoryEntry**: Id, Action, FromStatus?, ToStatus?, ActorUserId, ActorRole, OccurredAt, Reason?, Amount?. Single audit trail (status and non-status actions).
- **Users** live in Identity (`ApplicationUser`: Id, Email, DisplayName, one role). Domain holds user ids only.
- Infrastructure: OutboxMessage, ProcessedEvent, Notification, IdempotencyRecord.

## Claim state machine (single private `TransitionTo`; ADR 005)
| From | To | Method | Who | Rules |
|---|---|---|---|---|
| Submitted | UnderReview | Assign | Staff (self) / Manager (any staff or manager) | |
| UnderReview | InfoRequested | RequestInformation | Assigned staff / Manager | Question required; no open request |
| InfoRequested | UnderReview | RespondToInformation | Owning claimant | Closes open request |
| UnderReview | Approved | Approve | Assigned staff / Manager | Reserve set; 0 < ApprovedAmount ≤ Reserve; Outstanding := ApprovedAmount |
| UnderReview | Rejected | Reject | Assigned staff / Manager | Reason required; Outstanding := 0 |
| InfoRequested | Rejected | RejectForNoResponse | Assigned staff / Manager | Open request ≥ 15 days old (TimeProvider); reason "No response received"; Outstanding := 0 |
| Approved | Settled | Settle | Assigned staff / Manager | 0 < amount ≤ ApprovedAmount; SettledAt; Outstanding := 0 |

Terminal: Rejected, Settled. Not in table → 409. Rule broken in valid state → 422.

Non-status actions (own method + audit entry):
- **SetReserve**: UnderReview only; > 0; claim currency; Outstanding := Reserve.
- **Reassign**: Manager only; reason required; UnderReview / InfoRequested / Approved; target is ClaimStaff or ClaimManager.
- **AddDocument**: any non-terminal status.

## Invariants
- Money is decimal + currency; currency derived from market, never from client. Timestamps UTC.
- ClaimedAmount is informational; only Reserve / ApprovedAmount drive exposure.
- Actor comes from the token, never the body.
- Access: non-owning claimant → 404; unassigned ClaimStaff → 403.

## Glossary
- **Reserve**: insurer's estimate of what it will pay, set by staff.
- **Outstanding reserve**: amount still expected to be paid on an open claim.
- **Exposure**: sum of outstanding reserve on open claims (not Rejected/Settled).
- **Settlement**: the single recorded payment.
- **Open (workload)**: UnderReview, InfoRequested, Approved. **Queue**: Submitted (unassigned).
