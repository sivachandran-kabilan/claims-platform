using System.Text.Json;
using Claims.Application.Common.Messaging;
using Claims.Application.Common.Persistence;
using Claims.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claims.Api.IntegrationTests.Messaging;

internal sealed class FakeDeadLetters : IDeadLetterPublisher
{
    public List<(string? Key, string Value, string ErrorType)> Sent { get; } = [];

    public Task SendAsync(string? key, string value, string errorType, CancellationToken cancellationToken)
    {
        Sent.Add((key, value, errorType));
        return Task.CompletedTask;
    }
}

/// <summary>Consumer retry / dead-letter logic and the wire envelope, without a broker (AC-EVT-3, AC-EVT-4).</summary>
public sealed class DispatcherTests
{
    private static readonly TimeSpan[] NoDelay = [TimeSpan.Zero, TimeSpan.Zero];

    private static string ValidMessage() => JsonSerializer.Serialize(
        new EventEnvelope(Guid.NewGuid(), "ClaimSubmitted", 1, DateTimeOffset.UtcNow, Guid.NewGuid(),
            JsonSerializer.SerializeToElement(new { claimNumber = "CLM-2026-000001" })),
        EventJson.Options);

    private static (MessageDispatcher Dispatcher, FakeDeadLetters DeadLetters, Func<int> Calls) Create(int failuresBeforeSuccess)
    {
        var calls = 0;
        var deadLetters = new FakeDeadLetters();
        var dispatcher = new MessageDispatcher(
            (_, _) => ++calls <= failuresBeforeSuccess ? throw new InvalidOperationException("db down") : Task.CompletedTask,
            deadLetters, NoDelay, NullLogger.Instance);
        return (dispatcher, deadLetters, () => calls);
    }

    [Fact]
    public async Task Dispatch_HandlerSucceeds_NoDeadLetter()
    {
        var (dispatcher, deadLetters, calls) = Create(failuresBeforeSuccess: 0);

        await dispatcher.DispatchAsync("key", ValidMessage(), CancellationToken.None);

        calls().Should().Be(1);
        deadLetters.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispatch_HandlerFailsOnceThenSucceeds_RetriesWithoutDeadLetter()
    {
        var (dispatcher, deadLetters, calls) = Create(failuresBeforeSuccess: 1);

        await dispatcher.DispatchAsync("key", ValidMessage(), CancellationToken.None);

        calls().Should().Be(2);
        deadLetters.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispatch_HandlerFailsThreeTimes_SendsToDeadLetterWithErrorType()
    {
        var (dispatcher, deadLetters, calls) = Create(failuresBeforeSuccess: int.MaxValue);
        var message = ValidMessage();

        await dispatcher.DispatchAsync("claim-key", message, CancellationToken.None);

        calls().Should().Be(3);
        deadLetters.Sent.Should().ContainSingle().Which.Should().Be(("claim-key", message, nameof(InvalidOperationException)));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"hello\":\"world\"}")]
    public async Task Dispatch_UnparseableMessage_SendsToDeadLetterWithoutCallingHandler(string value)
    {
        var (dispatcher, deadLetters, calls) = Create(failuresBeforeSuccess: 0);

        await dispatcher.DispatchAsync("k", value, CancellationToken.None);

        calls().Should().Be(0);
        deadLetters.Sent.Should().ContainSingle().Which.ErrorType.Should().Be("Unparseable");
    }

    [Fact]
    public void Envelope_FromOutbox_SerializesRequiredFieldsWithNestedPayload()
    {
        var claimId = Guid.NewGuid();
        var message = new OutboxMessage(Guid.NewGuid(), "ClaimSubmitted", 1, claimId,
            """{"claimNumber":"CLM-2026-000042","status":"Submitted"}""", DateTimeOffset.UtcNow);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(EventEnvelope.FromOutbox(message), EventJson.Options));

        var root = json.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["eventId", "type", "version", "occurredAt", "claimId", "payload"]);
        root.GetProperty("eventId").GetGuid().Should().Be(message.Id);
        root.GetProperty("claimId").GetGuid().Should().Be(claimId);
        root.GetProperty("payload").GetProperty("claimNumber").GetString().Should().Be("CLM-2026-000042");
    }
}
