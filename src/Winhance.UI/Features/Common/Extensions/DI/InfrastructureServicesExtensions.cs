using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Events;
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
        services.AddSingleton<ICommandService, CommandService>();

        // Windows Services
        services.AddSingleton<IWindowsVersionService, WindowsVersionService>();
        services.AddSingleton<IWindowsThemeQueryService, WindowsThemeQueryService>();

        // User Preferences Service
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();

        // Localization Service
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // Event Bus (Singleton - Message routing)
        services.AddSingleton<IEventBus, EventBus>();

        // Initialization Service
        services.AddSingleton<IInitializationService, Winhance.Core.Features.Common.Services.InitializationService>();

        // Settings Discovery and Application
        services.AddSingleton<ISystemSettingsDiscoveryService, SystemSettingsDiscoveryService>();
        services.AddSingleton<ISettingApplicationService, SettingApplicationService>();

        // Domain Service Router
        services.AddSingleton<IDomainServiceRouter, DomainServiceRouter>();

        // ComboBox Services
        services.AddSingleton<IComboBoxSetupService, ComboBoxSetupService>();
        services.AddSingleton<IComboBoxResolver, ComboBoxResolver>();
        services.AddSingleton<IPowerPlanComboBoxService, PowerPlanComboBoxService>();

        // Settings Registry and Compatibility
        services.AddSingleton<ICompatibleSettingsRegistry, CompatibleSettingsRegistry>();
        services.AddSingleton<IWindowsCompatibilityFilter, WindowsCompatibilityFilter>();
        services.AddSingleton<IHardwareCompatibilityFilter, HardwareCompatibilityFilter>();
        services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();

        // PowerShell and Script Services
        services.AddSingleton<IPowerShellExecutionService, PowerShellExecutionService>();
        services.AddSingleton<IPowerCfgQueryService, PowerCfgQueryService>();
        services.AddSingleton<IPowerSettingsValidationService, PowerSettingsValidationService>();

        // Task Progress Service
        services.AddSingleton<ITaskProgressService, TaskProgressService>();

        // Http Client
        services.TryAddSingleton<System.Net.Http.HttpClient>();

        return services;
    }
}
