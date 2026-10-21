using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Application.Common.Persistence;
using Claims.Domain.Claims.Events;
using Claims.Domain.Common;

namespace Claims.Infrastructure.Persistence;

/// <summary>Turns a domain event into an outbox row (ADR 003). Payload is the event record as camelCase JSON.</summary>
internal static class OutboxWriter
{
    public const int EventVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static OutboxMessage ToOutboxMessage(IDomainEvent domainEvent)
    {
        if (domainEvent is not IClaimEvent claimEvent)
        {
            throw new InvalidOperationException($"{domainEvent.GetType().Name} has no claim id for the message key.");
        }

        return new OutboxMessage(
            domainEvent.EventId,
            domainEvent.GetType().Name,
            EventVersion,
            claimEvent.ClaimId,
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
            domainEvent.OccurredAt);
    }
}
