using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Winhance.UI.Features.Common.Extensions.DI;

/// <summary>
/// The composition root for the Winhance WinUI 3 application.
/// Orchestrates the registration of all services following Clean Architecture principles.
/// </summary>
public static class CompositionRoot
{
    /// <summary>
    /// Configures all services for the Winhance application.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The configured service collection for method chaining</returns>
    public static IServiceCollection ConfigureWinhanceServices(this IServiceCollection services)
    {
        // Register services in dependency order
        services
            .AddCoreServices()           // Core abstractions and interfaces
            .AddInfrastructureServices() // Infrastructure implementations
            .AddDomainServices()         // Domain services (Customization, Optimization, SoftwareApps)
            .AddUIServices();            // UI-specific services (ThemeService, etc.)

        return services;
    }

    /// <summary>
    /// Creates and configures a host builder with the Winhance service configuration.
    /// </summary>
    /// <returns>Configured host builder</returns>
    public static IHostBuilder CreateWinhanceHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.ConfigureWinhanceServices();
            });
    }
}
