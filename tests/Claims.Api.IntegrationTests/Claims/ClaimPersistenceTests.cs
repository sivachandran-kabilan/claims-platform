using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Application.Claims.Common;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using Claims.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Api.IntegrationTests.Claims;

/// <summary>EF mapping round-trips and the outbox transaction guarantee (AC-EVT-1).</summary>
[Collection(ApiCollection.Name)]
public sealed class ClaimPersistenceTests(ClaimsApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 3, 0, 0, TimeSpan.Zero);

    private async Task<Claim> NewClaimAsync(Actor claimant)
    {
        long number;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            number = await scope.ServiceProvider.GetRequiredService<IClaimNumberGenerator>().NextAsync(CancellationToken.None);
        }

        return Claim.Submit(ClaimNumber.Create(2026, number), claimant, ClaimType.Property, Market.HongKong,
            "POL-HK-9", new DateOnly(2026, 10, 2), "Water damage", "+85291234567", 5000m, Now);
    }

    private Task<Claim> LoadAsync(ClaimsDbContext db, Guid id) =>
        db.Claims.Include(c => c.InformationRequests).Include(c => c.Documents).Include(c => c.History)
            .SingleAsync(c => c.Id == id);

    private async Task StepAsync(Guid id, Action<Claim> change) =>
        await factory.WithDbAsync(async db =>
        {
            change(await LoadAsync(db, id));
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task ClaimMapping_FullLifecycle_RoundTripsAllFieldsAndChildren()
    {
        var claimant = await factory.CreateUserAsync(Roles.Claimant);
        var staff = await factory.CreateUserAsync(Roles.ClaimStaff);
        var c = new Actor(claimant.Id, Roles.Claimant);
        var s = new Actor(staff.Id, Roles.ClaimStaff);
        var claim = await NewClaimAsync(c);
        await factory.WithDbAsync(async db => { db.Claims.Add(claim); await db.SaveChangesAsync(); });

        // Each step runs in a fresh DbContext, so every value must survive a database round trip.
        await StepAsync(claim.Id, x => x.Assign(staff.Id, s, Now));
        await StepAsync(claim.Id, x => x.RequestInformation("Photos please", s, Now));
        await StepAsync(claim.Id, x => x.AddDocument("photo.jpg", "image/jpeg", 2048, "blob://1", c, Now));
        await StepAsync(claim.Id, x => x.RespondToInformation("Uploaded", c, Now));
        await StepAsync(claim.Id, x => x.SetReserve(4000.25m, s, Now));
        await StepAsync(claim.Id, x => x.Approve(3500.10m, s, Now));
        await StepAsync(claim.Id, x => x.Settle(3500.10m, s, Now));

        var loaded = await factory.WithDbAsync(db => LoadAsync(db, claim.Id));
        loaded.Number.Should().Be(claim.Number);
        loaded.Status.Should().Be(ClaimStatus.Settled);
        loaded.Market.Should().Be(Market.HongKong);
        loaded.Currency.Should().Be("HKD");
        loaded.IncidentDate.Should().Be(new DateOnly(2026, 10, 2));
        loaded.Reserve.Should().Be(4000.25m);
        loaded.ApprovedAmount.Should().Be(3500.10m);
        loaded.SettledAmount.Should().Be(3500.10m);
        loaded.OutstandingReserve.Should().Be(0);
        loaded.AssignedToUserId.Should().Be(staff.Id);
        loaded.SettledAt.Should().Be(Now);
        loaded.InformationRequests.Should().ContainSingle().Which.Response.Should().Be("Uploaded");
        loaded.Documents.Should().ContainSingle().Which.InformationRequestId.Should().Be(loaded.InformationRequests.Single().Id);
        loaded.History.OrderBy(h => h.Sequence).Select(h => h.Action).Should().Equal(
            ClaimAction.Submit, ClaimAction.Assign, ClaimAction.RequestInformation, ClaimAction.AddDocument,
            ClaimAction.RespondToInformation, ClaimAction.SetReserve, ClaimAction.Approve, ClaimAction.Settle);

        var eventTypes = await factory.WithDbAsync(db =>
            db.OutboxMessages.Where(m => m.ClaimId == claim.Id).OrderBy(m => m.OccurredAt).Select(m => m.Type).ToListAsync());
        eventTypes.Should().BeEquivalentTo(["ClaimSubmitted", "InformationRequested", "ClaimDecided", "ClaimSettled"]);
    }

    [Fact]
    public async Task SaveChanges_WhenSaveFails_WritesNoOutboxRowAndKeepsEvents()
    {
        var claimant = new Actor((await factory.CreateUserAsync(Roles.Claimant)).Id, Roles.Claimant);
        var first = await NewClaimAsync(claimant);
        await factory.WithDbAsync(async db => { db.Claims.Add(first); await db.SaveChangesAsync(); });

        // Same claim number violates the unique index, so the whole transaction must roll back.
        var duplicate = Claim.Submit(first.Number, claimant, ClaimType.Motor, Market.Malaysia,
            "POL-MY-1", new DateOnly(2026, 10, 3), "Dent", "+60123456789", 100m, Now);
        var act = () => factory.WithDbAsync(async db => { db.Claims.Add(duplicate); await db.SaveChangesAsync(); });

        await act.Should().ThrowAsync<DbUpdateException>();
        var outboxRows = await factory.WithDbAsync(db => db.OutboxMessages.CountAsync(m => m.ClaimId == duplicate.Id));
        outboxRows.Should().Be(0);
        duplicate.DomainEvents.Should().ContainSingle("events are only cleared after a successful save");
    }
}
