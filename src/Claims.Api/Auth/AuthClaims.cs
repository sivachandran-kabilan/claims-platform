using System.Security.Claims;
using Claims.Application.Common.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Claims.Api.Auth;

/// <summary>Claim names as they appear in our tokens (inbound claim mapping is off).</summary>
public static class AuthClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Role = "role";
}

/// <summary>Reads the caller from the validated token. Endpoints using it are always authenticated.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP context.");

    public Guid UserId => Guid.Parse(
        Principal.FindFirstValue(AuthClaims.Subject) ?? throw new InvalidOperationException("Token has no sub claim."));

    public string Role =>
        Principal.FindFirstValue(AuthClaims.Role) ?? throw new InvalidOperationException("Token has no role claim.");
}
