using Claims.Application.Claims.Common;
using Claims.Application.Common.Messaging;
using Claims.Application.Common.Persistence;
using Claims.Infrastructure.Messaging;
using Confluent.Kafka;
using Claims.Application.Identity;
using Claims.Infrastructure.Identity;
using Claims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
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

        // Split queries: a claim detail loads 3 child collections without multiplying rows.
        services.AddDbContext<ClaimsDbContext>(options => options.UseSqlServer(
            connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddScoped<IClaimsDbContext>(sp => sp.GetRequiredService<ClaimsDbContext>());
        services.AddScoped<IClaimNumberGenerator, ClaimNumberGenerator>();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Length over composition (CLAUDE.md: no composition theatre).
                options.Password.RequiredLength = IdentityRules.PasswordMinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.User.RequireUniqueEmail = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ClaimsDbContext>()
            .AddSignInManager()
            .AddPasswordValidator<CommonPasswordValidator>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<DevUserSeeder>();

        AddMessaging(services, configuration);

        return services;
    }

    /// <summary>Kafka producer, outbox relay and notification consumer. Skipped when Kafka:Enabled is false (tests).</summary>
    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(KafkaOptions.SectionName);
        services.Configure<KafkaOptions>(section);
        var kafka = section.Get<KafkaOptions>() ?? new KafkaOptions();
        if (!kafka.Enabled)
        {
            return;
        }

        services.AddSingleton(_ => new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true, // broker de-duplicates producer retries
            MessageTimeoutMs = 10_000,
        }).Build());
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<IDeadLetterPublisher, KafkaDeadLetterPublisher>();
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxRelay>();
        services.AddHostedService<NotificationConsumer>();
    }
}
