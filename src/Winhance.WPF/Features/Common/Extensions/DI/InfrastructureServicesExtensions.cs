using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Events;
using Winhance.Infrastructure.Features.Common.Registry;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    /// <summary>
    /// Extension methods for registering infrastructure services.
    /// This layer contains concrete implementations of core abstractions
    /// and system-level services.
    /// </summary>
    public static class InfrastructureServicesExtensions
    {
        /// <summary>
        /// Registers infrastructure services for the Winhance application.
        /// These are concrete implementations of core interfaces and system services.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // Core Infrastructure Services (Singleton - Cross-cutting concerns)
            services.AddSingleton<ILogService, Winhance.Core.Features.Common.Services.LogService>();
            services.AddSingleton<IRegistryService, RegistryService>();

            // Register segregated registry interfaces for ISP compliance
            services.AddSingleton<IRegistryReader>(sp => sp.GetRequiredService<IRegistryService>());
            services.AddSingleton<IRegistryWriter>(sp => sp.GetRequiredService<IRegistryService>());
            services.AddSingleton<IRegistryStatus>(sp => sp.GetRequiredService<IRegistryService>());

            services.AddSingleton<ICommandService, CommandService>();
            services.AddSingleton<
                IDependencyManager,
                Winhance.Core.Features.Common.Services.DependencyManager
            >();
            services.AddSingleton<
                IGlobalSettingsRegistry,
                Winhance.Core.Features.Common.Services.GlobalSettingsRegistry
            >();
            services.AddSingleton<
                ISettingsRegistry,
                Winhance.Core.Features.Common.Services.SettingsRegistry
            >();

            // Event Bus (Singleton - Application-wide communication)
            services.AddSingleton<IEventBus, EventBus>();

            // Internet Connectivity Service (Singleton - System resource)
            services.AddSingleton<IInternetConnectivityService>(
                provider => new InternetConnectivityService(
                    provider.GetRequiredService<ILogService>()
                )
            );

            // Battery Service (Singleton - System resource)
            services.AddSingleton<IBatteryService, BatteryService>();

            // System Services will be registered later after UI services due to dependency on IUacSettingsService

            // Detection Services (Singleton - Expensive initialization)
            services.AddSingleton<IPowerShellDetectionService, PowerShellDetectionService>();
            services.AddSingleton<IScriptPathDetectionService, ScriptPathDetectionService>();

            // PowerShell Execution Service will be registered after system services

            // Task Progress Service (Singleton - Application-wide progress tracking)
            services.AddSingleton<ITaskProgressService, TaskProgressService>();

            // Search Services (Singleton - Can be shared)
            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<ISearchTextCoordinationService, SearchTextCoordinationService>();

            // Configuration Services (Singleton - Application-wide configuration)
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IVersionService, VersionService>();

            // System Settings Discovery (Singleton - Coordinates between services)
            services.AddSingleton<
                ISystemSettingsDiscoveryService,
                SystemSettingsDiscoveryService
            >();

            // Scheduled Task Service (Singleton - System-wide resource)
            services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();

            // Navigation Services (Singleton - Application-wide navigation)
            services.AddSingleton<INavigationService>(provider =>
            {
                var navigationService = new FrameNavigationService(
                    provider,
                    provider.GetRequiredService<IParameterSerializer>()
                );

                // Register view mappings
                RegisterViewMappings(navigationService);
                return navigationService;
            });
            services.AddSingleton<IParameterSerializer, JsonParameterSerializer>();

            // Feature Discovery (Singleton - Expensive operation)
            services.AddSingleton<IFeatureDiscoveryService>(provider =>
            {
                var discoveryService = new FeatureDiscoveryService(
                    provider,
                    provider.GetRequiredService<ILogService>()
                );

                // Register Optimization Feature Descriptors
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Optimize.Descriptors.GamingPerformanceFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Optimize.Descriptors.PrivacyFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Optimize.Descriptors.UpdateFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Optimize.Descriptors.PowerFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Optimize.Descriptors.SecurityFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Optimize.Descriptors.ExplorerFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Optimize.Descriptors.NotificationFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Optimize.Descriptors.SoundFeatureDescriptor()
                );

                // Register Customization Feature Descriptors
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Customize.Descriptors.WindowsThemeFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Customize.Descriptors.StartMenuFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Customize.Descriptors.TaskbarFeatureDescriptor()
                );
                discoveryService.RegisterFeature(
                    new Winhance.Infrastructure.Features.Customize.Descriptors.ExplorerCustomizationFeatureDescriptor()
                );

                return discoveryService;
            });

            // ComboBox Services (Scoped - Per-operation state)
            services.AddScoped<IComboBoxDiscoveryService, ComboBoxDiscoveryService>();
            services.AddScoped<IComboBoxValueResolver, GenericComboBoxValueResolver>();

            // Tooltip Data Service (Singleton - Can be shared)
            services.AddSingleton<ITooltipDataService, TooltipDataService>();

            // Windows Compatibility Filter (Transient - Stateless)
            services.AddTransient<IWindowsCompatibilityFilter, WindowsCompatibilityFilter>();

            return services;
        }

        /// <summary>
        /// Completes system services registration after UI services are available.
        /// This method handles services with UI layer dependencies.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection CompleteSystemServicesRegistration(
            this IServiceCollection services
        )
        {
            // System Services (requires IUacSettingsService from UI layer)
            services.AddSingleton<ISystemServices>(
                provider => new Winhance.Infrastructure.Features.Common.Services.WindowsSystemService(
                    provider.GetRequiredService<IRegistryService>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IInternetConnectivityService>(),
                    null, // Intentionally not passing IWindowsThemeService to break circular dependency
                    provider.GetRequiredService<IUacSettingsService>()
                )
            );

            // PowerShell Execution Service (depends on ISystemServices)
            services.AddSingleton<IPowerShellExecutionService>(
                provider => new PowerShellExecutionService(
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<ISystemServices>(),
                    provider.GetRequiredService<IPowerShellDetectionService>()
                )
            );

            return services;
        }

        /// <summary>
        /// Registers strategy pattern services for SOLID compliance.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddStrategyServices(this IServiceCollection services)
        {
            // Strategy Pattern Services (Scoped - Per-operation strategy selection)
            services.AddScoped<
                ISettingApplicationStrategy,
                Winhance.Infrastructure.Features.Common.Services.Strategies.RegistrySettingApplicationStrategy
            >();
            services.AddScoped<
                ISettingApplicationStrategy,
                Winhance.Infrastructure.Features.Common.Services.Strategies.CommandSettingApplicationStrategy
            >();

            // System Setting Orchestrator (Scoped - Coordinates strategies)
            services.AddScoped<SystemSettingOrchestrator>();

            return services;
        }

        /// <summary>
        /// Registers view mappings for navigation service.
        /// </summary>
        /// <param name="navigationService">The navigation service to configure</param>
        private static void RegisterViewMappings(FrameNavigationService navigationService)
        {
            // Software Apps view mappings
            navigationService.RegisterViewMapping(
                "SoftwareApps",
                typeof(Winhance.WPF.Features.SoftwareApps.Views.SoftwareAppsView),
                typeof(Winhance.WPF.Features.SoftwareApps.ViewModels.SoftwareAppsViewModel)
            );

            navigationService.RegisterViewMapping(
                "WindowsApps",
                typeof(Winhance.WPF.Features.SoftwareApps.Views.WindowsAppsView),
                typeof(Winhance.WPF.Features.SoftwareApps.ViewModels.WindowsAppsViewModel)
            );

            navigationService.RegisterViewMapping(
                "ExternalApps",
                typeof(Winhance.WPF.Features.SoftwareApps.Views.ExternalAppsView),
                typeof(Winhance.WPF.Features.SoftwareApps.ViewModels.ExternalAppsViewModel)
            );

            // Optimization view mapping
            navigationService.RegisterViewMapping(
                "Optimize",
                typeof(Winhance.WPF.Features.Optimize.Views.OptimizeView),
                typeof(Winhance.WPF.Features.Optimize.ViewModels.OptimizeViewModel)
            );

            // Customization view mapping
            navigationService.RegisterViewMapping(
                "Customize",
                typeof(Winhance.WPF.Features.Customize.Views.CustomizeView),
                typeof(Winhance.WPF.Features.Customize.ViewModels.CustomizeViewModel)
            );
        }
    }
}
