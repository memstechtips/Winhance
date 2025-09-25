using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Events;
using Winhance.Infrastructure.Features.Common.Services;
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
            services.AddSingleton<IPowerCfgQueryService>(provider =>
                new PowerCfgQueryService(
                    provider.GetRequiredService<ICommandService>(),
                    provider.GetRequiredService<ILogService>()
                )
            );
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

            // Hardware Detection Service (Singleton - System resource)
            services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();


            // PowerShell Services (Singleton - System resources)
            services.AddSingleton<IPowerShellExecutionService, PowerShellExecutionService>();

            // Task Progress Service (Singleton - Application-wide progress tracking)
            services.AddSingleton<ITaskProgressService, TaskProgressService>();

            // Search Services (Singleton - Can be shared)
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
                    provider.GetRequiredService<IPowerCfgQueryService>(),
                    provider.GetRequiredService<IPowerSettingsValidationService>()
                )
            );

            // Scheduled Task Service (Singleton - System-wide resource)
            services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();

            // Navigation Services (Singleton - Application-wide navigation)
            services.AddSingleton<INavigationService>(provider =>
            {
                var navigationService = new FrameNavigationService(
                    provider,
                    provider.GetRequiredService<IParameterSerializer>(),
                    provider.GetRequiredService<ILogService>()
                );

                // Register view mappings
                RegisterViewMappings(navigationService);
                return navigationService;
            });
            services.AddSingleton<IParameterSerializer, JsonParameterSerializer>();


            // ComboBox Services (Scoped - Per-operation state)
            services.AddScoped<IComboBoxSetupService, ComboBoxSetupService>();
            services.AddScoped<IComboBoxResolver, ComboBoxResolver>();
            services.AddScoped<IPowerPlanComboBoxService, PowerPlanComboBoxService>();

            // RecommendedSettings Service (Singleton - Application-wide recommendation logic)
            services.AddSingleton<IRecommendedSettingsService>(provider =>
                new Infrastructure.Features.Common.Services.RecommendedSettingsService(
                    provider.GetRequiredService<IDomainServiceRouter>(),
                    provider.GetRequiredService<IWindowsRegistryService>(),
                    provider.GetRequiredService<IComboBoxResolver>(),
                    provider.GetRequiredService<IWindowsVersionService>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IEventBus>()
                ));

            // Settings Loading Service (Scoped - Per-feature loading operation)
            services.AddScoped<ISettingsLoadingService>(
                provider => new Winhance.WPF.Features.Common.Services.SettingsLoadingService(
                    provider.GetRequiredService<ISystemSettingsDiscoveryService>(),
                    provider.GetRequiredService<ISettingApplicationService>(),
                    provider.GetRequiredService<IEventBus>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IComboBoxSetupService>(),
                    provider.GetRequiredService<IDomainServiceRouter>(),
                    provider.GetRequiredService<ISettingsConfirmationService>(),
                    provider.GetRequiredService<IGlobalSettingsRegistry>(),
                    provider.GetRequiredService<IInitializationService>(),
                    provider.GetRequiredService<IPowerPlanComboBoxService>(),
                    provider.GetRequiredService<IComboBoxResolver>()
                )
            );

            // Windows Compatibility Filter (Transient - Stateless)
            services.AddTransient<IWindowsCompatibilityFilter, WindowsCompatibilityFilter>();
            services.AddTransient<IHardwareCompatibilityFilter, HardwareCompatibilityFilter>();
            services.AddSingleton<IPowerSettingsValidationService, PowerSettingsValidationService>();

            // Compatible Settings Registry (Singleton - Caches filtering decisions)
            services.AddSingleton<ICompatibleSettingsRegistry, CompatibleSettingsRegistry>();

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
            // New focused system services
            services.AddSingleton<IWindowsVersionService, WindowsVersionService>();
            services.AddSingleton<IWindowsUIManagementService, WindowsUIManagementService>();
            services.AddSingleton<IWindowsThemeQueryService, WindowsThemeQueryService>();

            // Setting Application Service (Scoped - Per-operation pipeline)
            services.AddScoped<ISettingApplicationService, SettingApplicationService>();

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
