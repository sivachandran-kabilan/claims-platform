using System.Text.Json;
using Claims.Application.Common.Messaging;
using Claims.Application.Common.Persistence;
using Claims.Domain.Claims.Events;
using Claims.Domain.Common;

namespace Claims.Infrastructure.Persistence;

/// <summary>Turns a domain event into an outbox row (ADR 003). Payload is the event record as camelCase JSON.</summary>
internal static class OutboxWriter
{
    public const int EventVersion = 1;

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
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), EventJson.Options),
            domainEvent.OccurredAt);
    }
}
