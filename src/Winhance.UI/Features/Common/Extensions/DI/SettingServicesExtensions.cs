using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Infrastructure.Features.Optimize.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;

namespace Winhance.UI.Features.Common.Extensions.DI;

public static class SettingServicesExtensions
{
    public static IServiceCollection AddSettingServices(this IServiceCollection services)
    {
        services
            .AddCustomizationServices()
            .AddOptimizationServices()
            .AddSoftwareAppServices();

        // power-plan-selection is NOT registered as an apply handler, so the apply funnel falls through to the
        // catalog engine (ApplyRequestResolver -> PowerPlanActivateOp -> WindowsStateWriter.ActivatePowerPlan ->
        // IPowerPlanActivationService.EnsureActivatedAsync).
        services.AddSingleton<ISpecialSettingHandlerRegistry>(sp =>
            new SpecialSettingHandlerRegistry(() => new Dictionary<string, ISpecialSettingHandler>
            {
                [SettingIds.UpdatesPolicyMode]  = sp.GetRequiredService<UpdateService>(),
                [SettingIds.ThemeModeWindows]   = sp.GetRequiredService<ThemeWallpaperApplier>(),
            }));

        return services;
    }

    public static IServiceCollection AddCustomizationServices(this IServiceCollection services)
    {
        services.AddSingleton<IWallpaperService, WallpaperService>();

        // ThemeWallpaperApplier's explorer refresh is declarative via the Setting's RestartProcess.
        services.AddSingleton<ThemeWallpaperApplier>();

        return services;
    }

    public static IServiceCollection AddOptimizationServices(this IServiceCollection services)
    {
        services.AddSingleton<PowerService>(sp => new PowerService(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<IPowerSettingsQueryService>(),
            sp.GetRequiredService<IPowerSchemeOperations>()
        ));
        services.AddSingleton<IPowerService>(sp => sp.GetRequiredService<PowerService>());

        services.AddSingleton<UpdateService>();

        return services;
    }

    public static IServiceCollection AddSoftwareAppServices(this IServiceCollection services)
    {
        services.AddSingleton<IWindowsAppsService, WindowsAppsService>();
        services.AddSingleton<IExternalAppsService, ExternalAppsService>();
        services.AddSingleton<IAppInstallationService, AppInstallationService>();
        services.AddSingleton<IWindowsAppUninstallService, WindowsAppUninstallService>();

        services.AddSingleton<IAppxPackageSource, AppxPackageSource>();

        services.AddSingleton<IAppxIconSource, AppxIconSource>();

        services.AddSingleton<IRepoIconSource, RepoIconSource>();
        services.AddSingleton<IIconManifestService, IconManifestService>();

        services.AddSingleton<IAppIconResolver, AppIconResolver>();

        services.AddSingleton<IAppStatusDiscoveryService, AppStatusDiscoveryService>();

        services.AddSingleton<WinGetComSession>();
        services.AddSingleton<IWinGetBootstrapper, WinGetBootstrapper>();
        services.AddSingleton<IWinGetDetectionService, WinGetDetectionService>();
        services.AddSingleton<IWinGetPackageInstaller, WinGetPackageInstaller>();

        services.AddSingleton<IChocolateyService, ChocolateyService>();

        services.AddSingleton<IExternalAppUninstallService, ExternalAppUninstallService>();

        services.AddSingleton<IStoreDownloadService, StoreDownloadService>();

        services.AddSingleton<IDirectDownloadService, DirectDownloadService>();

        services.AddSingleton<ILegacyCapabilityService, LegacyCapabilityService>();
        services.AddSingleton<IOptionalFeatureService, OptionalFeatureService>();

        services.AddSingleton<IBloatRemovalService, BloatRemovalService>();

        return services;
    }

}
