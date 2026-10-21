using Claims.Application.Identity;
using Claims.Domain.Common;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Claims.Infrastructure.Identity;

/// <summary>
/// Development only: creates the manager, staff and claimant accounts from configuration
/// (Seed:Manager / Seed:Staff / Seed:Claimant in user-secrets). Never overwrites existing users.
/// </summary>
public sealed class DevUserSeeder(
    ClaimsDbContext db,
    IIdentityService identity,
    IConfiguration configuration,
    ILogger<DevUserSeeder> logger)
{
    private static readonly (string Section, string Role)[] Accounts =
    [
        ("Manager", Roles.ClaimManager),
        ("Staff", Roles.ClaimStaff),
        ("Claimant", Roles.Claimant),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            logger.LogWarning("Database has pending migrations; dev users not seeded. Run: dotnet ef database update");
            return;
        }

        foreach (var (section, role) in Accounts)
        {
            var config = configuration.GetSection($"Seed:{section}");
            var email = config["Email"];
            var password = config["Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Seed:{Section} is not configured; skipping", section);
                continue;
            }

            var normalized = EmailAddress.Normalize(email);
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized.ToUpperInvariant(), cancellationToken))
            {
                continue;
            }

            var displayName = config["DisplayName"] ?? $"Dev {section}";
            await identity.CreateUserAsync(new NewUser(normalized, password, displayName, role), cancellationToken);
            logger.LogInformation("Seeded dev {Role} account", role);
        }
    }
}
