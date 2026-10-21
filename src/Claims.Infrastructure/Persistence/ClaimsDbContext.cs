using Claims.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Persistence;

/// <summary>
/// The single DbContext (ADR 001), including the Identity tables, so claim changes and
/// outbox rows can share one transaction. Claim, outbox and notification tables come in later slices.
/// </summary>
public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);
    }
}
