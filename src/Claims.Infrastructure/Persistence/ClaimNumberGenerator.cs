using Claims.Application.Claims.Common;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Persistence;

/// <summary>
/// Next value of the ClaimNumbers sequence. Global (not reset per year); a failed save leaves a gap.
/// Not composed into a subquery because SQL Server doesn't allow NEXT VALUE FOR there.
/// </summary>
internal sealed class ClaimNumberGenerator(ClaimsDbContext db) : IClaimNumberGenerator
{
    public async Task<long> NextAsync(CancellationToken cancellationToken)
    {
        var values = await db.Database
            .SqlQuery<long>($"SELECT NEXT VALUE FOR dbo.ClaimNumbers AS [Value]")
            .ToListAsync(cancellationToken);
        return values.Single();
    }
}
