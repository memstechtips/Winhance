using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.UI.Features.AdvancedTools.Services;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Customize.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.Settings.ViewModels;
using Winhance.UI.Features.SoftwareApps.Services;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Common.Extensions.DI;

public static class UIServicesExtensions
{
    public static IServiceCollection AddUIServices(this IServiceCollection services)
    {
        // Requires late initialization in MainWindow.xaml.cs after window creation
        services.AddSingleton<IDispatcherService, DispatcherService>();

        services.AddSingleton<IMainWindowProvider, MainWindowProvider>();

        services.AddSingleton<IResourceService, ResourceService>();

        services.AddSingleton<IThemeService, ThemeService>();

        // Requires XamlRoot to be set by MainWindow after content is loaded
        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<IFilePickerService, FilePickerService>();

        services.AddSingleton<IAppSelectionSource, AppSelectionSource>();
        services.AddSingleton<ISelectionSetBuilder, SelectionSetBuilder>();
        services.AddSingleton<IInstallConsent, DialogInstallConsent>();

        services.AddSingleton<IApplicationCloseService, ApplicationCloseService>();

        services.AddSingleton<IStartupNotificationService, StartupNotificationService>();

        services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();

        services.AddSingleton<IWindowsVersionFilterService, WindowsVersionFilterService>();

        services.AddSingleton<IConfigExportService, ConfigExportService>();
        services.AddSingleton<IConfigLoadService, ConfigLoadService>();
        services.AddSingleton<IConfigAppSelectionService, ConfigAppSelectionService>();
        services.AddSingleton<IConfigApplicationExecutionService, ConfigApplicationExecutionService>();
        services.AddSingleton<IConfigReviewOrchestrationService, ConfigReviewOrchestrationService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        services.AddSingleton<IConfigImportOverlayService, ConfigImportOverlayService>();

        services.AddSingleton<IConfigReviewService, ConfigReviewService>();
        services.AddSingleton<IConfigReviewModeService>(sp => (IConfigReviewModeService)sp.GetRequiredService<IConfigReviewService>());
        services.AddSingleton<IConfigReviewDiffService>(sp => (IConfigReviewDiffService)sp.GetRequiredService<IConfigReviewService>());
        services.AddSingleton<IConfigReviewBadgeService>(sp => (IConfigReviewBadgeService)sp.GetRequiredService<IConfigReviewService>());
        services.AddSingleton<IApplicationModeService>(sp => (IApplicationModeService)sp.GetRequiredService<IConfigReviewService>());

        services.AddSingleton<INavBadgeService, NavBadgeService>();

        services.AddSingleton<IRegeditLauncher, Winhance.UI.Features.Common.Utilities.RegeditLauncher>();

        services.AddSingleton<ISettingLocalizationService, SettingLocalizationService>();

        services.AddSingleton<ISettingReviewDiffApplier, SettingReviewDiffApplier>();

        services.AddSingleton<IReviewModeViewModelCoordinator, ReviewModeViewModelCoordinator>();

        // Per-mode setting write strategies. One implementation per thing a mode can do with an
        // edit - apply it, author it, refuse it - selected from the mode's declared capabilities so
        // that a new mode gets the right one without a second table to keep in sync.
        services.AddSingleton<LiveSettingWriteStrategy>();
        services.AddSingleton<BuilderSettingWriteStrategy>();
        services.AddSingleton<ReadOnlySettingWriteStrategy>();
        services.AddSingleton<ISettingWriteStrategySelector, SettingWriteStrategySelector>();

        services.AddSingleton<SettingViewModelDependencies>();

        services.AddSingleton<ISettingViewModelEnricher, SettingViewModelEnricher>();

        services.AddSingleton<ISettingViewModelFactory, SettingViewModelFactory>();

        services.AddSingleton<Features.Common.Interfaces.ISettingsLoadingService, SettingsLoadingService>();

        services.AddSingleton<TaskProgressViewModel>();
        services.AddSingleton<UpdateCheckViewModel>();
        services.AddSingleton<ReviewModeBarViewModel>();
        services.AddSingleton<BuilderModeBarViewModel>();

        services.AddSingleton<PendingRestartViewModel>();

        services.AddSingleton<MainWindowViewModel>();

        services.AddSingleton<MoreMenuViewModel>();

        services.AddTransient<SettingsViewModel>();

        // Optimize ViewModels (Singleton for state preservation during inner navigation)
        // Child VMs registered as IOptimizationFeatureViewModel so OptimizeViewModel
        // receives them via IEnumerable<IOptimizationFeatureViewModel> injection.
        services.AddSingleton<OptimizeViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, SoundOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, UpdateOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, NotificationOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, PrivacyOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, PowerOptimizationsViewModel>();
        services.AddSingleton<IOptimizationFeatureViewModel, GamingOptimizationsViewModel>();

        // Customize ViewModels (Singleton for state preservation during inner navigation)
        // Child VMs registered as ICustomizationFeatureViewModel so CustomizeViewModel
        // receives them via IEnumerable<ICustomizationFeatureViewModel> injection.
        services.AddSingleton<CustomizeViewModel>();
        services.AddSingleton<ICustomizationFeatureViewModel, ExplorerCustomizationsViewModel>();
        services.AddSingleton<ICustomizationFeatureViewModel, StartMenuCustomizationsViewModel>();
        services.AddSingleton<ICustomizationFeatureViewModel, TaskbarCustomizationsViewModel>();
        services.AddSingleton<ICustomizationFeatureViewModel, WindowsThemeCustomizationsViewModel>();

        services.AddSingleton<AdvancedToolsViewModel>();
        services.AddSingleton<WimUtilViewModel>();
        services.AddTransient<AutounattendGeneratorViewModel>();

        services.AddSingleton<IAutounattendXmlGeneratorService, AutounattendXmlGeneratorService>();

        // Concrete VMs for XAML binding; interface aliases for service-layer decoupling.
        services.AddSingleton<WindowsAppsViewModel>();
        services.AddSingleton<IWindowsAppsItemsProvider>(sp => sp.GetRequiredService<WindowsAppsViewModel>());
        services.AddSingleton<ExternalAppsViewModel>();
        services.AddSingleton<IExternalAppsItemsProvider>(sp => sp.GetRequiredService<ExternalAppsViewModel>());
        services.AddSingleton<SoftwareAppsViewModel>();

        return services;
    }
}
