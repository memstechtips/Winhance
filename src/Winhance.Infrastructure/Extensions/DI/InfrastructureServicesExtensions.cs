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

/// <summary>
/// Extension methods for registering infrastructure services.
/// </summary>
public static class InfrastructureServicesExtensions
{
    /// <summary>
    /// Registers infrastructure services for the Winhance application.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Core Infrastructure Services (Singleton - Cross-cutting concerns)
        services.AddSingleton<IConfigImportState, ConfigImportState>();
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();
        services.AddSingleton<ILogService, Winhance.Core.Features.Common.Services.LogService>();
        services.AddSingleton<IInteractiveUserService, InteractiveUserService>();
        services.AddSingleton<ISystemInfoProvider, SystemInfoProvider>();
        services.AddSingleton<IWindowsRegistryService, WindowsRegistryService>();
        // Dependency Manager
        services.AddSingleton<IDependencyManager, Winhance.Core.Features.Common.Services.DependencyManager>();

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

        // Settings Registry
        services.AddSingleton<IGlobalSettingsRegistry, Winhance.Core.Features.Common.Services.GlobalSettingsRegistry>();

        // Global Settings Preloader (registers bypassed settings in the global registry)
        services.AddSingleton<IGlobalSettingsPreloader, GlobalSettingsPreloader>();

        // File System Service
        services.AddSingleton<IFileSystemService, FileSystemService>();

        // Power Scheme Operations (P/Invoke wrapper for plan-level power operations)
        services.AddSingleton<IPowerSchemeOperations, PowerSchemeOperations>();

        // Power-plan activation orchestration (Phase 6.7 Slice 8b-1: extracted from PowerService; six leaf
        // deps, no PowerService/IStateWriter reference so it is DI-cycle-safe). Consumed by PowerService's
        // apply shells and by WindowsStateWriter.ActivatePowerPlan.
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

        // Settings Discovery and Application
        // SystemSettingsDiscoveryService depends on ISpecialDiscoveryRegistry.
        // The UI composition root re-registers that registry (in AddSettingServices)
        // with the real handler set (PowerService, UpdateService); because that runs
        // after AddInfrastructureServices, the richer registration wins in the app.
        // TryAdd here provides an empty default so the infrastructure container is
        // self-contained when composed on its own (e.g. integration smoke tests).
        services.TryAddSingleton<ISpecialDiscoveryRegistry>(_ =>
            new SpecialDiscoveryRegistry(() => System.Array.Empty<ISpecialSettingHandler>()));
        // SettingApplicationService also depends on the ISpecialSettingHandlerRegistry
        // dispatcher registry, re-registered by the UI composition root with the real
        // handler set. Same TryAdd-default rationale as ISpecialDiscoveryRegistry above.
        services.TryAddSingleton<ISpecialSettingHandlerRegistry>(_ =>
            new SpecialSettingHandlerRegistry(() => new Dictionary<string, ISpecialSettingHandler>()));
        services.AddSingleton<ISystemSettingsDiscoveryService, SystemSettingsDiscoveryService>();
        services.AddSingleton<IProcessRestartManager, ProcessRestartManager>();
        services.AddSingleton<IPowerCfgApplier, PowerCfgApplier>();
        services.AddSingleton<ISettingDependencyResolver, SettingDependencyResolver>();
        services.AddSingleton<IRecommendedSettingsApplier, RecommendedSettingsApplier>();
        services.AddSingleton<IBulkSettingsActionService, BulkSettingsActionService>();
        services.AddSingleton<ISettingOperationExecutor, SettingOperationExecutor>();
        services.AddSingleton<ISettingApplicationService, SettingApplicationService>();

        // Catalog detection context (new unified engine): a factory, because each detection batch needs a fresh
        // context to hold its own pre-fetched async reads.
        services.AddSingleton<ISystemDetectionContextFactory, SystemDetectionContextFactory>();
        // Catalog detection batch driver + the observe-only parallel-run shadow (logs old-vs-new divergences).
        services.AddSingleton<ICatalogDetectionService, CatalogDetectionService>();
        services.AddSingleton<IDetectionShadowRunner, DetectionShadowRunner>();
        // Catalog apply: the live IStateWriter that executes apply plans against the real system (Phase 6.4),
        // plus the OTS-aware .reg-import effect runner it delegates to.
        services.AddSingleton<IRegImportService, RegImportService>();
        services.AddSingleton<IStateWriter, WindowsStateWriter>();

        // ComboBox Services (IComboBoxSetupService/ComboBoxSetupService retired - Phase 6.8 teardown; every consumer
        // now builds the combobox options directly off the new catalog model. Resolver/PowerPlan stay - still consumed
        // by the teardown-scope old apply layer.)
        services.AddSingleton<IComboBoxResolver, ComboBoxResolver>();
        services.AddSingleton<IPowerPlanComboBoxService, PowerPlanComboBoxService>();

        // Settings Compatibility
        services.AddSingleton<ICompatibleSettingsRegistry, CompatibleSettingsRegistry>();
        services.AddSingleton<IWindowsCompatibilityFilter, WindowsCompatibilityFilter>();
        services.AddSingleton<IHardwareCompatibilityFilter, HardwareCompatibilityFilter>();
        services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();

        // Script Services
        services.AddSingleton<IPowerSettingsQueryService, PowerSettingsQueryService>();
        services.AddSingleton<IPowerSettingsValidationService, PowerSettingsValidationService>();

        // System Services
        services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();
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
