using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Common.Events;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Infrastructure.Features.Optimize.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;

namespace Winhance.Infrastructure.Extensions.DI;

public static class InfrastructureServicesExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigImportState, ConfigImportState>();
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();
        services.AddSingleton<ILogService, Winhance.Core.Features.Common.Services.LogService>();
        services.AddSingleton<IInteractiveUserService, InteractiveUserService>();
        services.AddSingleton<ISystemInfoProvider, SystemInfoProvider>();
        services.AddSingleton<IWindowsRegistryService, WindowsRegistryService>();

        services.AddSingleton<IWindowsVersionService, WindowsVersionService>();
        services.AddSingleton<IWindowsUIManagementService, WindowsUIManagementService>();

        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();

        services.AddSingleton<INewBadgeService, NewBadgeService>();

        services.AddSingleton<ILocalizationService, LocalizationService>();

        services.AddSingleton<IEventBus, EventBus>();

        services.AddSingleton<IInitializationService, Winhance.Core.Features.Common.Services.InitializationService>();

        services.AddSingleton<IFileSystemService, FileSystemService>();

        services.AddSingleton<IPowerSchemeOperations, PowerSchemeOperations>();

        // Power-plan activation orchestration (six leaf deps, no PowerService/IStateWriter reference so it
        // is DI-cycle-safe). Consumed by WindowsStateWriter.ActivatePowerPlan.
        services.AddSingleton<IPowerPlanActivationService, PowerPlanActivationService>();

        services.AddSingleton<IExplorerWindowManager, ExplorerWindowManager>();

        services.AddSingleton<IChangeHistoryService, ChangeHistoryService>();

        services.AddSingleton<ISystemParametersService, SystemParametersService>();

        services.AddSingleton<IPowerShellRunner, Winhance.Infrastructure.Features.Common.Utilities.PowerShellRunner>();

        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IDriverCategorizer,
            Winhance.Infrastructure.Features.AdvancedTools.Helpers.DriverCategorizer>();

        // power-plan-selection is NOT registered as an apply handler, so the apply funnel falls through to the
        // catalog engine (ApplyRequestResolver -> PowerPlanActivateOp -> WindowsStateWriter.ActivatePowerPlan ->
        // IPowerPlanActivationService.EnsureActivatedAsync).
        services.AddSingleton<ISpecialSettingHandlerRegistry>(sp =>
            new SpecialSettingHandlerRegistry(() => new Dictionary<string, ISpecialSettingHandler>
            {
                [SettingIds.UpdatesPolicyMode]  = sp.GetRequiredService<UpdateService>(),
                [SettingIds.ThemeModeWindows]   = sp.GetRequiredService<ThemeWallpaperApplier>(),
            }));
        // Pending Explorer restart state (observed by the bottom bar; cleared by ExplorerRestartService)
        services.AddSingleton<IPendingRestartService, PendingRestartService>();

        // The single owner of restarting Explorer (single-flight, elevation-safe relaunch)
        services.AddSingleton<IExplorerRestartService, ExplorerRestartService>();

        services.AddSingleton<IProcessRestartManager, ProcessRestartManager>();
        services.AddSingleton<IPowerCfgApplier, PowerCfgApplier>();
        services.AddSingleton<IRecommendedSettingsApplier, RecommendedSettingsApplier>();
        services.AddSingleton<IBulkSettingsActionService, BulkSettingsActionService>();
        services.AddSingleton<ISettingApplicationService, SettingApplicationService>();

        // Catalog detection context: a factory, because each detection batch needs a fresh
        // context to hold its own pre-fetched async reads.
        services.AddSingleton<ISystemDetectionContextFactory, SystemDetectionContextFactory>();
        services.AddSingleton<ICatalogDetectionService, CatalogDetectionService>();
        services.AddSingleton<ICatalogSettingStateProvider, CatalogSettingStateProvider>();
        services.AddSingleton<IRegImportService, RegImportService>();
        services.AddSingleton<IStateWriter, WindowsStateWriter>();
        services.AddSingleton<IAsyncEffectRunner, WindowsAsyncEffectRunner>();

        services.AddSingleton<IComboBoxResolver, ComboBoxResolver>();

        services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();

        services.AddSingleton<IPowerSettingsQueryService, PowerSettingsQueryService>();
        services.AddSingleton<ICatalogPowerExistenceFilter, CatalogPowerExistenceFilter>();
        services.AddSingleton<ICatalogSettingsRegistry, CatalogSettingsRegistry>();

        // One COM adapter, two contracts: Winhance's own tasks, and the state of tasks Windows owns.
        services.AddSingleton<ScheduledTaskService>();
        services.AddSingleton<IScheduledTaskService>(sp => sp.GetRequiredService<ScheduledTaskService>());
        services.AddSingleton<IScheduledTaskStateService>(sp => sp.GetRequiredService<ScheduledTaskService>());
        services.AddSingleton<ISystemBackupService, SystemBackupService>();
        services.AddSingleton<ISystemRestoreService, SystemRestoreService>();
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<ISponsorsService, SponsorsService>();

        services.AddSingleton<IScriptMigrationService, ScriptMigrationService>();
        services.AddSingleton<IRemovalScriptUpdateService, RemovalScriptUpdateService>();

        services.AddSingleton<TaskProgressService>();
        services.AddSingleton<ITaskProgressService>(sp => sp.GetRequiredService<TaskProgressService>());
        services.AddSingleton<IMultiScriptProgressService>(sp => sp.GetRequiredService<TaskProgressService>());

        services.AddSingleton<IConfigurationApplicationBridgeService, ConfigurationApplicationBridgeService>();
        services.AddSingleton<ISettingSnapshotSource, SettingSnapshotSource>();
        services.AddSingleton<IConfigFileWriter, ConfigFileWriter>();
        services.AddSingleton<IAutounattendWriter, Winhance.Infrastructure.Features.AdvancedTools.Services.AutounattendWriter>();
        services.AddSingleton<IBuilderSeedSource, BuilderSeedSource>();

        services.AddSingleton<IPolicyCleanupService, PolicyCleanupService>();

        services.AddSingleton<IConfigMigrationService, ConfigMigrationService>();

        services.AddSingleton<IDismProcessRunner, DismProcessRunner>();

        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.IDismImageInfoReader,
            Winhance.Infrastructure.Features.AdvancedTools.Services.DismImageInfoReader>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IWimImageService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.WimImageService>();
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.IVirtualDiskNative,
            Winhance.Infrastructure.Features.AdvancedTools.Services.VirtualDiskNative>();
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.IIsoImageReader,
            Winhance.Infrastructure.Features.AdvancedTools.Services.VirtualDiskIsoImageReader>();
        services.AddSingleton<Func<Winhance.Infrastructure.Features.AdvancedTools.Services.IFileSystemImageWrapper>>(
            _ => () => new Winhance.Infrastructure.Features.AdvancedTools.Services.Imapi2FileSystemImage());
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.IIsoImageWriter,
            Winhance.Infrastructure.Features.AdvancedTools.Services.Imapi2IsoImageWriter>();
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.IFileCopyNative,
            Winhance.Infrastructure.Features.AdvancedTools.Services.FileCopyNative>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.IMediaCopier,
            Winhance.Infrastructure.Features.AdvancedTools.Services.MediaCopier>();
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.IStorageManagementApi,
            Winhance.Infrastructure.Features.AdvancedTools.Services.WmiStorageManagementApi>();
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.IStorageOperations,
            Winhance.Infrastructure.Features.AdvancedTools.Services.WmiStorageService>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IUsbMediaWriter,
            Winhance.Infrastructure.Features.AdvancedTools.Services.StorageApiUsbMediaWriter>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IIsoService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.IsoService>();
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.IDriverInstallStepWriter,
            Winhance.Infrastructure.Features.AdvancedTools.Services.DriverInstallStepWriter>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IWimCustomizationService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.WimCustomizationService>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IAutounattendScriptBuilder,
            Winhance.Infrastructure.Features.AdvancedTools.Services.AutounattendScriptBuilder>();

        services.TryAddSingleton<System.Net.Http.HttpClient>();

        return services
            .AddCustomizationServices()
            .AddOptimizationServices()
            .AddSoftwareAppServices();
    }

    private static IServiceCollection AddCustomizationServices(this IServiceCollection services)
    {
        services.AddSingleton<IWallpaperService, WallpaperService>();

        // ThemeWallpaperApplier's explorer refresh is declarative via the Setting's RestartProcess.
        services.AddSingleton<ThemeWallpaperApplier>();

        return services;
    }

    private static IServiceCollection AddOptimizationServices(this IServiceCollection services)
    {
        services.AddSingleton<PowerService>();
        services.AddSingleton<IPowerService>(sp => sp.GetRequiredService<PowerService>());

        services.AddSingleton<UpdateService>();

        return services;
    }

    private static IServiceCollection AddSoftwareAppServices(this IServiceCollection services)
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
        services.AddSingleton<IServicingSession, ServicingSession>();

        services.AddSingleton<IBloatRemovalService, BloatRemovalService>();

        return services;
    }
}
