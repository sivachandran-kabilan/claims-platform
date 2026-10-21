namespace Claims.Api.Auth;

/// <summary>Authorization policy names used in [Authorize(Policy = ...)]. Defined in AuthSetup.</summary>
public static class Policies
{
    public const string AnyUser = nameof(AnyUser);
    public const string Claimant = nameof(Claimant);
    public const string Staff = nameof(Staff);     // ClaimStaff or ClaimManager
    public const string Manager = nameof(Manager);
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}
