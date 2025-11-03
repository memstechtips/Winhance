using Microsoft.Extensions.DependencyInjection;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    /// <summary>
    /// Extension methods for registering core services and abstractions.
    /// This layer contains only interface definitions and core abstractions
    /// with no concrete implementations.
    /// </summary>
    public static class CoreServicesExtensions
    {
        /// <summary>
        /// Registers core services and abstractions for the Winhance application.
        /// This method registers only interfaces and abstractions from the Core layer.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            // Core services are primarily interfaces and abstractions
            // Concrete implementations are registered in Infrastructure layer

            // The Core layer in Clean Architecture should not contain implementations
            // This method serves as documentation of core abstractions
            // and can be used for interface-only registrations if needed

            return services;
        }
    }
}
