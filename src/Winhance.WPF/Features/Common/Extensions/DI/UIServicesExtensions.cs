using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.UI.Interfaces;
using Winhance.Infrastructure.Features.UI.Services;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.Services.Configuration;

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
                .CompleteSystemServicesRegistration();
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
            services.AddSingleton<IWindowManagementService, WindowManagementService>();
            services.AddSingleton<IFlyoutManagementService, FlyoutManagementService>();

            // Notification Service (Singleton - Application-wide notifications)
            services.AddSingleton<IWinhanceNotificationService, WinhanceNotificationService>();

            // User Preferences Service (Singleton - Application-wide settings)
            services.AddSingleton<UserPreferencesService>(provider => new UserPreferencesService(
                provider.GetRequiredService<ILogService>()
            ));


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
                    sp.GetRequiredService<IWindowsRegistryService>(),
                    sp.GetRequiredService<IComboBoxResolver>(),
                    sp.GetRequiredService<ICommandService>(),
                    sp.GetRequiredService<ILogService>(),
                    sp.GetRequiredService<IDependencyManager>(),
                    sp.GetRequiredService<IGlobalSettingsRegistry>(),
                    sp.GetRequiredService<IEventBus>(),
                    sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                    sp.GetRequiredService<IRecommendedSettingsService>()
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
            services.AddTransient<IDialogService, DialogService>();
            services.AddTransient<ISettingsConfirmationService, SettingsConfirmationService>();

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
