using System.Text;
using Claims.Application.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Claims.Api.Auth;

/// <summary>Issues HS256 access tokens with sub, email, role, jti and exp (ADR 004). No other user data.</summary>
public sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenGenerator
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Create(UserResponse user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [AuthClaims.Role] = user.Role,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }
}
