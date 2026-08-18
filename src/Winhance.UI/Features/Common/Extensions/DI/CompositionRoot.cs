using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Winhance.Infrastructure.Extensions.DI;

namespace Winhance.UI.Features.Common.Extensions.DI;

public static class CompositionRoot
{
    public static IServiceCollection ConfigureWinhanceServices(this IServiceCollection services)
    {
        services
            .AddInfrastructureServices()
            .AddUIServices();

        return services;
    }

    public static IHostBuilder CreateWinhanceHost()
    {
        return Host.CreateDefaultBuilder()
            // The generic host validates the service graph only in the Development environment, which a shipped
            // desktop app never is - so a registration with an unresolvable dependency would surface on first
            // use. Force it on: a broken graph fails at startup, and WinhanceHostSmokeTests fails the gate first.
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .ConfigureServices((context, services) =>
            {
                services.ConfigureWinhanceServices();
            });
    }
}
