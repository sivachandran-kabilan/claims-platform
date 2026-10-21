using System.Text.Json;
using Claims.Application.Common.Messaging;
using Microsoft.Extensions.Logging;

namespace Claims.Infrastructure.Messaging;

/// <summary>
/// What the consumer does with one Kafka message, kept apart from the consume loop so it can be tested
/// without a broker. Unparseable → dead letter at once. Handler failure → retry (default 1 s, 2 s), then
/// dead letter. Either way the caller commits the offset afterwards, so a bad message can't block the partition.
/// </summary>
public sealed class MessageDispatcher(
    Func<EventEnvelope, CancellationToken, Task> handle,
    IDeadLetterPublisher deadLetters,
    IReadOnlyList<TimeSpan> retryDelays,
    ILogger logger)
{
    public static readonly IReadOnlyList<TimeSpan> DefaultRetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    public async Task DispatchAsync(string? key, string value, CancellationToken cancellationToken)
    {
        var envelope = TryParse(value);
        if (envelope is null)
        {
            logger.LogError("Unparseable message with key {Key}; sending to dead-letter topic", key);
            await deadLetters.SendAsync(key, value, "Unparseable", cancellationToken);
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await handle(envelope, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt <= retryDelays.Count)
            {
                logger.LogWarning("Handling event {EventId} failed on attempt {Attempt} ({ErrorType}); retrying",
                    envelope.EventId, attempt, ex.GetType().Name);
                await Task.Delay(retryDelays[attempt - 1], cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("Handling event {EventId} failed after {Attempts} attempts ({ErrorType}); sending to dead-letter topic",
                    envelope.EventId, attempt, ex.GetType().Name);
                await deadLetters.SendAsync(key, value, ex.GetType().Name, cancellationToken);
                return;
            }
        }
    }

    private static EventEnvelope? TryParse(string value)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(value, EventJson.Options);
            return envelope is { EventId: var id, Type: { Length: > 0 } } && id != Guid.Empty ? envelope : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
