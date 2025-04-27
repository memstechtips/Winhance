using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.Common.ScriptGeneration
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
        public static IServiceCollection AddScriptGenerationServices(this IServiceCollection services)
        {
            // Register script template provider
            services.AddSingleton<IScriptTemplateProvider, PowerShellScriptTemplateProvider>();
            
            // Register script builder service
            services.AddSingleton<IScriptBuilderService, PowerShellScriptBuilderService>();
            
            // Register script factory
            services.AddSingleton<IScriptFactory, PowerShellScriptFactory>();
            
            // Register script generation service
            services.AddSingleton<IScriptGenerationService, ScriptGenerationService>();
            
            // Register script content modifier
            services.AddSingleton<IScriptContentModifier, ScriptContentModifier>();
            
            // Register composite script content modifier
            services.AddSingleton<CompositeScriptContentModifier>();
            
            // Register specialized script modifiers
            services.AddSingleton<IPackageScriptModifier, PackageScriptModifier>();
            services.AddSingleton<ICapabilityScriptModifier, CapabilityScriptModifier>();
            services.AddSingleton<IFeatureScriptModifier, FeatureScriptModifier>();
            services.AddSingleton<IRegistryScriptModifier, RegistryScriptModifier>();
            
            // Register scheduled task service
            services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();
            
            // Register script update service
            services.AddSingleton<IScriptUpdateService, ScriptUpdateService>();
            
            return services;
        }
    }
}