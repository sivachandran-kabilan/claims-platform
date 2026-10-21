using System.Net.Http.Json;
using System.Text.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Application.Common.Messaging;
using Claims.Application.Common.Persistence;
using Claims.Application.Notifications;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using Claims.Infrastructure.Messaging;
using Claims.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claims.Api.IntegrationTests.Messaging;

/// <summary>Stands in for Kafka: records what was published, or fails on demand.</summary>
internal sealed class FakeEventPublisher : IEventPublisher
{
    public List<OutboxMessage> Published { get; } = [];
    public bool Fail { get; set; }

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new InvalidOperationException("Broker unavailable");
        }

        Published.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>AC-EVT-2 (relay), AC-EVT-3 (idempotent consumer), AC-NOT-1 (notifications) against real SQL Server, no broker.</summary>
[Collection(ApiCollection.Name)]
public sealed class MessagingTests(ClaimsApiFactory factory)
{
    private async Task<int> RunRelayPassAsync(FakeEventPublisher publisher)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var processor = new OutboxProcessor(
            scope.ServiceProvider.GetRequiredService<ClaimsDbContext>(), publisher, TimeProvider.System, NullLogger<OutboxProcessor>.Instance);
        return await processor.ProcessBatchAsync(CancellationToken.None);
    }

    /// <summary>Publishes everything other tests left behind, so each test sees only its own rows.</summary>
    private async Task DrainOutboxAsync()
    {
        var publisher = new FakeEventPublisher();
        while (await RunRelayPassAsync(publisher) > 0)
        {
        }
    }

    private async Task HandleAsync(EventEnvelope envelope)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ClaimEventNotificationHandler>().HandleAsync(envelope, CancellationToken.None);
    }

    private Task<List<OutboxMessage>> OutboxRowsAsync(Guid claimId) =>
        factory.WithDbAsync(db => db.OutboxMessages.AsNoTracking().Where(m => m.ClaimId == claimId).ToListAsync());

    private static EventEnvelope Envelope(string type, Guid claimantId, string? outcome = null) =>
        new(Guid.NewGuid(), type, 1, DateTimeOffset.UtcNow, Guid.NewGuid(),
            JsonSerializer.SerializeToElement(new { claimNumber = "CLM-2026-000777", claimantUserId = claimantId, outcome }, EventJson.Options));

    // ---- AC-EVT-2 Relay ---------------------------------------------------------------------------

    [Fact]
    public async Task ProcessBatch_PublisherSucceeds_PublishesInOrderAndMarksProcessed()
    {
        await DrainOutboxAsync();
        var c = await factory.ArrangeClaimAsync(ClaimStatus.InfoRequested); // ClaimSubmitted, then InformationRequested
        var publisher = new FakeEventPublisher();

        await RunRelayPassAsync(publisher);

        publisher.Published.Where(m => m.ClaimId == c.Id).Select(m => m.Type)
            .Should().Equal("ClaimSubmitted", "InformationRequested");
        (await OutboxRowsAsync(c.Id)).Should().OnlyContain(m => m.ProcessedAt != null && m.LastError == null);
    }

    [Fact]
    public async Task ProcessBatch_PublisherFails_RowStaysUnprocessedWithAttemptAndError_StopsBatch()
    {
        await DrainOutboxAsync();
        var first = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var second = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var publisher = new FakeEventPublisher { Fail = true };

        var published = await RunRelayPassAsync(publisher);

        published.Should().Be(0);
        var firstRow = (await OutboxRowsAsync(first.Id)).Single();
        firstRow.ProcessedAt.Should().BeNull();
        firstRow.Attempts.Should().Be(1);
        firstRow.LastError.Should().Contain("Broker unavailable");
        (await OutboxRowsAsync(second.Id)).Single().Attempts.Should().Be(0, "the batch stops at the first failure");
    }

    [Fact]
    public async Task ProcessBatch_AfterFailure_NextPassPublishesNothingLost()
    {
        await DrainOutboxAsync();
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var publisher = new FakeEventPublisher { Fail = true };
        await RunRelayPassAsync(publisher);
        await RunRelayPassAsync(publisher);

        publisher.Fail = false;
        await RunRelayPassAsync(publisher);

        publisher.Published.Should().ContainSingle(m => m.ClaimId == c.Id);
        var row = (await OutboxRowsAsync(c.Id)).Single();
        row.ProcessedAt.Should().NotBeNull();
        row.Attempts.Should().Be(2);
    }

    // ---- AC-EVT-3 Idempotent consumer + notification mapping --------------------------------------

    [Fact]
    public async Task Handle_DuplicateEventId_CreatesOneNotification()
    {
        var claimant = await factory.CreateUserAsync(Roles.Claimant);
        var envelope = Envelope("ClaimSubmitted", claimant.Id);

        await HandleAsync(envelope);
        await HandleAsync(envelope);

        var count = await factory.WithDbAsync(db => db.Notifications.CountAsync(n => n.UserId == claimant.Id));
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownType_MarkedProcessedWithoutNotification()
    {
        var claimant = await factory.CreateUserAsync(Roles.Claimant);
        var envelope = Envelope("SomethingNew", claimant.Id);

        await HandleAsync(envelope);

        var (notifications, processed) = await factory.WithDbAsync(async db => (
            await db.Notifications.CountAsync(n => n.UserId == claimant.Id),
            await db.ProcessedEvents.AnyAsync(e => e.EventId == envelope.EventId)));
        notifications.Should().Be(0);
        processed.Should().BeTrue();
    }

    [Theory]
    [InlineData("ClaimSubmitted", null, "has been received")]
    [InlineData("InformationRequested", null, "need more information")]
    [InlineData("ClaimDecided", "Approved", "has been approved")]
    [InlineData("ClaimDecided", "RejectedNoResponse", "has been rejected")]
    [InlineData("ClaimSettled", null, "has been settled")]
    public async Task Handle_EachEventType_NotifiesClaimantWithExpectedMessage(string type, string? outcome, string expected)
    {
        var claimant = await factory.CreateUserAsync(Roles.Claimant);

        await HandleAsync(Envelope(type, claimant.Id, outcome));

        var notification = await factory.WithDbAsync(db => db.Notifications.SingleAsync(n => n.UserId == claimant.Id));
        notification.Message.Should().Contain(expected).And.Contain("CLM-2026-000777");
        notification.Type.Should().Be(type);
    }

    // ---- AC-NOT-1 GET /notifications/mine ---------------------------------------------------------

    [Fact]
    public async Task NotificationsMine_ReturnsOnlyOwnLatest50NewestFirst()
    {
        var (client, me) = await factory.CreateNewClaimantClientAsync();
        var other = await factory.CreateUserAsync(Roles.Claimant);
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        await factory.WithDbAsync(async db =>
        {
            for (var i = 0; i < 55; i++)
            {
                db.Notifications.Add(new Notification(Guid.NewGuid(), me.Id, Guid.NewGuid(), $"CLM-2026-{i:D6}", "ClaimSubmitted", $"n{i}", start.AddMinutes(i)));
            }

            db.Notifications.Add(new Notification(Guid.NewGuid(), other.Id, Guid.NewGuid(), "CLM-2026-999999", "ClaimSubmitted", "theirs", start));
            await db.SaveChangesAsync();
        });

        var list = await client.GetFromJsonAsync<List<NotificationResponse>>("/api/v1/notifications/mine", ApiJson.Options);

        list.Should().HaveCount(50);
        list!.Select(n => n.CreatedAt).Should().BeInDescendingOrder();
        list.First().Message.Should().Be("n54");
        list.Should().NotContain(n => n.Message == "theirs");
    }

    [Fact]
    public async Task NotificationsMine_EndToEnd_SubmitRelayConsumeThenVisible()
    {
        await DrainOutboxAsync();
        var (client, _) = await factory.CreateNewClaimantClientAsync();
        var claim = await client.SubmitOkAsync();
        var publisher = new FakeEventPublisher();
        await RunRelayPassAsync(publisher);

        // Feed the consumer exactly what the relay would have put on the wire.
        var wire = JsonSerializer.Serialize(EventEnvelope.FromOutbox(publisher.Published.Single(m => m.ClaimId == claim.Id)), EventJson.Options);
        await HandleAsync(JsonSerializer.Deserialize<EventEnvelope>(wire, EventJson.Options)!);

        var list = await client.GetFromJsonAsync<List<NotificationResponse>>("/api/v1/notifications/mine", ApiJson.Options);
        list.Should().ContainSingle().Which.Message.Should().Be($"Your claim {claim.Number} has been received.");
    }
}
