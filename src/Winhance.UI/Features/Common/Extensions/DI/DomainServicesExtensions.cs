using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Infrastructure.Features.Optimize.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;
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
        services
            .AddCustomizationDomainServices()
            .AddOptimizationDomainServices()
            .AddSoftwareAppServices();

        // Phase 1/6 of the domain services cleanup: id-keyed dispatchers that
        // will replace IDomainServiceRouter in Phase 2 onward. Both surfaces
        // coexist during the transition; nothing reads these yet.
        services.AddSingleton<ISpecialSettingHandlerRegistry>(sp =>
            new SpecialSettingHandlerRegistry(new Dictionary<string, ISpecialSettingHandler>
            {
                [SettingIds.PowerPlanSelection] = sp.GetRequiredService<PowerService>(),
                [SettingIds.UpdatesPolicyMode]  = sp.GetRequiredService<UpdateService>(),
                [SettingIds.ThemeModeWindows]   = sp.GetRequiredService<WindowsThemeService>(),
            }));

        services.AddSingleton<IActionCommandRegistry>(sp =>
            new ActionCommandRegistry(new Dictionary<string, IActionCommandProvider>
            {
                [SettingIds.TaskbarClean]         = sp.GetRequiredService<TaskbarService>(),
                [SettingIds.StartMenuCleanWin10]  = sp.GetRequiredService<StartMenuService>(),
                [SettingIds.StartMenuCleanWin11]  = sp.GetRequiredService<StartMenuService>(),
            }));

        services.AddSingleton<ISpecialDiscoveryRegistry>(sp =>
            new SpecialDiscoveryRegistry(new List<ISpecialSettingHandler>
            {
                sp.GetRequiredService<PowerService>(),
                sp.GetRequiredService<UpdateService>(),
                sp.GetRequiredService<WindowsThemeService>(),
            }));

        return services;
    }

    /// <summary>
    /// Registers customization domain services.
    /// </summary>
    public static IServiceCollection AddCustomizationDomainServices(this IServiceCollection services)
    {
        // Register WallpaperService (required by WindowsThemeService)
        services.AddSingleton<IWallpaperService, WallpaperService>();

        // Register WindowsThemeService
        services.AddSingleton<WindowsThemeService>();
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<WindowsThemeService>());

        // Register StartMenuService
        services.AddSingleton<StartMenuService>();
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<StartMenuService>());

        // Register TaskbarService
        services.AddSingleton<TaskbarService>();
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<TaskbarService>());

        // Register ExplorerCustomizationService
        services.AddSingleton<ExplorerCustomizationService>();
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<ExplorerCustomizationService>());

        return services;
    }

    /// <summary>
    /// Registers optimization domain services.
    /// </summary>
    public static IServiceCollection AddOptimizationDomainServices(this IServiceCollection services)
    {
        // Register PowerService (keeps factory — 3 registrations with IPowerService forwarding)
        services.AddSingleton<PowerService>(sp => new PowerService(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<IPowerSettingsQueryService>(),
            sp.GetRequiredService<ICompatibleSettingsRegistry>(),
            sp.GetRequiredService<IEventBus>(),
            sp.GetRequiredService<IPowerPlanComboBoxService>(),
            sp.GetRequiredService<IProcessExecutor>(),
            sp.GetRequiredService<IFileSystemService>(),
            sp.GetRequiredService<IPowerSchemeOperations>()
        ));
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<PowerService>());
        services.AddSingleton<IPowerService>(sp => sp.GetRequiredService<PowerService>());

        // Register PrivacyAndSecurityService
        services.AddSingleton<PrivacyAndSecurityService>();
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<PrivacyAndSecurityService>());

        // Register GamingPerformanceService
        services.AddSingleton<GamingPerformanceService>();
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<GamingPerformanceService>());

        // Register NotificationService
        services.AddSingleton<NotificationService>();
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<NotificationService>());

        // Register SoundService
        services.AddSingleton<SoundService>();
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<SoundService>());

        // Register UpdateService
        services.AddSingleton<UpdateService>();
        services.AddSingleton<IDomainService>(sp => sp.GetRequiredService<UpdateService>());

        return services;
    }

    /// <summary>
    /// Registers software apps domain services.
    /// </summary>
    public static IServiceCollection AddSoftwareAppServices(this IServiceCollection services)
    {
        // Domain Services (Singleton - consumed by Singleton ViewModels)
        services.AddSingleton<IWindowsAppsService, WindowsAppsService>();
        services.AddSingleton<IExternalAppsService, ExternalAppsService>();
        services.AddSingleton<IAppInstallationService, AppInstallationService>();
        services.AddSingleton<IAppUninstallationService, AppUninstallationService>();

        // AppX package source (PackageManager COM → WMI → PowerShell fallback)
        services.AddSingleton<IAppxPackageSource, AppxPackageSource>();

        // AppX icon source (PackageManager + AppListEntry.DisplayInfo.GetLogo,
        // covers current-user / all-users / provisioned scopes)
        services.AddSingleton<IAppxIconSource, AppxIconSource>();

        // Microsoft Store CDN icon source (Layer-2 fallback for AppX entries
        // not present on this machine in any registered/provisioned form)
        services.AddSingleton<IStoreIconSource, StoreIconSource>();

        // Layer 1b icon sources (shell images, binary icons via Windows ARP).
        // Layer 2b is now handled by the per-entry IconSources field on
        // ItemDefinition (URLs + local paths) rather than a separate service.
        services.AddSingleton<IShellImageFactory, ShellImageFactory>();
        services.AddSingleton<IBinaryIconSource, BinaryIconSource>();

        // App icon resolver (cache-first, called from WindowsAppsViewModel after install-status discovery)
        services.AddSingleton<IAppIconResolver, AppIconResolver>();

        // App Status Discovery Service (Singleton - Expensive operation)
        services.AddSingleton<IAppStatusDiscoveryService, AppStatusDiscoveryService>();

        // WinGet decomposed services
        services.AddSingleton<WinGetComSession>();
        services.AddSingleton<IWinGetBootstrapper, WinGetBootstrapper>();
        services.AddSingleton<IWinGetDetectionService, WinGetDetectionService>();
        services.AddSingleton<IWinGetPackageInstaller, WinGetPackageInstaller>();

        // Chocolatey Services (Fallback package manager)
        services.AddSingleton<IChocolateyService, ChocolateyService>();

        // App Uninstall Service
        services.AddSingleton<IAppUninstallService, AppUninstallService>();

        // Store Download Service (Fallback for market-restricted apps)
        services.AddSingleton<IStoreDownloadService, StoreDownloadService>();

        // Direct Download Service (For non-WinGet apps)
        services.AddSingleton<IDirectDownloadService, DirectDownloadService>();

        // Legacy Capability and Optional Feature Services (Singleton - depends on Singleton IWindowsAppsService)
        services.AddSingleton<ILegacyCapabilityService>(provider => new LegacyCapabilityService(
            provider.GetRequiredService<ILogService>(),
            provider.GetRequiredService<IWindowsAppsService>()
        ));
        services.AddSingleton<IOptionalFeatureService>(provider => new OptionalFeatureService(
            provider.GetRequiredService<ILogService>(),
            provider.GetRequiredService<IWindowsAppsService>()
        ));

        // App Removal Service (Singleton - Simplified removal logic)
        services.AddSingleton<IBloatRemovalService, BloatRemovalService>();

        return services;
    }

}
