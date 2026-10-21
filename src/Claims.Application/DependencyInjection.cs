using Claims.Application.Claims.Workflow;
using Claims.Application.Common.Behaviors;
using Claims.Application.Notifications;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<ClaimWorkflow>();
        services.AddScoped<ClaimEventNotificationHandler>();

        return services;
    }
}
