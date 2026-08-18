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
        // Core Infrastructure Services (Singleton - Cross-cutting concerns)
        services.AddSingleton<IConfigImportState, ConfigImportState>();
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();
        services.AddSingleton<ILogService, Winhance.Core.Features.Common.Services.LogService>();
        services.AddSingleton<IInteractiveUserService, InteractiveUserService>();
        services.AddSingleton<ISystemInfoProvider, SystemInfoProvider>();
        services.AddSingleton<IWindowsRegistryService, WindowsRegistryService>();

        // Windows Services
        services.AddSingleton<IWindowsVersionService, WindowsVersionService>();
        services.AddSingleton<IWindowsUIManagementService, WindowsUIManagementService>();

        // User Preferences Service
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();

        // New Badge Service (tracks which settings are new in current release)
        services.AddSingleton<INewBadgeService, NewBadgeService>();

        // Localization Service
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // Event Bus (Singleton - Message routing)
        services.AddSingleton<IEventBus, EventBus>();

        // Initialization Service
        services.AddSingleton<IInitializationService, Winhance.Core.Features.Common.Services.InitializationService>();

        // File System Service
        services.AddSingleton<IFileSystemService, FileSystemService>();

        // Power Scheme Operations (P/Invoke wrapper for plan-level power operations)
        services.AddSingleton<IPowerSchemeOperations, PowerSchemeOperations>();

        // Power-plan activation orchestration (six leaf deps, no PowerService/IStateWriter reference so it
        // is DI-cycle-safe). Consumed by WindowsStateWriter.ActivatePowerPlan.
        services.AddSingleton<IPowerPlanActivationService, PowerPlanActivationService>();

        // Explorer Window Manager (open/focus folders in Explorer)
        services.AddSingleton<IExplorerWindowManager, ExplorerWindowManager>();

        // User-facing change receipt (ChangeHistory.txt)
        services.AddSingleton<IChangeHistoryService, ChangeHistoryService>();

        // System Parameters (wraps User32 SystemParametersInfo P/Invoke)
        services.AddSingleton<ISystemParametersService, SystemParametersService>();

        // PowerShell Runner
        services.AddSingleton<IPowerShellRunner, Winhance.Infrastructure.Features.Common.Utilities.PowerShellRunner>();

        // Driver Categorizer
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IDriverCategorizer,
            Winhance.Infrastructure.Features.AdvancedTools.Helpers.DriverCategorizer>();

        // Settings Application
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
        // Catalog detection batch driver.
        services.AddSingleton<ICatalogDetectionService, CatalogDetectionService>();
        // Full-state provider for GetSettingStatesAsync + overlay.
        services.AddSingleton<ICatalogSettingStateProvider, CatalogSettingStateProvider>();
        // Catalog apply: the synchronous writer, plus the runner for effects that launch a process.
        services.AddSingleton<IRegImportService, RegImportService>();
        services.AddSingleton<IStateWriter, WindowsStateWriter>();
        services.AddSingleton<IAsyncEffectRunner, WindowsAsyncEffectRunner>();

        // ComboBox Services: consumers build the combobox options directly off the catalog model.
        services.AddSingleton<IComboBoxResolver, ComboBoxResolver>();

        // Settings Compatibility
        services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();

        // Script Services
        services.AddSingleton<IPowerSettingsQueryService, PowerSettingsQueryService>();
        services.AddSingleton<ICatalogPowerExistenceFilter, CatalogPowerExistenceFilter>();
        services.AddSingleton<ICatalogSettingsRegistry, CatalogSettingsRegistry>();

        // System Services
        // One COM adapter, two contracts: Winhance's own tasks, and the state of tasks Windows owns.
        services.AddSingleton<ScheduledTaskService>();
        services.AddSingleton<IScheduledTaskService>(sp => sp.GetRequiredService<ScheduledTaskService>());
        services.AddSingleton<IScheduledTaskStateService>(sp => sp.GetRequiredService<ScheduledTaskService>());
        services.AddSingleton<ISystemBackupService, SystemBackupService>();
        services.AddSingleton<ISystemRestoreService, SystemRestoreService>();
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<ISponsorsService, SponsorsService>();

        // Script Services
        services.AddSingleton<IScriptMigrationService, ScriptMigrationService>();
        services.AddSingleton<IRemovalScriptUpdateService, RemovalScriptUpdateService>();

        // Task Progress Service
        services.AddSingleton<TaskProgressService>();
        services.AddSingleton<ITaskProgressService>(sp => sp.GetRequiredService<TaskProgressService>());
        services.AddSingleton<IMultiScriptProgressService>(sp => sp.GetRequiredService<TaskProgressService>());

        // Configuration Application Bridge (for config import/export)
        services.AddSingleton<IConfigurationApplicationBridgeService, ConfigurationApplicationBridgeService>();

        // Policy Cleanup Service (for Windows Defaults import)
        services.AddSingleton<IPolicyCleanupService, PolicyCleanupService>();

        // Configuration Migration (for backward-compatible config imports)
        services.AddSingleton<IConfigMigrationService, ConfigMigrationService>();

        // Advanced Tools Services — DISM Process Runner (shared utility)
        services.AddSingleton<IDismProcessRunner, DismProcessRunner>();

        // Advanced Tools Services — WIM/ISO decomposed services
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IWimImageService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.WimImageService>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IOscdimgToolManager,
            Winhance.Infrastructure.Features.AdvancedTools.Services.OscdimgToolManager>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IIsoService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.IsoService>();
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IWimCustomizationService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.WimCustomizationService>();
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.AutounattendScriptBuilder>();

        // Http Client
        services.TryAddSingleton<System.Net.Http.HttpClient>();

        return services;
    }
}
