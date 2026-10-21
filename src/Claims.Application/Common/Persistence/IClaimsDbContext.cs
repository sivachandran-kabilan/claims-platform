using Claims.Domain.Claims;
using Microsoft.EntityFrameworkCore;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public static class ClaimsDbConstants
{
    /// <summary>Shadow rowversion column on Claims: the ETag source. Not part of the domain model.</summary>
    public const string RowVersion = "RowVersion";
}
