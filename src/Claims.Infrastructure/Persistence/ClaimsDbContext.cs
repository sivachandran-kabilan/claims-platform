using Claims.Application.Common.Persistence;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using Claims.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Persistence;

/// <summary>
/// The single DbContext (ADR 001), including the Identity tables, so claim changes and their
/// outbox rows commit in one transaction (ADR 003).
/// </summary>
public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IClaimsDbContext
{
    public const string ClaimNumberSequence = "ClaimNumbers";

    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    /// <summary>
    /// Collects domain events from tracked aggregates and saves them as outbox rows in the same SaveChanges
    /// (one transaction). Events are cleared only after a successful save; on failure the outbox rows are
    /// detached so a retry doesn't add them twice.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();
        var messages = aggregates.SelectMany(a => a.DomainEvents).Select(OutboxWriter.ToOutboxMessage).ToList();
        OutboxMessages.AddRange(messages);

        int written;
        try
        {
            written = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch
        {
            messages.ForEach(m => Entry(m).State = EntityState.Detached);
            throw;
        }

        aggregates.ForEach(a => a.ClearDomainEvents());
        return written;
    }

    /// <summary>Synchronous saves would skip the outbox; everything in this app is async.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync so domain events reach the outbox.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>(ClaimNumberSequence).StartsAt(1).IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);
    }
}
