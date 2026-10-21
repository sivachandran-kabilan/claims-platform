using System.Text;
using System.Threading.RateLimiting;
using Claims.Application.Common.Security;
using Claims.Application.Identity;
using Claims.Domain.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Claims.Api.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddClaimsAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= 32, "Jwt:Key must be at least 32 bytes (set it in appsettings.Development.json).")
            .Validate(o => o.Issuer.Length > 0 && o.Audience.Length > 0, "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(o => o.AccessTokenMinutes is > 0 and <= 15, "Jwt:AccessTokenMinutes must be between 1 and 15.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false; // keep "sub" and "role" as issued
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256], // rejects alg=none and anything else
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = AuthClaims.Subject,
                    RoleClaimType = AuthClaims.Role,
                };
            });

        services.AddAuthorizationBuilder()
            // Anything without an explicit [AllowAnonymous] requires a valid token.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.AnyUser, p => p.RequireAuthenticatedUser())
            .AddPolicy(Policies.Claimant, p => p.RequireRole(Roles.Claimant))
            .AddPolicy(Policies.Staff, p => p.RequireRole(Roles.ClaimStaff, Roles.ClaimManager))
            .AddPolicy(Policies.Manager, p => p.RequireRole(Roles.ClaimManager));

        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Auth, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
