using System.Net.Http.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Application.Claims.Common;
using Claims.Application.Reports;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Api.IntegrationTests.Reports;

/// <summary>
/// AC-RPT-1 (workload) and AC-RPT-2 (exposure). The test DB is shared, so tests use their own staff
/// and backdated claims (seeded through the domain), and exposure checks before/after deltas.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportTests(ClaimsApiFactory factory)
{
    private static readonly Actor Claimant = new(Guid.NewGuid(), Roles.Claimant);

    /// <summary>Creates a claim submitted <paramref name="daysAgo"/> days ago and applies <paramref name="progress"/>.</summary>
    private async Task<Claim> SeedAsync(int daysAgo, Market market = Market.Singapore, ClaimType type = ClaimType.Motor,
        Action<Claim, DateTimeOffset>? progress = null)
    {
        var then = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        await using var scope = factory.Services.CreateAsyncScope();
        var number = await scope.ServiceProvider.GetRequiredService<IClaimNumberGenerator>().NextAsync(CancellationToken.None);
        var claim = Claim.Submit(ClaimNumber.Create(2026, number), Claimant, type, market, "POL-RPT",
            DateOnly.FromDateTime(then.UtcDateTime).AddDays(-1), "Report seed", "+6590000000", 100m, then);
        progress?.Invoke(claim, then);
        await factory.WithDbAsync(async db => { db.Claims.Add(claim); await db.SaveChangesAsync(); });
        return claim;
    }

    private static Action<Claim, DateTimeOffset> AssignTo(Guid staffId, Action<Claim, Actor, DateTimeOffset>? then = null) =>
        (claim, at) =>
        {
            var staff = new Actor(staffId, Roles.ClaimStaff);
            claim.Assign(staffId, staff, at);
            then?.Invoke(claim, staff, at);
        };

    private async Task<WorkloadReport> WorkloadAsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<WorkloadReport>("/api/v1/reports/workload", ApiJson.Options))!;

    private async Task<ExposureReport> ExposureAsync() =>
        (await (await factory.CreateClientForRoleAsync(Roles.ClaimManager))
            .GetFromJsonAsync<ExposureReport>("/api/v1/reports/exposure", ApiJson.Options))!;

    // ---- AC-RPT-1 Workload ------------------------------------------------------------------------

    [Fact]
    public async Task Workload_Manager_ShowsPerStatusCountsAgeAndOldestForStaff()
    {
        var staff = await factory.CreateUserAsync(Roles.ClaimStaff);
        var oldest = await SeedAsync(10, progress: AssignTo(staff.Id));
        await SeedAsync(4, progress: AssignTo(staff.Id, (c, a, t) => c.RequestInformation("Docs?", a, t)));
        await SeedAsync(2, progress: AssignTo(staff.Id, (c, a, t) => { c.SetReserve(500m, a, t); c.Approve(400m, a, t); }));

        var report = await WorkloadAsAsync(await factory.CreateClientForRoleAsync(Roles.ClaimManager));

        var row = report.Staff.Single(s => s.UserId == staff.Id);
        (row.OpenClaims, row.UnderReview, row.InfoRequested, row.Approved).Should().Be((3, 1, 1, 1));
        row.AverageAgeDays.Should().BeApproximately(5.3, 0.1); // (10 + 4 + 2) / 3
        row.OldestClaimNumber.Should().Be(oldest.Number.Value);
        row.OldestAgeDays.Should().BeApproximately(10, 0.1);
        row.Role.Should().Be(Roles.ClaimStaff);
    }

    [Fact]
    public async Task Workload_ExcludesTerminalAndSubmittedFromStaffRows()
    {
        var staff = await factory.CreateUserAsync(Roles.ClaimStaff);
        await SeedAsync(3, progress: AssignTo(staff.Id, (c, a, t) => c.Reject("No cover", a, t)));
        await SeedAsync(3, progress: AssignTo(staff.Id, (c, a, t) => { c.SetReserve(50m, a, t); c.Approve(50m, a, t); c.Settle(50m, a, t); }));
        await SeedAsync(3, progress: AssignTo(staff.Id));

        var report = await WorkloadAsAsync(await factory.CreateClientForRoleAsync(Roles.ClaimManager));

        report.Staff.Single(s => s.UserId == staff.Id).OpenClaims.Should().Be(1);
    }

    [Fact]
    public async Task Workload_Manager_IncludesStaffWithNoClaims()
    {
        var idle = await factory.CreateUserAsync(Roles.ClaimStaff);

        var report = await WorkloadAsAsync(await factory.CreateClientForRoleAsync(Roles.ClaimManager));

        var row = report.Staff.Single(s => s.UserId == idle.Id);
        row.OpenClaims.Should().Be(0);
        row.AverageAgeDays.Should().BeNull();
        row.OldestClaimNumber.Should().BeNull();
        report.Staff.Should().NotContain(s => s.Role == Roles.Claimant);
    }

    [Fact]
    public async Task Workload_Staff_SeesOnlyOwnRowAndQueue()
    {
        var (client, me) = await factory.CreateNewClientAsync(Roles.ClaimStaff);
        await SeedAsync(1, progress: AssignTo(me.Id));
        await factory.CreateUserAsync(Roles.ClaimStaff); // someone else who must not appear

        var report = await WorkloadAsAsync(client);

        report.Staff.Should().ContainSingle().Which.UserId.Should().Be(me.Id);
        report.Staff.Single().OpenClaims.Should().Be(1);
        report.Unassigned.Should().NotBeNull();
    }

    [Fact]
    public async Task Workload_Queue_ReportsCountAndOldestSubmitted()
    {
        var ancient = await SeedAsync(400);

        var report = await WorkloadAsAsync(await factory.CreateClientForRoleAsync(Roles.ClaimManager));

        report.Unassigned.Count.Should().BeGreaterThanOrEqualTo(1);
        report.Unassigned.OldestClaimNumber.Should().Be(ancient.Number.Value);
        report.Unassigned.OldestAgeDays.Should().BeApproximately(400, 0.1);
        report.Unassigned.AverageAgeDays.Should().NotBeNull();
    }

    // ---- AC-RPT-2 Exposure ------------------------------------------------------------------------

    private static ExposureRow RowOf(ExposureReport report, Market market, ClaimType type, ClaimStatus status) =>
        report.Rows.SingleOrDefault(r => r.Market == market && r.Type == type && r.Status == status)
        ?? new ExposureRow(market, type, status, market.Currency(), 0, 0, 0);

    private static decimal TotalOf(ExposureReport report, string currency) =>
        report.Totals.SingleOrDefault(t => t.Currency == currency)?.OutstandingReserve ?? 0;

    [Fact]
    public async Task Exposure_AddsOutstandingForOpenClaims_CountsUnreserved()
    {
        var staffId = (await factory.CreateUserAsync(Roles.ClaimStaff)).Id;
        var before = RowOf(await ExposureAsync(), Market.Philippines, ClaimType.Property, ClaimStatus.UnderReview);

        await SeedAsync(1, Market.Philippines, ClaimType.Property, AssignTo(staffId, (c, a, t) => c.SetReserve(1000.50m, a, t)));
        await SeedAsync(1, Market.Philippines, ClaimType.Property, AssignTo(staffId));

        var after = RowOf(await ExposureAsync(), Market.Philippines, ClaimType.Property, ClaimStatus.UnderReview);
        (after.OutstandingReserve - before.OutstandingReserve).Should().Be(1000.50m);
        (after.ClaimCount - before.ClaimCount).Should().Be(2);
        (after.UnreservedCount - before.UnreservedCount).Should().Be(1);
        after.Currency.Should().Be("PHP");
    }

    [Fact]
    public async Task Exposure_ApprovedClaim_UsesApprovedAmount()
    {
        var staffId = (await factory.CreateUserAsync(Roles.ClaimStaff)).Id;
        var before = RowOf(await ExposureAsync(), Market.Malaysia, ClaimType.Motor, ClaimStatus.Approved);

        await SeedAsync(1, Market.Malaysia, ClaimType.Motor, AssignTo(staffId, (c, a, t) => { c.SetReserve(900m, a, t); c.Approve(750m, a, t); }));

        var after = RowOf(await ExposureAsync(), Market.Malaysia, ClaimType.Motor, ClaimStatus.Approved);
        (after.OutstandingReserve - before.OutstandingReserve).Should().Be(750m);
    }

    [Fact]
    public async Task Exposure_ExcludesRejectedAndSettled()
    {
        var staffId = (await factory.CreateUserAsync(Roles.ClaimStaff)).Id;
        var before = await ExposureAsync();

        await SeedAsync(1, Market.HongKong, ClaimType.Property, AssignTo(staffId, (c, a, t) => { c.SetReserve(300m, a, t); c.Reject("No", a, t); }));
        await SeedAsync(1, Market.HongKong, ClaimType.Property, AssignTo(staffId, (c, a, t) => { c.SetReserve(300m, a, t); c.Approve(300m, a, t); c.Settle(300m, a, t); }));

        var after = await ExposureAsync();
        after.Totals.Should().BeEquivalentTo(before.Totals);
        after.Rows.Should().NotContain(r => r.Status == ClaimStatus.Rejected || r.Status == ClaimStatus.Settled);
    }

    [Fact]
    public async Task Exposure_Totals_ArePerCurrency()
    {
        var staffId = (await factory.CreateUserAsync(Roles.ClaimStaff)).Id;
        var before = await ExposureAsync();

        await SeedAsync(1, Market.Australia, ClaimType.Motor, AssignTo(staffId, (c, a, t) => c.SetReserve(100m, a, t)));
        await SeedAsync(1, Market.Thailand, ClaimType.Motor, AssignTo(staffId, (c, a, t) => c.SetReserve(200m, a, t)));

        var after = await ExposureAsync();
        (TotalOf(after, "AUD") - TotalOf(before, "AUD")).Should().Be(100m);
        (TotalOf(after, "THB") - TotalOf(before, "THB")).Should().Be(200m);
        (TotalOf(after, "SGD") - TotalOf(before, "SGD")).Should().Be(0m);
        after.Totals.Select(t => t.Currency).Should().OnlyHaveUniqueItems();
    }
}
