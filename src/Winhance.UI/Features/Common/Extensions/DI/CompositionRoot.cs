using Microsoft.Extensions.DependencyInjection;
using Winhance.Infrastructure.Extensions.DI;
using Winhance.UI.Helpers;

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

    // A plain container rather than the generic host: nothing here uses its configuration, logging or lifetime,
    // and Host.CreateDefaultBuilder loads about 30 assemblies and watches appsettings.json for them.
    // ValidateOnBuild makes a registration with an unresolvable dependency fail at startup, not on first use, and
    // WinhanceHostSmokeTests fails the gate first.
    public static ServiceProvider BuildWinhanceServices(LaunchOptions? launch = null)
    {
        var services = new ServiceCollection()
            .ConfigureWinhanceServices()
            .AddSingleton(launch ?? LaunchOptions.None);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
