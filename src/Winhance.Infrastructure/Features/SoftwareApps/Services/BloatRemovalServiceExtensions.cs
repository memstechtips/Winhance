using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Extension methods for registering bloat removal services.
    /// </summary>
    public static class BloatRemovalServiceExtensions
    {
        /// <summary>
        /// Adds bloat removal services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddBloatRemovalServices(this IServiceCollection services)
        {
            // First, add all the script generation services
            services.AddScriptGenerationServices();

            // BloatRemovalScriptService is now registered in ScriptGenerationServiceExtensions
            // to avoid circular dependencies

            // Register BloatRemovalCoordinatorService
            services.AddSingleton<IBloatRemovalCoordinatorService>(sp =>
            {
                return new BloatRemovalCoordinatorService(
                    sp.GetRequiredService<ILogService>(),
                    sp.GetRequiredService<IBloatRemovalScriptService>(),
                    sp.GetRequiredService<IAppRemovalService>(),
                    sp.GetRequiredService<ICapabilityRemovalService>(),
                    sp.GetRequiredService<IFeatureRemovalService>()
                );
            });

            return services;
        }
    }
}
