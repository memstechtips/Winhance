using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Events;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    public static class InfrastructureServicesExtensions
    {
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
            services.AddSingleton<IGlobalSettingsPreloader, GlobalSettingsPreloader>();

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

            // System Backup Service (Singleton - System protection)
            services.AddSingleton<ISystemBackupService, SystemBackupService>();

            // Script Migration Service (Singleton - One-time migration)
            services.AddSingleton<IScriptMigrationService, ScriptMigrationService>();

            // Task Progress Service (Singleton - Application-wide progress tracking)
            services.AddSingleton<ITaskProgressService, TaskProgressService>();

            // Search Services (Singleton - Can be shared)
            services.AddSingleton<ISearchTextCoordinationService, SearchTextCoordinationService>();

            // Configuration Services (Singleton - Application-wide configuration)
            services.AddSingleton<IVersionService, VersionService>();
            services.AddSingleton<ConfigurationApplicationBridgeService>();

            // Tooltip Services (Singleton - Application-wide tooltip management)
            services.AddSingleton<ITooltipDataService, TooltipDataService>();

            // System Settings Discovery (Singleton - Coordinates between services)
            services.AddSingleton<ISystemSettingsDiscoveryService>(provider =>
                new SystemSettingsDiscoveryService(
                    provider.GetRequiredService<IWindowsRegistryService>(),
                    provider.GetRequiredService<ICommandService>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IPowerCfgQueryService>(),
                    provider.GetRequiredService<IPowerSettingsValidationService>(),
                    provider.GetRequiredService<IDomainServiceRouter>()
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
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IViewPoolService>()
                );

                // Register view mappings
                RegisterViewMappings(navigationService);
                return navigationService;
            });
            services.AddSingleton<IParameterSerializer, JsonParameterSerializer>();


            // ComboBox Services
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
                    provider.GetRequiredService<IComboBoxResolver>(),
                    provider.GetRequiredService<IUserPreferencesService>(),
                    provider.GetRequiredService<IDialogService>(),
                    provider.GetRequiredService<ICompatibleSettingsRegistry>()
                )
            );

            services.AddScoped<IFilterUpdateService, Winhance.WPF.Features.Common.Services.FilterUpdateService>();

            // Windows Compatibility Filter (Transient - Stateless)
            services.AddTransient<IWindowsCompatibilityFilter, WindowsCompatibilityFilter>();
            services.AddTransient<IHardwareCompatibilityFilter, HardwareCompatibilityFilter>();
            services.AddSingleton<IPowerSettingsValidationService, PowerSettingsValidationService>();

            // Compatible Settings Registry (Singleton - Caches filtering decisions)
            services.AddSingleton<ICompatibleSettingsRegistry, CompatibleSettingsRegistry>();

            // HttpClient for external API calls
            services.TryAddSingleton<System.Net.Http.HttpClient>();

            // Advanced Tools Services
            services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IWimUtilService,
                Winhance.Infrastructure.Features.AdvancedTools.Services.WimUtilService>();
            services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.AutounattendScriptBuilder>();

            return services;
        }

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

            // Advanced Tools view mapping
            navigationService.RegisterViewMapping(
                "WimUtil",
                typeof(Winhance.WPF.Features.AdvancedTools.Views.WimUtilView),
                typeof(Winhance.WPF.Features.AdvancedTools.ViewModels.WimUtilViewModel)
            );
        }
    }
}
