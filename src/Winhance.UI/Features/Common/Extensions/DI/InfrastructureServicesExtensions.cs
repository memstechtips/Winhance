using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Events;
using Winhance.Infrastructure.Features.Common.EventHandlers;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.UI.Features.Common.Extensions.DI;

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
        services.AddSingleton<ILogService, Winhance.Core.Features.Common.Services.LogService>();
        services.AddSingleton<IWindowsRegistryService, WindowsRegistryService>();
        // Dependency Manager
        services.AddSingleton<IDependencyManager, Winhance.Core.Features.Common.Services.DependencyManager>();

        // Windows Services
        services.AddSingleton<IWindowsVersionService, WindowsVersionService>();
        services.AddSingleton<IWindowsThemeQueryService, WindowsThemeQueryService>();
        services.AddSingleton<IWindowsUIManagementService, WindowsUIManagementService>();

        // User Preferences Service
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();

        // Localization Service
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // Event Bus (Singleton - Message routing)
        services.AddSingleton<IEventBus, EventBus>();

        // Initialization Service
        services.AddSingleton<IInitializationService, Winhance.Core.Features.Common.Services.InitializationService>();

        // Settings Registry
        services.AddSingleton<IGlobalSettingsRegistry, Winhance.Core.Features.Common.Services.GlobalSettingsRegistry>();
        services.AddSingleton<ISettingsRegistry, Winhance.Core.Features.Common.Services.SettingsRegistry>();

        // Global Settings Preloader (populates setting-to-feature mappings)
        services.AddSingleton<IGlobalSettingsPreloader, GlobalSettingsPreloader>();

        // Settings Discovery and Application
        services.AddSingleton<ISystemSettingsDiscoveryService, SystemSettingsDiscoveryService>();
        services.AddSingleton<ISettingApplicationService, SettingApplicationService>();

        // Domain Service Router
        services.AddSingleton<IDomainServiceRouter, DomainServiceRouter>();

        // ComboBox Services
        services.AddSingleton<IComboBoxSetupService, ComboBoxSetupService>();
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

        // Internet Connectivity
        services.AddSingleton<IInternetConnectivityService>(provider =>
            new InternetConnectivityService(provider.GetRequiredService<ILogService>()));

        // System Services
        services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();
        services.AddSingleton<ISystemBackupService, SystemBackupService>();
        services.AddSingleton<IVersionService, VersionService>();

        // Script Services
        services.AddSingleton<IScriptMigrationService, ScriptMigrationService>();
        services.AddSingleton<IRemovalScriptUpdateService, RemovalScriptUpdateService>();

        // Task Progress Service
        services.AddSingleton<ITaskProgressService, TaskProgressService>();

        // Tooltip Services
        services.AddSingleton<ITooltipDataService, TooltipDataService>();
        services.AddSingleton<TooltipRefreshEventHandler>();

        // Configuration Application Bridge (for config import/export)
        services.AddSingleton<ConfigurationApplicationBridgeService>();

        // Configuration Migration (for backward-compatible config imports)
        services.AddSingleton<ConfigMigrationService>();

        // Recommended Settings Service
        services.AddSingleton<IRecommendedSettingsService>(provider =>
            new RecommendedSettingsService(
                provider.GetRequiredService<IDomainServiceRouter>(),
                provider.GetRequiredService<IWindowsVersionService>(),
                provider.GetRequiredService<ILogService>()));

        // Advanced Tools Services
        services.AddSingleton<Winhance.Core.Features.AdvancedTools.Interfaces.IWimUtilService,
            Winhance.Infrastructure.Features.AdvancedTools.Services.WimUtilService>();
        services.AddSingleton<Winhance.Infrastructure.Features.AdvancedTools.Services.AutounattendScriptBuilder>();

        // Http Client
        services.TryAddSingleton<System.Net.Http.HttpClient>();

        return services;
    }
}
