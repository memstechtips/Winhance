using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Common.Events;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Optimize.Services;

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

        // SettingApplicationService depends on the ISpecialSettingHandlerRegistry dispatcher registry; TryAdd
        // registers an empty default here so the UI composition root's real handler-set registration wins.
        services.TryAddSingleton<ISpecialSettingHandlerRegistry>(_ =>
            new SpecialSettingHandlerRegistry(() => new Dictionary<string, ISpecialSettingHandler>()));
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

        services.AddSingleton<IPolicyCleanupService, PolicyCleanupService>();

        services.AddSingleton<IConfigMigrationService, ConfigMigrationService>();

        services.AddSingleton<IDismProcessRunner, DismProcessRunner>();

        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IWimImageService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.WimImageService>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IOscdimgToolManager,
            Winhance.Infrastructure.Features.AdvancedTools.Services.OscdimgToolManager>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IIsoService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.IsoService>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IWimCustomizationService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.WimCustomizationService>();
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.AutounattendScriptBuilder>();

        services.TryAddSingleton<System.Net.Http.HttpClient>();

        return services;
    }
}
