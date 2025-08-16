using Microsoft.Extensions.DependencyInjection;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    /// <summary>
    /// Defines the service lifetime strategy for the Winhance application.
    /// This class documents and enforces consistent service lifetime decisions
    /// across the entire dependency injection configuration.
    /// </summary>
    public static class ServiceLifetimeStrategy
    {
        /// <summary>
        /// Services that should be registered as Singletons.
        /// These are cross-cutting concerns and stateless services that are expensive to create
        /// or need to maintain state across the application lifetime.
        /// </summary>
        public static readonly HashSet<string> SingletonServices = new()
        {
            // Core Infrastructure Services
            "ILogService",
            "IRegistryService",
            "ISystemServices",
            "ICommandService",
            "IEventBus",
            "ITaskProgressService",
            // System Detection Services (Expensive to initialize)
            "IPowerShellDetectionService",
            "IScriptPathDetectionService",
            "IBatteryService",
            "IInternetConnectivityService",
            // Configuration Services
            "IConfigurationService",
            "IGlobalSettingsRegistry",
            "ISettingsRegistry",
            // Discovery Services (Expensive operations)
            "IAppDiscoveryService",
            "IFeatureDiscoveryService",
            "IVersionService",
            // UI Infrastructure
            "IThemeManager",
            "IApplicationCloseService",
            // Notification Services
            "IWinhanceNotificationService",
        };

        /// <summary>
        /// Services that should be registered as Scoped.
        /// These are domain services that follow Domain-Driven Design principles
        /// and should have a per-operation lifetime to maintain proper boundaries.
        /// </summary>
        public static readonly HashSet<string> ScopedServices = new()
        {
            // Domain Services (DDD Pattern)
            "IWindowsThemeService",
            "IStartMenuService",
            "ITaskbarService",
            "IExplorerCustomizationService",
            // Optimization Domain Services
            "IPowerService",
            "IPrivacyService",
            "ISecurityService",
            "IGamingPerformanceService",
            "INotificationService",
            "ISoundService",
            "IUpdateService",
            "IExplorerOptimizationService",
            // Software Apps Domain Services
            "IPackageManager",
            "IAppService",
            "IAppInstallationService",
            "IAppRemovalService",
            // Application Layer Services
            "ISettingApplicationService",
            "SystemSettingOrchestrator",
            // UI Coordination Services (Per-operation state)
            "ISettingsUICoordinator",
        };

        /// <summary>
        /// Services that should be registered as Transient.
        /// These are lightweight, stateless services and ViewModels that should be
        /// created fresh for each resolution to avoid state pollution.
        /// </summary>
        public static readonly HashSet<string> TransientServices = new()
        {
            // ViewModels (Always Transient to avoid state issues)
            "MainViewModel",
            "OptimizeViewModel",
            "CustomizeViewModel",
            "WindowsAppsViewModel",
            "ExternalAppsViewModel",
            "SoftwareAppsViewModel",
            // Feature ViewModels
            "PowerOptimizationsViewModel",
            "PrivacyOptimizationsViewModel",
            "SecurityOptimizationsViewModel",
            "GamingandPerformanceOptimizationsViewModel",
            "NotificationOptimizationsViewModel",
            "SoundOptimizationsViewModel",
            "UpdateOptimizationsViewModel",
            "ExplorerOptimizationsViewModel",
            "TaskbarCustomizationsViewModel",
            "StartMenuCustomizationsViewModel",
            "WindowsThemeCustomizationsViewModel",
            "ExplorerCustomizationsViewModel",
            // UI Services (Stateless)
            "IDialogService",
            "ISettingsConfirmationService",
            "ISettingsDelegateAssignmentService",
            // Utility Services
            "IPropertyUpdater",
            "IOptimizeConfigurationApplier",
            "IWindowsCompatibilityFilter",
            // Factory Services
            "IFeatureViewModelFactory",
        };

        /// <summary>
        /// Gets the recommended service lifetime for a given service type.
        /// </summary>
        /// <param name="serviceType">The service type to get lifetime for</param>
        /// <returns>The recommended ServiceLifetime</returns>
        public static ServiceLifetime GetRecommendedLifetime(Type serviceType)
        {
            var typeName = serviceType.Name;

            if (SingletonServices.Contains(typeName))
                return ServiceLifetime.Singleton;

            if (ScopedServices.Contains(typeName))
                return ServiceLifetime.Scoped;

            if (TransientServices.Contains(typeName))
                return ServiceLifetime.Transient;

            // Default strategy based on naming conventions
            if (typeName.EndsWith("ViewModel"))
                return ServiceLifetime.Transient;

            if (
                typeName.EndsWith("Service")
                && (typeName.Contains("Domain") || typeName.Contains("Business"))
            )
                return ServiceLifetime.Scoped;

            if (typeName.EndsWith("Factory") || typeName.EndsWith("Builder"))
                return ServiceLifetime.Transient;

            if (typeName.Contains("Configuration") || typeName.Contains("Settings"))
                return ServiceLifetime.Singleton;

            // Default to Scoped for safety
            return ServiceLifetime.Scoped;
        }

        /// <summary>
        /// Validates that a service is being registered with the correct lifetime.
        /// </summary>
        /// <param name="serviceType">The service type being registered</param>
        /// <param name="proposedLifetime">The lifetime being proposed</param>
        /// <returns>True if the lifetime is appropriate, false otherwise</returns>
        public static bool ValidateServiceLifetime(
            Type serviceType,
            ServiceLifetime proposedLifetime
        )
        {
            var recommendedLifetime = GetRecommendedLifetime(serviceType);
            return recommendedLifetime == proposedLifetime;
        }

        /// <summary>
        /// Gets a human-readable explanation for why a service should have a particular lifetime.
        /// </summary>
        /// <param name="serviceType">The service type to explain</param>
        /// <returns>Explanation string</returns>
        public static string GetLifetimeRationale(Type serviceType)
        {
            var typeName = serviceType.Name;
            var recommendedLifetime = GetRecommendedLifetime(serviceType);

            return recommendedLifetime switch
            {
                ServiceLifetime.Singleton =>
                    $"{typeName} should be Singleton because it's a cross-cutting concern or expensive to create",
                ServiceLifetime.Scoped =>
                    $"{typeName} should be Scoped because it's a domain service that should maintain proper DDD boundaries",
                ServiceLifetime.Transient =>
                    $"{typeName} should be Transient because it's stateless or a ViewModel that should avoid state pollution",
                _ => $"{typeName} lifetime rationale not defined",
            };
        }
    }
}
