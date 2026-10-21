using Claims.Application.Common.Messaging;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Claims.Infrastructure.Messaging;

/// <summary>
/// One relay pass (ADR 003): publish unprocessed outbox rows oldest first. On the first failure the row
/// keeps ProcessedAt = null (Attempts+1, LastError set) and the batch stops, preserving order; the next
/// pass retries. Nothing is lost while the broker is down.
/// </summary>
public sealed class OutboxProcessor(
    ClaimsDbContext db, IEventPublisher publisher, TimeProvider clock, ILogger<OutboxProcessor> logger)
{
    public const int BatchSize = 50;

    /// <returns>The number of rows published.</returns>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt).ThenBy(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var published = 0;
        foreach (var message in batch)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RecordFailure($"{ex.GetType().Name}: {ex.Message}", clock.GetUtcNow());
                await db.SaveChangesAsync(cancellationToken);
                logger.LogWarning(
                    "Publishing event {EventId} failed (attempt {Attempts}); will retry on the next pass",
                    message.Id, message.Attempts);
                break;
            }

            message.MarkProcessed(clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            published++;
        }

        return published;
    }
}
