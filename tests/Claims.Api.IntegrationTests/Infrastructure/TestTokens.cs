using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Claims.Api.IntegrationTests.Infrastructure;

/// <summary>Mints tokens with chosen defects to prove the API rejects them.</summary>
public static class TestTokens
{
    public static string Create(
        string issuer = ClaimsApiFactory.TestJwtIssuer,
        string audience = ClaimsApiFactory.TestJwtAudience,
        string key = ClaimsApiFactory.TestJwtKey,
        DateTime? expires = null,
        string role = "ClaimManager")
    {
        var expiry = expires ?? DateTime.UtcNow.AddMinutes(10);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expiry.AddMinutes(-15),
            NotBefore = expiry.AddMinutes(-15),
            Expires = expiry,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = Guid.NewGuid().ToString(),
                ["role"] = role,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>Unsigned token: header alg "none", empty signature.</summary>
    public static string CreateUnsigned()
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var payload = Base64UrlEncoder.Encode(
            $$"""{"sub":"{{Guid.NewGuid()}}","role":"ClaimManager","iss":"{{ClaimsApiFactory.TestJwtIssuer}}","aud":"{{ClaimsApiFactory.TestJwtAudience}}","exp":{{exp}}}""");
        return $"{header}.{payload}.";
    }

    /// <summary>
    /// Valid token with the first character of the signature changed. Not the last character: its low
    /// 2 bits are base64url padding the decoder ignores, so 'A' → 'B' there left the signature valid (flaky).
    /// </summary>
    public static string Tamper(string token)
    {
        var i = token.LastIndexOf('.') + 1;
        return token[..i] + (token[i] == 'A' ? 'B' : 'A') + token[(i + 1)..];
    }
}
