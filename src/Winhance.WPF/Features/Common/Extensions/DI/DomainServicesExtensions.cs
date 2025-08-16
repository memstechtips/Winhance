using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Descriptors;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Infrastructure.Features.Optimize.Descriptors;
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
                .AddCustomizationServices()
                .AddOptimizationServices()
                .AddSoftwareAppServices()
                .AddDomainServiceRegistry();
        }

        /// <summary>
        /// Registers customization domain services.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddCustomizationServices(this IServiceCollection services)
        {
            // Wallpaper Service (Singleton - System resource)
            services.AddSingleton<IWallpaperService, WallpaperService>();

            // Customization Domain Services (Scoped - DDD pattern)
            services.AddScoped<IWindowsThemeService>(sp => new WindowsThemeService(
                sp.GetRequiredService<IWallpaperService>(),
                sp.GetRequiredService<ISystemServices>(),
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddScoped<IDomainService>(sp => sp.GetRequiredService<IWindowsThemeService>());

            services.AddScoped<IStartMenuService>(sp => new StartMenuService(
                sp.GetRequiredService<IScheduledTaskService>(),
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<ISystemServices>()
            ));
            services.AddScoped<IDomainService>(sp => sp.GetRequiredService<IStartMenuService>());

            services.AddScoped<ITaskbarService>(sp => new TaskbarService(
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<ICommandService>()
            ));
            services.AddScoped<IDomainService>(sp => sp.GetRequiredService<ITaskbarService>());

            services.AddScoped<IExplorerCustomizationService>(
                sp => new ExplorerCustomizationService(
                    sp.GetRequiredService<SystemSettingOrchestrator>(),
                    sp.GetRequiredService<ILogService>(),
                    sp.GetRequiredService<ICommandService>(),
                    sp.GetRequiredService<IRegistryService>()
                )
            );
            services.AddScoped<IDomainService>(sp =>
                sp.GetRequiredService<IExplorerCustomizationService>()
            );

            // Feature descriptors are registered with IFeatureDiscoveryService in InfrastructureServices

            return services;
        }

        /// <summary>
        /// Registers optimization domain services.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddOptimizationServices(this IServiceCollection services)
        {
            // Optimization Domain Services (Scoped - DDD pattern)
            services.AddScoped<IPowerService>(sp => new PowerService(
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<IComboBoxValueResolver>(),
                sp.GetRequiredService<ICommandService>(),
                sp.GetRequiredService<IBatteryService>(),
                sp.GetRequiredService<IPowerShellExecutionService>()
            ));
            services.AddScoped<IDomainService>(sp => sp.GetRequiredService<IPowerService>());

            services.AddScoped<IPrivacyService>(sp => new PrivacyService(
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddScoped<IDomainService>(sp => sp.GetRequiredService<IPrivacyService>());

            services.AddScoped<ISecurityService>(sp => new SecurityService(
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<IComboBoxValueResolver>()
            ));
            services.AddScoped<IDomainService>(sp => sp.GetRequiredService<ISecurityService>());

            services.AddScoped<IGamingPerformanceService>(sp => new GamingPerformanceService(
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddScoped<IDomainService>(sp =>
                sp.GetRequiredService<IGamingPerformanceService>()
            );

            services.AddScoped<INotificationService>(sp => new NotificationService(
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddScoped<IDomainService>(sp => sp.GetRequiredService<INotificationService>());

            services.AddScoped<ISoundService>(sp => new SoundService(
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddScoped<IDomainService>(sp => sp.GetRequiredService<ISoundService>());

            services.AddScoped<IUpdateService>(sp => new UpdateService(
                sp.GetRequiredService<SystemSettingOrchestrator>(),
                sp.GetRequiredService<ILogService>()
            ));
            services.AddScoped<IDomainService>(sp => sp.GetRequiredService<IUpdateService>());

            services.AddScoped<IExplorerOptimizationService>(
                sp => new Winhance.Infrastructure.Features.Optimize.Services.ExplorerOptimizationService(
                    sp.GetRequiredService<SystemSettingOrchestrator>(),
                    sp.GetRequiredService<ILogService>()
                )
            );
            services.AddScoped<IDomainService>(sp =>
                sp.GetRequiredService<IExplorerOptimizationService>()
            );

            // Feature descriptors are registered with IFeatureDiscoveryService in InfrastructureServices

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
        public static IServiceCollection AddDomainServiceRegistry(this IServiceCollection services)
        {
            // Domain Service Registry (Scoped - Per-operation service discovery)
            services.AddScoped<
                IDomainServiceRegistry,
                Infrastructure.Features.Common.Services.DomainServiceRegistry
            >();

            // Domain Dependency Service (Singleton - Clean Architecture enforcement)
            services.AddSingleton<IDomainDependencyService, DomainDependencyService>();

            return services;
        }
    }
}
