using Microsoft.Extensions.DependencyInjection;

namespace Winhance.UI.Features.Common.Extensions.DI;

/// <summary>
/// Extension methods for registering core services and abstractions.
/// </summary>
public static class CoreServicesExtensions
{
    /// <summary>
    /// Registers core services and abstractions for the Winhance application.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Core services are primarily interfaces and abstractions
        // Concrete implementations are registered in Infrastructure layer

        return services;
    }
}
