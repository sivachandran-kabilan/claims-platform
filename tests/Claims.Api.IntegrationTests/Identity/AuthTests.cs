using System.Net;
using System.Net.Http.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Application.Identity;
using Claims.Application.Identity.Login;
using Claims.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Claims.Api.IntegrationTests.Identity;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ClaimsApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string NewEmail() => $"claimant-{Guid.NewGuid():N}@test.local";

    // AC-AUTH-1 -----------------------------------------------------------------

    [Fact]
    public async Task Register_ValidRequest_Returns201WithClaimantRole()
    {
        var email = NewEmail();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "a-long-enough-pass", displayName = "Alice" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user!.Role.Should().Be(Roles.Claimant);
        user.Email.Should().Be(email);
        (await response.Content.ReadAsStringAsync()).Should().NotContainAny("token", "password");
    }

    [Fact]
    public async Task Register_BodyWithRole_IgnoresRoleAndCreatesClaimant()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = NewEmail(), password = "a-long-enough-pass", displayName = "Mallory", role = Roles.ClaimManager });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<UserResponse>())!.Role.Should().Be(Roles.Claimant);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var body = new { email = NewEmail(), password = "a-long-enough-pass", displayName = "Bob" };
        (await _client.PostAsJsonAsync("/api/v1/auth/register", body)).StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/v1/auth/register", body with { email = body.email.ToUpperInvariant() });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("short")]        // below minimum length (FluentValidation)
    [InlineData("password123")]  // common password (Identity validator)
    public async Task Register_ShortOrCommonPassword_Returns400WithPasswordError(string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = NewEmail(), password, displayName = "Carol" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.Should().Contain(k => k.Equals("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400WithEmailError()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "not-an-email", password = "a-long-enough-pass", displayName = "Dave" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.Should().Contain(k => k.Equals("Email", StringComparison.OrdinalIgnoreCase));
    }

    // AC-AUTH-2 -----------------------------------------------------------------

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenWithExpectedClaims()
    {
        var user = await factory.CreateUserAsync(Roles.ClaimStaff);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, user.Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(login!.AccessToken);
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.GetClaim("email").Value.Should().Be(user.Email);
        jwt.GetClaim("role").Value.Should().Be(Roles.ClaimStaff);
        jwt.Id.Should().NotBeNullOrEmpty();
        jwt.Alg.Should().Be("HS256");
        (jwt.ValidTo - DateTime.UtcNow).Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
        jwt.Claims.Select(c => c.Type).Should().NotContain(["name", "given_name", "phone_number"]);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnIdentical401()
    {
        var user = await factory.CreateUserAsync(Roles.Claimant);

        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, password = "wrong-password-1" });
        var unknownEmail = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = NewEmail(), password = "wrong-password-1" });

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var a = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var b = await unknownEmail.Content.ReadFromJsonAsync<ProblemDetails>();
        b.Should().BeEquivalentTo(a, o => o.Excluding(p => p!.Extensions));
        a!.Detail.Should().BeNull();
    }

    [Fact]
    public async Task Login_FiveFailures_LocksOutEvenWithCorrectPassword()
    {
        var user = await factory.CreateUserAsync(Roles.Claimant);
        for (var i = 0; i < 5; i++)
        {
            (await _client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, password = "wrong-password-1" }))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var correct = await _client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, user.Password });

        correct.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await correct.Content.ReadFromJsonAsync<ProblemDetails>())!.Title.Should().Be("Invalid credentials.");
    }

    // AC-AUTH-3 -----------------------------------------------------------------

    [Fact]
    public async Task Me_ValidToken_ReturnsCurrentUser()
    {
        var user = await factory.CreateUserAsync(Roles.Claimant);
        var client = factory.CreateClientWithToken(await factory.LoginAsync(user));

        var me = await client.GetFromJsonAsync<UserResponse>("/api/v1/auth/me");

        me!.Id.Should().Be(user.Id);
        me.Role.Should().Be(Roles.Claimant);
    }

    [Fact]
    public async Task Me_MissingToken_Returns401ProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    public static TheoryData<string, string> BadTokens() => new()
    {
        { "expired", TestTokens.Create(expires: DateTime.UtcNow.AddMinutes(-5)) },
        { "wrong issuer", TestTokens.Create(issuer: "someone-else") },
        { "wrong audience", TestTokens.Create(audience: "another-app") },
        { "wrong key", TestTokens.Create(key: "a-completely-different-signing-key-0123456789") },
        { "tampered", TestTokens.Tamper(TestTokens.Create()) },
        { "alg none", TestTokens.CreateUnsigned() },
        { "garbage", "not-a-jwt" },
    };

    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task Me_InvalidToken_Returns401(string scenario, string token)
    {
        var response = await factory.CreateClientWithToken(token).GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, scenario);
    }
}
