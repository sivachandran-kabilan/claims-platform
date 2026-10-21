using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Application.Claims.Common;
using Claims.Application.Identity;
using Claims.Domain.Common;
using Claims.Application.Identity.Login;
using Claims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Api.IntegrationTests.Infrastructure;

public sealed record TestUser(Guid Id, string Email, string Password, string Role);

/// <summary>
/// Hosts the API in memory against a throwaway database on the local SQL Server instance (ADR 007).
/// One database per factory instance, migrated on start and dropped at the end.
/// Override the server with the CLAIMS_TEST_SQLSERVER environment variable.
/// </summary>
public class ClaimsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtIssuer = "claims-platform";
    public const string TestJwtAudience = "claims-platform-web";
    public const string TestJwtKey = "integration-tests-only-signing-key-0123456789";
    public const string TestPassword = "Integration-Pass-2026";

    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly ConcurrentDictionary<string, string> _tokensByRole = new();

    public ClaimsApiFactory()
    {
        var server = Environment.GetEnvironmentVariable("CLAIMS_TEST_SQLSERVER") ?? "localhost";
        _databaseName = $"ClaimsPlatform_Tests_{Guid.NewGuid():N}";
        _connectionString = $"Server={server};Database={_databaseName};Integrated Security=true;TrustServerCertificate=true";
    }

    /// <summary>Requests per minute per IP on auth endpoints. High by default: all test requests share one "IP".</summary>
    protected virtual int AuthPermitLimit => 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" keeps Development-only features (Swagger, appsettings.Development.json, dev seeding) out of tests.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Claims", _connectionString);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("RateLimiting:AuthPermitLimit", AuthPermitLimit.ToString());
        builder.UseSetting("Kafka:Enabled", "false"); // no broker in tests; relay/consumer logic is tested directly
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        EnsureOwnDatabase(db);
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            EnsureOwnDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }

    /// <summary>
    /// Safety net before migrating and dropping: the host must be connected to this factory's throwaway
    /// database. Fails loudly if configuration (e.g. a ConnectionStrings__Claims environment variable)
    /// ever won over the test setting, instead of dropping a developer's real database.
    /// </summary>
    private void EnsureOwnDatabase(ClaimsDbContext db)
    {
        var actual = db.Database.GetDbConnection().Database;
        if (actual != _databaseName)
        {
            throw new InvalidOperationException(
                $"Test host is connected to '{actual}', not its throwaway database '{_databaseName}'. Refusing to migrate or drop it.");
        }
    }

    /// <summary>Creates a user with the given role directly through the identity service (bypasses the API).</summary>
    public async Task<TestUser> CreateUserAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        await using var scope = Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var user = await identity.CreateUserAsync(new NewUser(email, TestPassword, $"Test {role}", role), CancellationToken.None);
        return new TestUser(user.Id, email, TestPassword, role);
    }

    public async Task<string> LoginAsync(TestUser user)
    {
        var response = await CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { user.Email, user.Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    /// <summary>A client authenticated as a (cached, per-factory) user in the given role.</summary>
    public async Task<HttpClient> CreateClientForRoleAsync(string role)
    {
        if (!_tokensByRole.TryGetValue(role, out var token))
        {
            token = await LoginAsync(await CreateUserAsync(role));
            _tokensByRole[role] = token;
        }

        return CreateClientWithToken(token);
    }

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>A client for a brand-new user (not cached), for isolation and assignment tests.</summary>
    public async Task<(HttpClient Client, TestUser User)> CreateNewClientAsync(string role)
    {
        var user = await CreateUserAsync(role);
        return (CreateClientWithToken(await LoginAsync(user)), user);
    }

    public Task<(HttpClient Client, TestUser User)> CreateNewClaimantClientAsync() => CreateNewClientAsync(Roles.Claimant);

    /// <summary>Runs code against the test database in its own scope (fresh DbContext).</summary>
    public async Task<T> WithDbAsync<T>(Func<ClaimsDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ClaimsDbContext>());
    }

    public Task WithDbAsync(Func<ClaimsDbContext, Task> action) =>
        WithDbAsync(async db => { await action(db); return true; });
}

public static class ClaimRequests
{
    public static Dictionary<string, object?> ValidBody(string market = "Singapore", string type = "Motor") => new()
    {
        ["type"] = type,
        ["market"] = market,
        ["policyNumber"] = "POL-2026-001",
        ["incidentDate"] = "2026-10-01",
        ["description"] = "Rear-ended at traffic lights",
        ["contactPhone"] = "+6591234567",
        ["claimedAmount"] = 1500.50m,
    };

    public static async Task<HttpResponseMessage> SubmitAsync(
        this HttpClient client, object? body = null, string? idempotencyKey = "generate")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/claims")
        {
            Content = JsonContent.Create(body ?? ValidBody()),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey == "generate" ? Guid.NewGuid().ToString() : idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    public static async Task<ClaimDetailResponse> SubmitOkAsync(this HttpClient client, object? body = null)
    {
        var response = await client.SubmitAsync(body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options))!;
    }
}

/// <summary>Matches the API's JSON settings (enums as strings).</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ClaimsApiFactory>
{
    public const string Name = "Api";
}
