using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Infrastructure.Features.Optimize.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Implementations;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    /// <summary>
    /// Extension methods for registering domain services.
    /// Domain services encapsulate business logic and follow Domain-Driven Design principles.
    /// They are registered with Scoped lifetime to maintain proper boundaries.
    /// </summary>
    public static class DomainServicesExtensions
    {
        /// <summary>
        /// Registers all domain services for the Winhance application.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddDomainServices(this IServiceCollection services)
        {
            return services
                .AddCustomizationDomainServices()
                .AddOptimizationDomainServices()
                .AddSoftwareAppServices()
                .AddDomainServiceRouter();
        }

        public static IServiceCollection AddCustomizationDomainServices(
            this IServiceCollection services
        )
        {
            // Register WallpaperService (required by WindowsThemeService)
            services.AddSingleton<IWallpaperService, WallpaperService>();

            // Register WindowsThemeService
            services.AddSingleton<WindowsThemeService>(sp => new WindowsThemeService(
                sp.GetRequiredService<IWallpaperService>(),
                sp.GetRequiredService<ISystemServices>(),
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>()
            ));
            // Register as IDomainService for registry
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<WindowsThemeService>());

            // Register StartMenuService
            services.AddSingleton<StartMenuService>(sp => new StartMenuService(
                sp.GetRequiredService<IScheduledTaskService>(),
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<ISystemServices>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<StartMenuService>());

            // Register TaskbarService
            services.AddSingleton<TaskbarService>(sp => new TaskbarService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<ICommandService>(),
                sp.GetRequiredService<IWindowsRegistryService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<TaskbarService>());

            // Register ExplorerCustomizationService
            services.AddSingleton<ExplorerCustomizationService>(sp => new ExplorerCustomizationService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<ICommandService>(),
                sp.GetRequiredService<IWindowsRegistryService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<ExplorerCustomizationService>());

            return services;
        }

        public static IServiceCollection AddOptimizationDomainServices(
            this IServiceCollection services
        )
        {
            // Register PowerService
            services.AddSingleton<PowerService>(sp => new PowerService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<IComboBoxResolver>(),
                sp.GetRequiredService<ICommandService>(),
                sp.GetRequiredService<IBatteryService>(),
                sp.GetRequiredService<IPowerShellExecutionService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<PowerService>());
            // Register as IPowerService for ViewModels that still use direct injection
            services.AddSingleton<IPowerService>(sp => sp.GetRequiredService<PowerService>());

            // Register PrivacyService
            services.AddSingleton<PrivacyService>(sp => new PrivacyService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<PrivacyService>());

            // Register SecurityService
            services.AddSingleton<SecurityService>(sp => new SecurityService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<SecurityService>());

            // Register GamingPerformanceService
            services.AddSingleton<GamingPerformanceService>(sp => new GamingPerformanceService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<GamingPerformanceService>());

            // Register NotificationService
            services.AddSingleton<NotificationService>(sp => new NotificationService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<NotificationService>());

            // Register SoundService
            services.AddSingleton<SoundService>(sp => new SoundService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<SoundService>());

            // Register UpdateService
            services.AddSingleton<UpdateService>(sp => new UpdateService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<UpdateService>());

            // Register ExplorerOptimizationService
            services.AddSingleton<ExplorerOptimizationService>(sp => new ExplorerOptimizationService(
                sp.GetRequiredService<SettingControlHandler>(),
                sp.GetRequiredService<ISystemSettingsDiscoveryService>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<ExplorerOptimizationService>());

            return services;
        }

        /// <summary>
        /// Registers software apps domain services.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddSoftwareAppServices(this IServiceCollection services)
        {
            // App Discovery Service (Singleton - Expensive operation)
            services.AddSingleton<AppDiscoveryService>();
            services.AddSingleton<IAppDiscoveryService>(provider =>
                provider.GetRequiredService<AppDiscoveryService>()
            );

            // App Services (Scoped - Business logic)
            services.AddScoped<IAppService, AppServiceAdapter>();
            services.AddScoped<IPackageManager, PackageManager>();

            // Installation Services (Scoped - Per-operation state)
            services.AddScoped<IWinGetInstaller, WinGetInstaller>();
            services.AddScoped<IWinGetInstallationService, WinGetInstallationServiceAdapter>();
            services.AddScoped<IAppInstallationService, AppInstallationService>();
            services.AddScoped<IAppRemovalService, AppRemovalService>();
            services.AddScoped<ICapabilityInstallationService, CapabilityInstallationService>();
            services.AddScoped<ICapabilityRemovalService, CapabilityRemovalService>();
            services.AddScoped<IFeatureInstallationService, FeatureInstallationService>();
            services.AddScoped<IFeatureRemovalService, FeatureRemovalService>();

            // Coordination Services (Scoped - Orchestrate operations)
            services.AddScoped<
                IAppInstallationCoordinatorService,
                AppInstallationCoordinatorService
            >();
            services.AddScoped<IBloatRemovalCoordinatorService, BloatRemovalCoordinatorService>();

            // Verification Service (Singleton - Stateless)
            services.AddSingleton<IAppVerificationService, AppVerificationService>();

            // Special Handler Service (Singleton - Stateless)
            services.AddSingleton<ISpecialAppHandlerService, SpecialAppHandlerService>();

            // Script Detection Service (Singleton - Expensive operation)
            services.AddSingleton<
                IScriptDetectionService,
                Infrastructure.Features.SoftwareApps.Services.ScriptGeneration.ScriptDetectionService
            >();

            return services;
        }

        /// <summary>
        /// Registers the domain service registry for service discovery.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddDomainServiceRouter(this IServiceCollection services)
        {
            // Domain Service Registry (Scoped - Per-operation service discovery)
            services.AddScoped<
                IDomainServiceRouter,
                Infrastructure.Features.Common.Services.DomainServiceRouter
            >();

            // Domain Dependency Service (Singleton - Clean Architecture enforcement)
            services.AddSingleton<IDomainDependencyService, DomainDependencyService>();

            return services;
        }
    }
}
