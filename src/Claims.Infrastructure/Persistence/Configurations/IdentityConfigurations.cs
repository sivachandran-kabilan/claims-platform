using Claims.Domain.Common;
using Claims.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder) =>
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
}

/// <summary>Roles are reference data, seeded by migration in every environment.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder) => builder.HasData(
        Role("6f1e3c2a-0b7d-4c5e-9a10-000000000001", Roles.Claimant),
        Role("6f1e3c2a-0b7d-4c5e-9a10-000000000002", Roles.ClaimStaff),
        Role("6f1e3c2a-0b7d-4c5e-9a10-000000000003", Roles.ClaimManager));

    // Fixed id and concurrency stamp keep the seed stable, so later migrations don't re-emit it.
    private static IdentityRole<Guid> Role(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id,
    };
}
