namespace Claims.Domain.Common;

/// <summary>The three application roles. Use these constants everywhere; never string literals.</summary>
public static class Roles
{
    public const string Claimant = "Claimant";
    public const string ClaimStaff = "ClaimStaff";
    public const string ClaimManager = "ClaimManager";

    public static readonly IReadOnlyList<string> All = [Claimant, ClaimStaff, ClaimManager];

    /// <summary>Roles a ClaimManager may create through POST /users.</summary>
    public static readonly IReadOnlyList<string> Staff = [ClaimStaff, ClaimManager];
}
