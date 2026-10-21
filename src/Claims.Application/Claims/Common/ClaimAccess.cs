using Claims.Application.Common.Security;
using Claims.Domain.Claims;
using Claims.Domain.Common;

namespace Claims.Application.Claims.Common;

/// <summary>
/// Resource-level access rules (ADR 004), applied in queries so they run in SQL.
/// A claimant sees only their own claims; anyone else's is indistinguishable from "not found" (404).
/// Staff and managers see every claim. "Who may act" rules are added in the workflow slice.
/// </summary>
public static class ClaimAccess
{
    public static bool IsClaimant(this ICurrentUser user) => user.Role == Roles.Claimant;

    public static IQueryable<Claim> VisibleTo(this IQueryable<Claim> claims, ICurrentUser user)
    {
        if (!user.IsClaimant())
        {
            return claims;
        }

        var userId = user.UserId;
        return claims.Where(c => c.ClaimantUserId == userId);
    }

    public static Actor ToActor(this ICurrentUser user) => new(user.UserId, user.Role);
}

/// <summary>Next claim number from the database sequence (never COUNT + 1).</summary>
public interface IClaimNumberGenerator
{
    Task<long> NextAsync(CancellationToken cancellationToken);
}
