using Microsoft.AspNetCore.Identity;

namespace Claims.Infrastructure.Identity;

/// <summary>Identity user. Guid keys match the user ids held by the domain (e.g. Claim.ClaimantUserId).</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
