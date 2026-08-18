using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Winhance.Infrastructure.Extensions.DI;

namespace Winhance.UI.Features.Common.Extensions.DI;

public static class CompositionRoot
{
    public static IServiceCollection ConfigureWinhanceServices(this IServiceCollection services)
    {
        // Register services in dependency order
        services
            .AddInfrastructureServices() // Infrastructure implementations
            .AddSettingServices()        // Setting services (Customization, Optimization, SoftwareApps)
            .AddUIServices();            // UI-specific services (ThemeService, etc.)

        return services;
    }

    public static IHostBuilder CreateWinhanceHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.ConfigureWinhanceServices();
            });
    }
}
