using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Application.Common.Persistence;

namespace Claims.Application.Common.Messaging;

/// <summary>
/// The Kafka message value on claims.events (ADR 002): eventId, type, version, occurredAt, claimId, payload.
/// The message key is the claim id, which keeps each claim's events in order.
/// </summary>
public sealed record EventEnvelope(
    Guid EventId, string Type, int Version, DateTimeOffset OccurredAt, Guid ClaimId, JsonElement Payload)
{
    public static EventEnvelope FromOutbox(OutboxMessage message)
    {
        using var payload = JsonDocument.Parse(message.Payload);
        return new EventEnvelope(
            message.Id, message.Type, message.Version, message.OccurredAt, message.ClaimId, payload.RootElement.Clone());
    }
}

/// <summary>JSON settings shared by the outbox payload, the envelope and the consumer: camelCase, enums as names.</summary>
public static class EventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

/// <summary>Publishes one outbox row to the event stream. Throws if the broker does not confirm delivery.</summary>
public interface IEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
