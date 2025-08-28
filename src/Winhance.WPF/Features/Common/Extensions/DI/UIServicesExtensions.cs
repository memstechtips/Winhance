using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.UI.Interfaces;
using Winhance.Infrastructure.Features.UI.Services;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.Services.Configuration;
using Winhance.WPF.Features.SoftwareApps.Services;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    /// <summary>
    /// Extension methods for registering UI layer services.
    /// These services handle user interface concerns and coordinate between
    /// the presentation layer and domain services.
    /// </summary>
    public static class UIServicesExtensions
    {
        /// <summary>
        /// Registers UI layer services for the Winhance application.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddUIServices(this IServiceCollection services)
        {
            return services
                .AddUIInfrastructureServices()
                .AddUICoordinationServices()
                .AddConfigurationServices()
                .AddDialogServices()
                .CompleteSystemServicesRegistration(); // Complete system services after UI dependencies are available
        }

        /// <summary>
        /// Registers core UI infrastructure services.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddUIInfrastructureServices(
            this IServiceCollection services
        )
        {
            // Theme Management (Singleton - Application-wide theming)
            services.AddSingleton<IThemeManager>(provider => new ThemeManager(
                provider.GetRequiredService<INavigationService>()
            ));

            // Application Close Service (Singleton - Application-wide resource)
            services.AddSingleton<IApplicationCloseService, ApplicationCloseService>();

            // Window Services (Singleton - Application-wide resources)
            services.AddSingleton<WindowInitializationService>();

            // Notification Service (Singleton - Application-wide notifications)
            services.AddSingleton<IWinhanceNotificationService, WinhanceNotificationService>();

            // User Preferences Service (Singleton - Application-wide settings)
            services.AddSingleton<UserPreferencesService>(provider => new UserPreferencesService(
                provider.GetRequiredService<ILogService>()
            ));

            // UAC Settings Service (Singleton - System-wide settings)
            services.AddSingleton<IUacSettingsService>(provider => new UacSettingsService(
                provider.GetRequiredService<UserPreferencesService>(),
                provider.GetRequiredService<ILogService>()
            ));

            // Design-time Data Service (Singleton - Development support)
            services.AddSingleton<IDesignTimeDataService, DesignTimeDataService>();

            return services;
        }

        /// <summary>
        /// Registers UI coordination services.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddUICoordinationServices(this IServiceCollection services)
        {
            // Settings Confirmation Service (Transient - Per-operation)
            services.AddTransient<ISettingsConfirmationService, SettingsConfirmationService>();


            // Event Handlers (Singleton - Event subscribers should be long-lived)
            services.AddSingleton<Infrastructure.Features.Common.EventHandlers.TooltipRefreshEventHandler>();

            // Removed duplicate SettingTooltipDataService - using ITooltipDataService instead

            // Application Layer Services
            services.AddScoped<ISettingApplicationService>(sp => 
                new Infrastructure.Features.Common.Services.SettingApplicationService(
                    sp.GetRequiredService<IDomainServiceRouter>(),
                    sp.GetRequiredService<ILogService>(),
                    sp.GetRequiredService<IRecommendedSettingsService>(),
                    sp.GetRequiredService<IDependencyManager>(),
                    sp.GetRequiredService<IGlobalSettingsRegistry>(),
                    sp.GetRequiredService<IEventBus>()
                ));
            services.AddTransient<IPropertyUpdater, PropertyUpdater>();
            // TODO: Fix ambiguous IOptimizeConfigurationApplier reference
            // services.AddTransient<IOptimizeConfigurationApplier, OptimizeConfigurationApplier>();

            return services;
        }

        /// <summary>
        /// Registers dialog and UI interaction services.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddDialogServices(this IServiceCollection services)
        {
            // Dialog Services (Transient - Per-dialog instance)
            services.AddTransient<IDialogService, DialogService>();
            services.AddSingleton<SoftwareAppsDialogService>();

            return services;
        }

        /// <summary>
        /// Registers navigation services with view mappings.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddNavigationServices(this IServiceCollection services)
        {
            // Navigation service registration with view mappings
            // This is typically done in InfrastructureServicesExtensions but can be customized here

            return services;
        }
    }
}
