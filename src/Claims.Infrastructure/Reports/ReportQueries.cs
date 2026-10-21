using Claims.Application.Reports;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Reports;

/// <summary>
/// Report queries as SQL projections (GroupBy / Count / Sum / Average). No claim aggregates or history are
/// loaded; only grouped rows and the small staff list come back. Indexes: (Status, AssignedToUserId) and
/// (Market, Type, Status) include the columns these queries read.
/// </summary>
internal sealed class ReportQueries(ClaimsDbContext db) : IReportQueries
{
    private static readonly ClaimStatus[] OpenWorkload = [ClaimStatus.UnderReview, ClaimStatus.InfoRequested, ClaimStatus.Approved];

    public async Task<WorkloadReport> GetWorkloadAsync(Guid? onlyUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var assigned = db.Claims.AsNoTracking()
            .Where(c => c.AssignedToUserId != null && OpenWorkload.Contains(c.Status));
        if (onlyUserId is { } userId)
        {
            assigned = assigned.Where(c => c.AssignedToUserId == userId);
        }

        var perAssignee = await assigned
            .GroupBy(c => c.AssignedToUserId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                UnderReview = g.Count(c => c.Status == ClaimStatus.UnderReview),
                InfoRequested = g.Count(c => c.Status == ClaimStatus.InfoRequested),
                Approved = g.Count(c => c.Status == ClaimStatus.Approved),
                AverageHours = g.Average(c => (double)EF.Functions.DateDiffHour(c.SubmittedAt, now)),
                Oldest = g.Min(c => c.SubmittedAt),
                OldestNumber = g.OrderBy(c => c.SubmittedAt).Select(c => c.Number).First(),
            })
            .ToDictionaryAsync(x => x.UserId, cancellationToken);

        // Every staff/manager account, so people with no open claims still show (manager view).
        var staffRoles = Roles.Staff.ToArray();
        var staff = await (
                from user in db.Users.AsNoTracking()
                join userRole in db.UserRoles on user.Id equals userRole.UserId
                join role in db.Roles on userRole.RoleId equals role.Id
                where staffRoles.Contains(role.Name!) && (onlyUserId == null || user.Id == onlyUserId)
                orderby user.DisplayName
                select new { user.Id, user.DisplayName, Role = role.Name! })
            .ToListAsync(cancellationToken);

        var rows = staff.Select(s => perAssignee.TryGetValue(s.Id, out var a)
                ? new StaffWorkload(s.Id, s.DisplayName, s.Role, a.UnderReview + a.InfoRequested + a.Approved,
                    a.UnderReview, a.InfoRequested, a.Approved, Days(a.AverageHours), a.OldestNumber.Value, AgeDays(now, a.Oldest))
                : new StaffWorkload(s.Id, s.DisplayName, s.Role, 0, 0, 0, 0, null, null, null))
            .ToList();

        var queue = await db.Claims.AsNoTracking()
            .Where(c => c.Status == ClaimStatus.Submitted)
            .GroupBy(c => 1)
            .Select(g => new
            {
                Count = g.Count(),
                AverageHours = g.Average(c => (double)EF.Functions.DateDiffHour(c.SubmittedAt, now)),
                Oldest = g.Min(c => c.SubmittedAt),
                OldestNumber = g.OrderBy(c => c.SubmittedAt).Select(c => c.Number).First(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        var unassigned = queue is null
            ? new QueueSummary(0, null, null, null)
            : new QueueSummary(queue.Count, Days(queue.AverageHours), queue.OldestNumber.Value, AgeDays(now, queue.Oldest));

        return new WorkloadReport(now, rows, unassigned);
    }

    public async Task<ExposureReport> GetExposureAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var grouped = await db.Claims.AsNoTracking()
            .Where(c => c.Status != ClaimStatus.Rejected && c.Status != ClaimStatus.Settled)
            .GroupBy(c => new { c.Market, c.Type, c.Status, c.Currency })
            .Select(g => new
            {
                g.Key.Market,
                g.Key.Type,
                g.Key.Status,
                g.Key.Currency,
                Outstanding = g.Sum(c => c.OutstandingReserve),
                Count = g.Count(),
                Unreserved = g.Count(c => c.Reserve == null),
            })
            .ToListAsync(cancellationToken);

        // At most 6 markets x 2 types x 4 statuses rows, so ordering and per-currency totals are done here.
        var rows = grouped
            .Select(g => new ExposureRow(g.Market, g.Type, g.Status, g.Currency, g.Outstanding, g.Count, g.Unreserved))
            .OrderBy(r => r.Market).ThenBy(r => r.Type).ThenBy(r => r.Status)
            .ToList();
        var totals = rows
            .GroupBy(r => r.Currency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(r => r.OutstandingReserve), g.Sum(r => r.ClaimCount)))
            .OrderBy(t => t.Currency)
            .ToList();

        return new ExposureReport(now, rows, totals);
    }

    private static double Days(double hours) => Math.Round(hours / 24, 1);

    private static double AgeDays(DateTimeOffset now, DateTimeOffset submittedAt) => Math.Round((now - submittedAt).TotalDays, 1);
}
