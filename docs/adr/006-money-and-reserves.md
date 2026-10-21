# ADR 006: Money, reserves, approval and settlement

Status: Accepted (Prompt 1, 2026-10-21)

## Decision
- `Money` = `decimal` + ISO currency. Currency is derived from `Market` (SG→SGD, AU→AUD, HK→HKD, MY→MYR, TH→THB, PH→PHP) and never accepted from the client.
- `ClaimedAmount`: claimant's estimate at submit; informational only, never a reserve.
- `Reserve`: set by staff via assess, **UnderReview only**, > 0. Empty until assessed.
- `ApprovedAmount`: entered manually on approve; must be > 0 and **≤ Reserve** (user decision, Prompt 1). Approve without a reserve → 422.
- `Settle`: single payment, > 0 and ≤ `ApprovedAmount`. No partial settlements.
- `OutstandingReserve` (stored column): Assess sets = Reserve; Approve sets = ApprovedAmount (difference released); Reject / RejectForNoResponse / Settle set = 0.
- Exposure sums `OutstandingReserve` per currency; never adds across currencies. USD total cut.
- **Storage (decided slice 3):** one `Currency` column per claim and plain `decimal` amounts (2 dp). Every amount on a claim shares that currency, so mixing currencies within a claim is impossible by construction. `Money` is used where an amount leaves its claim (events, DTOs, reports). Rejected: `Money` objects on the aggregate, because EF Core 8 cannot map optional complex types cleanly (Reserve/ApprovedAmount/SettledAmount are optional).
- Amounts with more than 2 decimal places → 422 (all six currencies use 2 minor units).

## Consequences
- Exposure query is a single `GROUP BY` on one column.
- Unreserved open claims contribute 0; the report shows their count separately.
