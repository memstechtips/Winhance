using System;
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
using Winhance.UI.Features.SoftwareApps.Services;
using Winhance.Core.Features.Common.Events;

namespace Winhance.UI.Features.Common.Extensions.DI;

/// <summary>
/// Extension methods for registering domain services.
/// </summary>
public static class DomainServicesExtensions
{
    /// <summary>
    /// Registers all domain services for the Winhance application.
    /// </summary>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        return services
            .AddCustomizationDomainServices()
            .AddOptimizationDomainServices()
            .AddSoftwareAppServices();
    }

    /// <summary>
    /// Registers customization domain services.
    /// </summary>
    public static IServiceCollection AddCustomizationDomainServices(this IServiceCollection services)
    {
        // Register WallpaperService (required by WindowsThemeService)
        services.AddSingleton<IWallpaperService, WallpaperService>();

        // Register WindowsThemeService
        services.AddSingleton<WindowsThemeService>(sp => new WindowsThemeService(
            sp.GetRequiredService<IWallpaperService>(),
            sp.GetRequiredService<IWindowsVersionService>(),
            sp.GetRequiredService<IWindowsUIManagementService>(),
            sp.GetRequiredService<IWindowsRegistryService>(),
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>(),
            sp.GetRequiredService<IConfigImportState>()
        ));
        // Register as IDomainService for registry
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<WindowsThemeService>());

        // Register StartMenuService
        services.AddSingleton<StartMenuService>(sp => new StartMenuService(
            sp.GetRequiredService<IScheduledTaskService>(),
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>(),
            sp.GetRequiredService<IInteractiveUserService>(),
            sp.GetRequiredService<IProcessExecutor>()
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
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>()
        ));
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<ExplorerCustomizationService>());

        return services;
    }

    /// <summary>
    /// Registers optimization domain services.
    /// </summary>
    public static IServiceCollection AddOptimizationDomainServices(this IServiceCollection services)
    {
        // Register PowerService
        services.AddSingleton<PowerService>(sp => new PowerService(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<IPowerSettingsQueryService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>(),
            sp.GetRequiredService<IEventBus>(),
            sp.GetRequiredService<IPowerPlanComboBoxService>(),
            sp.GetRequiredService<IProcessExecutor>()
        ));
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<PowerService>());
        // Register as IPowerService for ViewModels that still use direct injection
        services.AddSingleton<IPowerService>(sp => sp.GetRequiredService<PowerService>());

        // Register PrivacyAndSecurityService
        services.AddSingleton<PrivacyAndSecurityService>(sp => new PrivacyAndSecurityService(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>()
        ));
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<PrivacyAndSecurityService>());

        // Register GamingPerformanceService
        services.AddSingleton<GamingPerformanceService>(sp => new GamingPerformanceService(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>()
        ));
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<GamingPerformanceService>());

        // Register NotificationService
        services.AddSingleton<NotificationService>(sp => new NotificationService(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>()
        ));
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<NotificationService>());

        // Register SoundService
        services.AddSingleton<SoundService>(sp => new SoundService(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>()
        ));
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<SoundService>());

        // Register UpdateService
        services.AddSingleton<UpdateService>(sp => new UpdateService(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<IWindowsRegistryService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>(),
            sp.GetRequiredService<IProcessExecutor>(),
            sp.GetRequiredService<IPowerShellRunner>()
        ));
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<UpdateService>());

        return services;
    }

    /// <summary>
    /// Registers software apps domain services.
    /// </summary>
    public static IServiceCollection AddSoftwareAppServices(this IServiceCollection services)
    {
        // New Domain Services (Scoped - Business logic)
        services.AddScoped<IWindowsAppsService, WindowsAppsService>();
        services.AddScoped<IExternalAppsService, ExternalAppsService>();
        services.AddScoped<IAppInstallationService, AppInstallationService>();
        services.AddScoped<IAppRemovalService, AppRemovalService>();

        // App Status Discovery Service (Singleton - Expensive operation)
        services.AddSingleton<IAppStatusDiscoveryService, AppStatusDiscoveryService>();

        // App Services (Scoped - Business logic)
        services.AddScoped<IAppLoadingService, AppLoadingService>();

        // WinGet Service
        services.AddSingleton<IWinGetService, WinGetService>();

        // Chocolatey Services (Fallback package manager)
        services.AddSingleton<IChocolateyService, ChocolateyService>();
        services.AddSingleton<IChocolateyConsentService, ChocolateyConsentService>();

        // App Uninstall Service
        services.AddScoped<IAppUninstallService, AppUninstallService>();

        // Store Download Service (Fallback for market-restricted apps)
        services.AddSingleton<IStoreDownloadService, StoreDownloadService>();

        // Direct Download Service (For non-WinGet apps)
        services.AddSingleton<IDirectDownloadService, DirectDownloadService>();

        // Legacy Capability and Optional Feature Services (Scoped - depends on Scoped IWindowsAppsService)
        services.AddScoped<ILegacyCapabilityService>(provider => new LegacyCapabilityService(
            provider.GetRequiredService<ILogService>(),
            provider.GetRequiredService<IWindowsAppsService>()
        ));
        services.AddScoped<IOptionalFeatureService>(provider => new OptionalFeatureService(
            provider.GetRequiredService<ILogService>(),
            provider.GetRequiredService<IWindowsAppsService>()
        ));

        // App Removal Service (Singleton - Simplified removal logic)
        services.AddSingleton<IBloatRemovalService, BloatRemovalService>();

        return services;
    }

}
