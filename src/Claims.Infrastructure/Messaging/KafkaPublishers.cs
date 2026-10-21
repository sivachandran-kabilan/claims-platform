using System.Text;
using System.Text.Json;
using Claims.Application.Common.Messaging;
using Claims.Application.Common.Persistence;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Claims.Infrastructure.Messaging;

/// <summary>Publishes outbox rows to claims.events, keyed by claim id (per-claim ordering).</summary>
internal sealed class KafkaEventPublisher(IProducer<string, string> producer, IOptions<KafkaOptions> options) : IEventPublisher
{
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var value = JsonSerializer.Serialize(EventEnvelope.FromOutbox(message), EventJson.Options);

        // Awaits the broker's delivery report (acks=all); throws ProduceException if not delivered.
        await producer.ProduceAsync(
            options.Value.Topic,
            new Message<string, string> { Key = message.ClaimId.ToString(), Value = value },
            cancellationToken);
    }
}

/// <summary>Where messages go when they can't be handled. Original key/value kept; reason in headers.</summary>
public interface IDeadLetterPublisher
{
    Task SendAsync(string? key, string value, string errorType, CancellationToken cancellationToken);
}

internal sealed class KafkaDeadLetterPublisher(IProducer<string, string> producer, IOptions<KafkaOptions> options) : IDeadLetterPublisher
{
    public async Task SendAsync(string? key, string value, string errorType, CancellationToken cancellationToken)
    {
        var headers = new Headers
        {
            { "x-error-type", Encoding.UTF8.GetBytes(errorType) },
            { "x-original-topic", Encoding.UTF8.GetBytes(options.Value.Topic) },
        };

        await producer.ProduceAsync(
            options.Value.DeadLetterTopic,
            new Message<string, string> { Key = key!, Value = value, Headers = headers },
            cancellationToken);
    }
}
