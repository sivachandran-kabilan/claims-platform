using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Comes from user-secrets or environment variables; never committed.
        var connectionString = configuration.GetConnectionString("Claims")
            ?? throw new InvalidOperationException(
                "Connection string 'Claims' is not configured. Set it with dotnet user-secrets (see README).");

        services.AddDbContext<ClaimsDbContext>(options => options.UseSqlServer(connectionString));

        return services;
    }
}
