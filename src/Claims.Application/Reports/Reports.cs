using Claims.Application.Common.Security;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using MediatR;

namespace Claims.Application.Reports;

// ---- Response models (ages in days, 1 decimal, measured from submission) ----------------------------

public sealed record WorkloadReport(DateTimeOffset GeneratedAt, IReadOnlyList<StaffWorkload> Staff, QueueSummary Unassigned);

/// <summary>One assignee's open claims (UnderReview, InfoRequested, Approved).</summary>
public sealed record StaffWorkload(
    Guid UserId, string DisplayName, string Role, int OpenClaims, int UnderReview, int InfoRequested, int Approved,
    double? AverageAgeDays, string? OldestClaimNumber, double? OldestAgeDays);

/// <summary>Submitted claims waiting to be picked up (always unassigned).</summary>
public sealed record QueueSummary(int Count, double? AverageAgeDays, string? OldestClaimNumber, double? OldestAgeDays);

public sealed record ExposureReport(DateTimeOffset GeneratedAt, IReadOnlyList<ExposureRow> Rows, IReadOnlyList<CurrencyTotal> Totals);

/// <summary>Outstanding reserve on open claims (not Rejected/Settled) in one group, in its native currency.</summary>
public sealed record ExposureRow(
    Market Market, ClaimType Type, ClaimStatus Status, string Currency,
    decimal OutstandingReserve, int ClaimCount, int UnreservedCount);

/// <summary>Per-currency total. Never summed across currencies (no FX; USD total cut).</summary>
public sealed record CurrencyTotal(string Currency, decimal OutstandingReserve, int ClaimCount);

/// <summary>
/// Report SQL lives in Infrastructure: workload ages need SQL Server's DATEDIFF (EF.Functions.DateDiffHour),
/// and Application stays provider-neutral. Implementations must use SQL projections only.
/// </summary>
public interface IReportQueries
{
    /// <param name="onlyUserId">Restrict staff rows to one assignee (ClaimStaff see their own row only).</param>
    Task<WorkloadReport> GetWorkloadAsync(Guid? onlyUserId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ExposureReport> GetExposureAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

// ---- GET /reports/workload ---------------------------------------------------------------------------

public sealed record GetWorkloadQuery : IRequest<WorkloadReport>;

public sealed class GetWorkloadQueryHandler(IReportQueries reports, ICurrentUser currentUser, TimeProvider clock)
    : IRequestHandler<GetWorkloadQuery, WorkloadReport>
{
    // Managers see every staff/manager row; ClaimStaff only their own (plus the queue).
    public Task<WorkloadReport> Handle(GetWorkloadQuery request, CancellationToken cancellationToken) =>
        reports.GetWorkloadAsync(
            currentUser.Role == Roles.ClaimManager ? null : currentUser.UserId, clock.GetUtcNow(), cancellationToken);
}

// ---- GET /reports/exposure ---------------------------------------------------------------------------

public sealed record GetExposureQuery : IRequest<ExposureReport>;

public sealed class GetExposureQueryHandler(IReportQueries reports, TimeProvider clock)
    : IRequestHandler<GetExposureQuery, ExposureReport>
{
    public Task<ExposureReport> Handle(GetExposureQuery request, CancellationToken cancellationToken) =>
        reports.GetExposureAsync(clock.GetUtcNow(), cancellationToken);
}
