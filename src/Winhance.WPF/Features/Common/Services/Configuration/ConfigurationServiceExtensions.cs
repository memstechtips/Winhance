using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Services;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Extension methods for registering configuration services with the dependency injection container.
    /// </summary>
    public static class ConfigurationServiceExtensions
    {
        /// <summary>
        /// Adds all configuration services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddConfigurationServices(this IServiceCollection services)
        {
            // Register the main configuration applier service
            services.AddTransient<IConfigurationApplierService, ConfigurationApplierService>();

            // Register the property updater and view model refresher
            services.AddTransient<IConfigurationPropertyUpdater, ConfigurationPropertyUpdater>();
            services.AddTransient<IViewModelRefresher, ViewModelRefresher>();

            // Register all section-specific appliers
            services.AddTransient<ISectionConfigurationApplier, WindowsAppsConfigurationApplier>();
            services.AddTransient<ISectionConfigurationApplier, ExternalAppsConfigurationApplier>();
            services.AddTransient<ISectionConfigurationApplier, CustomizeConfigurationApplier>();

            // Register the new Optimize configuration applier for composition-based ViewModel
            services.AddTransient<
                Winhance.Core.Features.Common.Interfaces.IOptimizeConfigurationApplier,
                OptimizeConfigurationApplier
            >();

            // Register the unified configuration service with all its dependencies available
            services.AddSingleton<IUnifiedConfigurationService, UnifiedConfigurationService>();

            return services;
        }
    }
}
