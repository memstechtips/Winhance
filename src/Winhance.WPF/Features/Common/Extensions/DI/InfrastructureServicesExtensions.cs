using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Infrastructure.Features.Common.Events;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.WindowsRegistry;
using Winhance.WPF.Features.Common.Interfaces;

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
            services.AddSingleton<IWindowsRegistryService, WindowsRegistryService>();

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

            // Tooltip Services (Singleton - Application-wide tooltip management)
            services.AddSingleton<ITooltipDataService, TooltipDataService>();

            // System Settings Discovery (Singleton - Coordinates between services)
            services.AddSingleton<ISystemSettingsDiscoveryService>(provider =>
                new SystemSettingsDiscoveryService(
                    provider.GetRequiredService<IWindowsRegistryService>(),
                    provider.GetRequiredService<ICommandService>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IWindowsCompatibilityFilter>(),
                    provider.GetRequiredService<IComboBoxResolver>()
                )
            );

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


            // ComboBox Services (Scoped - Per-operation state)
            services.AddScoped<IComboBoxSetupService, ComboBoxSetupService>();
            services.AddScoped<IComboBoxResolver, ComboBoxResolver>();

            // RecommendedSettings Service (Singleton - Application-wide recommendation logic)
            services.AddSingleton<IRecommendedSettingsService>(provider =>
                new Infrastructure.Features.Common.Services.RecommendedSettingsService(
                    provider.GetRequiredService<IDomainServiceRouter>(),
                    provider.GetRequiredService<ISystemServices>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IEventBus>()
                ));

            // Settings Loading Service (Scoped - Per-feature loading operation)
            services.AddScoped<ISettingsLoadingService>(
                provider => new Winhance.WPF.Features.Common.Services.SettingsLoadingService(
                    provider.GetRequiredService<ISettingApplicationService>(),
                    provider.GetRequiredService<ITaskProgressService>(),
                    provider.GetRequiredService<IEventBus>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IComboBoxSetupService>(),
                    provider.GetRequiredService<IDomainServiceRouter>(),
                    provider.GetRequiredService<ISettingsConfirmationService>(),
                    provider.GetRequiredService<IGlobalSettingsRegistry>()
                )
            );

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
            // System Services
            services.AddSingleton<ISystemServices>(
                provider => new Winhance.Infrastructure.Features.Common.Services.WindowsSystemService(
                    provider.GetRequiredService<IWindowsRegistryService>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IInternetConnectivityService>(),
                    null // Intentionally not passing IDomainService to break circular dependency
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
        /// Registers simplified controlHandler services.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddcontrolHandlerServices(this IServiceCollection services)
        {
            // System Setting controlHandler (Scoped - Coordinates registry operations)
            services.AddScoped<SettingControlHandler>(sp => new SettingControlHandler(
                sp.GetRequiredService<IWindowsRegistryService>(),
                sp.GetRequiredService<IComboBoxResolver>(),
                sp.GetRequiredService<ILogService>()
            ));

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
