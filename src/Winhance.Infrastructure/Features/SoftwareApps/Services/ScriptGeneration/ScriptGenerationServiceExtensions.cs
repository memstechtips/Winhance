using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Extension methods for registering script generation services.
    /// </summary>
    public static class ScriptGenerationServiceExtensions
    {
        /// <summary>
        /// Adds script generation services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddScriptGenerationServices(
            this IServiceCollection services
        )
        {
            // Register script template provider
            services.AddSingleton<
                IBloatRemovalScriptTemplateProvider,
                BloatRemovalScriptTemplateProvider
            >();

            // Register script builder service
            services.AddSingleton<
                IBloatRemovalScriptBuilderService,
                BloatRemovalScriptBuilderService
            >();

            // Register script factory
            services.AddSingleton<IBloatRemovalScriptFactory, BloatRemovalScriptFactory>();

            // Register bloat removal script saving service
            services.AddSingleton<IBloatRemovalScriptSavingService, BloatRemovalScriptSavingService>();

            // Register bloat removal script generation service
            services.AddSingleton<IBloatRemovalScriptGenerationService, BloatRemovalScriptGenerationService>();

            // Register bloat removal script service
            services.AddSingleton<IBloatRemovalScriptService, BloatRemovalScriptService>();

            // Register script content modifier
            services.AddSingleton<
                IBloatRemovalScriptContentModifier,
                BloatRemovalScriptContentModifier
            >();

            // Register composite script content modifier
            services.AddSingleton<CompositeBloatRemovalScriptModifier>();

            // Register specialized script modifiers
            services.AddSingleton<IPackageScriptModifier, PackageScriptModifier>();
            services.AddSingleton<ICapabilityScriptModifier, CapabilityScriptModifier>();
            services.AddSingleton<IFeatureScriptModifier, FeatureScriptModifier>();
            services.AddSingleton<IRegistryScriptModifier, RegistryScriptModifier>();

            // Register scheduled task service
            services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();

            // Register script update service
            services.AddSingleton<IScriptUpdateService>(sp =>
            {
                return new ScriptUpdateService(
                    sp.GetRequiredService<ILogService>(),
                    sp.GetRequiredService<IAppDiscoveryService>(),
                    sp.GetRequiredService<IBloatRemovalScriptContentModifier>(),
                    sp.GetRequiredService<IBloatRemovalScriptTemplateProvider>()
                );
            });

            return services;
        }
    }
}
