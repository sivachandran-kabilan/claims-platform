using Claims.Application.Common.Exceptions;
using Claims.Application.Common.Security;
using Claims.Application.Identity;
using Claims.Domain.Claims;
using Claims.Domain.Common;

namespace Claims.Application.Claims.Common;

/// <summary>A "who may act on this claim" rule, checked after the claim is loaded (403 when false).</summary>
public delegate bool ActRule(Claim claim, ICurrentUser user);

/// <summary>
/// Resource-level access rules (ADR 004). Visibility is applied in queries so it runs in SQL:
/// a claimant sees only their own claims, and anyone else's is indistinguishable from "not found" (404).
/// Acting rules are checked once the claim is loaded: staff act only on claims assigned to them (403).
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

    // ---- Acting rules -----------------------------------------------------------------------------

    /// <summary>A manager on any claim; staff only on claims assigned to them.</summary>
    public static readonly ActRule AssignedStaffOrManager = (claim, user) =>
        user.Role == Roles.ClaimManager
        || (user.Role == Roles.ClaimStaff && claim.AssignedToUserId == user.UserId);

    /// <summary>The claimant who owns the claim.</summary>
    public static readonly ActRule Owner = (claim, user) =>
        user.IsClaimant() && claim.ClaimantUserId == user.UserId;

    public static readonly ActRule ManagerOnly = (_, user) => user.Role == Roles.ClaimManager;

    /// <summary>Documents: the owner, the assigned staff member, or a manager.</summary>
    public static readonly ActRule DocumentUploader = (claim, user) =>
        Owner(claim, user) || AssignedStaffOrManager(claim, user);

    /// <summary>Staff may only assign a claim to themselves; a manager may assign anyone.</summary>
    public static ActRule CanAssign(Guid assigneeUserId) => (_, user) =>
        user.Role == Roles.ClaimManager
        || (user.Role == Roles.ClaimStaff && assigneeUserId == user.UserId);

    public static void EnsureCanAct(Claim claim, ICurrentUser user, ActRule rule)
    {
        if (!rule(claim, user))
        {
            throw new ForbiddenAccessException("You are not allowed to perform this action on this claim.");
        }
    }

    /// <summary>Assignees must be existing ClaimStaff or ClaimManager accounts (422 otherwise).</summary>
    public static async Task EnsureAssignableAsync(
        IIdentityService identity, Guid assigneeUserId, CancellationToken cancellationToken)
    {
        var assignee = await identity.GetUserAsync(assigneeUserId, cancellationToken);
        if (assignee is null || !Roles.Staff.Contains(assignee.Role))
        {
            throw new UnprocessableException("The assignee must be an existing ClaimStaff or ClaimManager account.");
        }
    }
}

/// <summary>Next claim number from the database sequence (never COUNT + 1).</summary>
public interface IClaimNumberGenerator
{
    Task<long> NextAsync(CancellationToken cancellationToken);
}
