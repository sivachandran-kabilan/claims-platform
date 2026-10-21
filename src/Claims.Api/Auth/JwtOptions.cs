namespace Claims.Api.Auth;

/// <summary>Bound from the "Jwt" section. Key comes from user-secrets; validated at startup (32+ bytes).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
}
