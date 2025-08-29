using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration;
using Winhance.WPF.Features.Common.Services.Configuration;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    /// <summary>
    /// The composition root for the Winhance application.
    /// This class is responsible for orchestrating the registration of all services
    /// while maintaining proper separation of concerns and adherence to SOLID principles.
    /// Located in the UI layer as per Clean Architecture principles.
    /// </summary>
    public static class CompositionRoot
    {
        /// <summary>
        /// Configures all services for the Winhance application.
        /// This method serves as the single entry point for dependency injection configuration.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The configured service collection for method chaining</returns>
        public static IServiceCollection ConfigureWinhanceServices(this IServiceCollection services)
        {
            try
            {
                // Register services in dependency order to avoid issues
                services
                    .AddCoreServices() // Core abstractions and interfaces
                    .AddInfrastructureServices() // Infrastructure implementations
                    .AddcontrolHandlerServices() // Simplified orchestrator services
                    .AddDomainServices() // Domain services following DDD
                                         // Add existing script generation and bloat removal services
                    .AddScriptGenerationServices() // From Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
                    .AddBloatRemovalServices() // From Infrastructure.Features.SoftwareApps.Services
                    .AddConfigurationServices() // From WPF.Features.Common.Services.Configuration
                    .AddUIServices() // UI layer services
                    .AddViewModels() // ViewModels with proper lifetimes
                    .AddViews(); // View registrations

                return services;
            }
            catch (Exception ex)
            {
                // Log configuration error and rethrow with context
                throw new InvalidOperationException(
                    "Failed to configure Winhance services. See inner exception for details.",
                    ex
                );
            }
        }

        /// <summary>
        /// Creates and configures a host builder with the Winhance service configuration.
        /// </summary>
        /// <returns>Configured host builder</returns>
        public static IHostBuilder CreateWinhanceHost()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices(
                    (context, services) =>
                    {
                        services.ConfigureWinhanceServices();
                    }
                );
        }

        /// <summary>
        /// Gets service registration statistics for monitoring and diagnostics.
        /// </summary>
        /// <param name="services">The service collection to analyze</param>
        /// <returns>Registration statistics</returns>
        public static ServiceRegistrationStatistics GetRegistrationStatistics(
            IServiceCollection services
        )
        {
            return new ServiceRegistrationStatistics
            {
                TotalRegistrations = services.Count,
                SingletonCount = services.Count(s => s.Lifetime == ServiceLifetime.Singleton),
                ScopedCount = services.Count(s => s.Lifetime == ServiceLifetime.Scoped),
                TransientCount = services.Count(s => s.Lifetime == ServiceLifetime.Transient),
                UniqueServiceTypes = services.Select(s => s.ServiceType).Distinct().Count(),
                DuplicateRegistrations = services
                    .GroupBy(s => s.ServiceType)
                    .Where(g => g.Count() > 1)
                    .Select(g => new DuplicateRegistration
                    {
                        ServiceType = g.Key,
                        RegistrationCount = g.Count(),
                    })
                    .ToList(),
            };
        }
    }

    /// <summary>
    /// Statistics about service registrations for monitoring and diagnostics.
    /// </summary>
    public class ServiceRegistrationStatistics
    {
        public int TotalRegistrations { get; set; }
        public int SingletonCount { get; set; }
        public int ScopedCount { get; set; }
        public int TransientCount { get; set; }
        public int UniqueServiceTypes { get; set; }
        public List<DuplicateRegistration> DuplicateRegistrations { get; set; } = new();

        public bool HasDuplicates => DuplicateRegistrations.Any();

        public override string ToString()
        {
            return $"Total: {TotalRegistrations}, "
                + $"Unique: {UniqueServiceTypes}, "
                + $"Singleton: {SingletonCount}, "
                + $"Scoped: {ScopedCount}, "
                + $"Transient: {TransientCount}, "
                + $"Duplicates: {DuplicateRegistrations.Count}";
        }
    }

    /// <summary>
    /// Represents a duplicate service registration.
    /// </summary>
    public class DuplicateRegistration
    {
        public Type ServiceType { get; set; } = null!;
        public int RegistrationCount { get; set; }

        public override string ToString()
        {
            return $"{ServiceType.Name}: {RegistrationCount} registrations";
        }
    }
}
