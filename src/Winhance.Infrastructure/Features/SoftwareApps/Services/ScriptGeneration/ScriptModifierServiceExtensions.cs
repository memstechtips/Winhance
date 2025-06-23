using System;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Extension methods for registering script modifier services with the dependency injection container.
    /// </summary>
    public static class ScriptModifierServiceExtensions
    {
        /// <summary>
        /// Adds script modifier services to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddScriptModifierServices(this IServiceCollection services)
        {
            // Register the specialized modifiers
            services.AddTransient<IPackageScriptModifier, PackageScriptModifier>();
            services.AddTransient<ICapabilityScriptModifier, CapabilityScriptModifier>();
            services.AddTransient<IFeatureScriptModifier, FeatureScriptModifier>();
            services.AddTransient<IRegistryScriptModifier, RegistryScriptModifier>();

            // Composite modifier registration moved to ScriptGenerationServiceExtensions.cs
            // to avoid duplicate registrations

            return services;
        }
    }
}
