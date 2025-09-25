using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Infrastructure.Features.Optimize.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Core.Features.Common.Events;


namespace Winhance.WPF.Features.Common.Extensions.DI
{
    public static class DomainServicesExtensions
    {
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
                sp.GetRequiredService<IWindowsVersionService>(),
                sp.GetRequiredService<IWindowsUIManagementService>(),
                sp.GetRequiredService<IWindowsRegistryService>(),
                sp.GetRequiredService<ILogService>()
            ));
            // Register as IDomainService for registry
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<WindowsThemeService>());

            // Register StartMenuService
            services.AddSingleton<StartMenuService>(sp => new StartMenuService(
                sp.GetRequiredService<IScheduledTaskService>(),
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<ICompatibleSettingsRegistry>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<StartMenuService>());

            // Register TaskbarService
            services.AddSingleton<TaskbarService>(sp => new TaskbarService(
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<IWindowsRegistryService>(),
                sp.GetRequiredService<ICompatibleSettingsRegistry>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<TaskbarService>());

            // Register ExplorerCustomizationService
            services.AddSingleton<ExplorerCustomizationService>(sp => new ExplorerCustomizationService(
                sp.GetRequiredService<ILogService>()
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
                sp.GetRequiredService<ILogService>(),
                sp.GetRequiredService<ICommandService>(),
                sp.GetRequiredService<IPowerCfgQueryService>(),
                sp.GetRequiredService<ICompatibleSettingsRegistry>(),
                sp.GetRequiredService<IEventBus>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<PowerService>());
            // Register as IPowerService for ViewModels that still use direct injection
            services.AddSingleton<IPowerService>(sp => sp.GetRequiredService<PowerService>());

            // Register PrivacyService
            services.AddSingleton<PrivacyService>(sp => new PrivacyService(
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<PrivacyService>());

            // Register SecurityService
            services.AddSingleton<SecurityService>(sp => new SecurityService(
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<SecurityService>());

            // Register GamingPerformanceService
            services.AddSingleton<GamingPerformanceService>(sp => new GamingPerformanceService(
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<GamingPerformanceService>());

            // Register NotificationService
            services.AddSingleton<NotificationService>(sp => new NotificationService(
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<NotificationService>());

            // Register SoundService
            services.AddSingleton<SoundService>(sp => new SoundService(
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<SoundService>());

            // Register UpdateService
            services.AddSingleton<UpdateService>(sp => new UpdateService(
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<UpdateService>());

            // Register ExplorerOptimizationService
            services.AddSingleton<ExplorerOptimizationService>(sp => new ExplorerOptimizationService(
                sp.GetRequiredService<ILogService>()
            ));
            services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<ExplorerOptimizationService>());

            return services;
        }

        public static IServiceCollection AddSoftwareAppServices(this IServiceCollection services)
        {
            // New Domain Services (Scoped - Business logic)
            services.AddScoped<IWindowsAppsService, WindowsAppsService>();
            services.AddScoped<IExternalAppsService, ExternalAppsService>();
            services.AddScoped<IAppOperationService, AppOperationService>();

            // App Status Discovery Service (Singleton - Expensive operation)
            services.AddSingleton<IAppStatusDiscoveryService, AppStatusDiscoveryService>();

            // App Services (Scoped - Business logic)
            services.AddScoped<IAppLoadingService, AppLoadingService>();

            // Simplified WinGet Service
            services.AddSingleton<IWinGetService, WinGetService>();

            // Legacy Capability and Optional Feature Services
            services.AddSingleton<ILegacyCapabilityService>(provider => new LegacyCapabilityService(
                provider.GetRequiredService<ILogService>(),
                provider.GetRequiredService<IPowerShellExecutionService>()
            ));
            services.AddSingleton<IOptionalFeatureService>(provider => new OptionalFeatureService(
                provider.GetRequiredService<ILogService>(),
                provider.GetRequiredService<IPowerShellExecutionService>()
            ));

            // Script Detection Service (Singleton - Expensive operation)
            services.AddSingleton<
                IScriptDetectionService,
                Infrastructure.Features.SoftwareApps.Services.ScriptDetectionService
            >();

            // App Removal Service (Singleton - Simplified removal logic)
            services.AddSingleton<IBloatRemovalService, BloatRemovalService>();

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

            // Initialization Service (Singleton - Global state tracking)
            services.AddSingleton<IInitializationService, InitializationService>();

            return services;
        }
    }
}
