using Claims.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Claims.Application.Common.Persistence;

/// <summary>
/// The data-access port (approved slice 4): handlers query and save through EF directly.
/// Implemented by ClaimsDbContext, whose SaveChangesAsync also writes domain events to the outbox.
/// </summary>
public interface IClaimsDbContext
{
    DbSet<Claim> Claims { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ProcessedEvent> ProcessedEvents { get; }

    /// <summary>Change-tracking entry; the workflow uses it for the rowversion (If-Match) check.</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public static class ClaimsDbConstants
{
    /// <summary>Shadow rowversion column on Claims: the ETag source. Not part of the domain model.</summary>
    public const string RowVersion = "RowVersion";
}
