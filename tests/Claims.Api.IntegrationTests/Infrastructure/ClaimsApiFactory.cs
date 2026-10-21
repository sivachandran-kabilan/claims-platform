using Claims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the API in memory against a throwaway database on the local SQL Server instance (ADR 007).
/// One database per test run; dropped when the run finishes.
/// Override the server with the CLAIMS_TEST_SQLSERVER environment variable.
/// </summary>
public sealed class ClaimsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString;

    public ClaimsApiFactory()
    {
        var server = Environment.GetEnvironmentVariable("CLAIMS_TEST_SQLSERVER") ?? "localhost";
        var database = $"ClaimsPlatform_Tests_{Guid.NewGuid():N}";
        _connectionString = $"Server={server};Database={database};Integrated Security=true;TrustServerCertificate=true";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" keeps Development-only features (Swagger, user-secrets, dev seeding) out of tests.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Claims", _connectionString);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ClaimsApiFactory>
{
    public const string Name = "Api";
}
