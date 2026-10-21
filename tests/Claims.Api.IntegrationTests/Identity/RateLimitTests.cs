using System.Net;
using System.Net.Http.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Claims.Api.IntegrationTests.Identity;

/// <summary>Own host with a tiny limit, so it can't interfere with the shared test host.</summary>
public sealed class RateLimitedApiFactory : ClaimsApiFactory
{
    protected override int AuthPermitLimit => 3;
}

public sealed class RateLimitTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Login_OverRateLimit_Returns429ProblemDetails()
    {
        var client = factory.CreateClient();
        var body = new { email = "nobody@test.local", password = "wrong-password-1" };
        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/auth/login", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", body);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
