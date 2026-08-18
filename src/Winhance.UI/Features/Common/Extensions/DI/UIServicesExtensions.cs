using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
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
        // Dispatcher Service (Singleton - UI thread dispatching)
        // Requires late initialization in MainWindow.xaml.cs after window creation
        services.AddSingleton<IDispatcherService, DispatcherService>();

        // Main Window Provider (Singleton - Abstracts static App.MainWindow access)
        services.AddSingleton<IMainWindowProvider, MainWindowProvider>();

        // Resource Service (Singleton - Abstracts XAML Application.Current.Resources access)
        services.AddSingleton<IResourceService, ResourceService>();

        // Theme Service (Singleton - Application-wide theme management)
        services.AddSingleton<IThemeService, ThemeService>();

        // Dialog Service (Singleton - ContentDialog management with queuing)
        // Requires XamlRoot to be set by MainWindow after content is loaded
        services.AddSingleton<IDialogService, DialogService>();

        // File Picker Service (Singleton - Wraps Win32FileDialogHelper via IMainWindowProvider)
        services.AddSingleton<IFilePickerService, FilePickerService>();

        // Selected Apps Provider (Singleton - Bridges WIM feature to SoftwareApps feature)
        services.AddSingleton<ISelectedAppsProvider, SelectedAppsProvider>();

        // Application Close Service (Singleton - Handles shutdown with the sponsors dialog)
        services.AddSingleton<IApplicationCloseService, ApplicationCloseService>();

        // Startup Notification Service (Singleton - Shows backup notification after startup)
        services.AddSingleton<IStartupNotificationService, StartupNotificationService>();

        // Startup Orchestrator (Singleton - Orchestrates startup phases for testability)
        services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();

        // Windows Version Filter Service (Singleton - Filter state, persistence, review mode)
        services.AddSingleton<IWindowsVersionFilterService, WindowsVersionFilterService>();

        // Configuration Services (Singleton - Import/Export/Review)
        services.AddSingleton<IConfigExportService, ConfigExportService>();
        services.AddSingleton<IConfigLoadService, ConfigLoadService>();
        services.AddSingleton<IConfigAppSelectionService, ConfigAppSelectionService>();
        services.AddSingleton<IConfigApplicationExecutionService, ConfigApplicationExecutionService>();
        services.AddSingleton<IConfigReviewOrchestrationService, ConfigReviewOrchestrationService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Config Import Overlay Service
        services.AddSingleton<IConfigImportOverlayService, ConfigImportOverlayService>();

        // Config Review Mode Service (Singleton - state persists across page navigation)
        services.AddSingleton<IConfigReviewService, ConfigReviewService>();
        services.AddSingleton<IConfigReviewModeService>(sp => (IConfigReviewModeService)sp.GetRequiredService<IConfigReviewService>());
        services.AddSingleton<IConfigReviewDiffService>(sp => (IConfigReviewDiffService)sp.GetRequiredService<IConfigReviewService>());
        services.AddSingleton<IConfigReviewBadgeService>(sp => (IConfigReviewBadgeService)sp.GetRequiredService<IConfigReviewService>());
        services.AddSingleton<IApplicationModeService>(sp => (IApplicationModeService)sp.GetRequiredService<IConfigReviewService>());

        // Nav Badge Service (Singleton - Computes nav badge state during review mode)
        services.AddSingleton<INavBadgeService, NavBadgeService>();

        // Regedit Launcher (Singleton - Opens regedit at registry paths)
        services.AddSingleton<IRegeditLauncher, Winhance.UI.Features.Common.Utilities.RegeditLauncher>();

        // Setting Localization Service (Singleton - Localizes setting definitions)
        services.AddSingleton<ISettingLocalizationService, SettingLocalizationService>();

        // Setting Review Diff Applier (Singleton - Applies review diffs to ViewModels)
        services.AddSingleton<ISettingReviewDiffApplier, SettingReviewDiffApplier>();

        // Review Mode ViewModel Coordinator (Singleton - abstracts concrete ViewModel dependencies)
        services.AddSingleton<IReviewModeViewModelCoordinator, ReviewModeViewModelCoordinator>();

        // Per-mode setting write strategies. One implementation per thing a mode can do with an
        // edit - apply it, author it, refuse it - selected from the mode's declared capabilities so
        // that a new mode gets the right one without a second table to keep in sync.
        services.AddSingleton<LiveSettingWriteStrategy>();
        services.AddSingleton<BuilderSettingWriteStrategy>();
        services.AddSingleton<ReadOnlySettingWriteStrategy>();
        services.AddSingleton<ISettingWriteStrategySelector, SettingWriteStrategySelector>();

        // Setting ViewModel Dependencies (parameter object grouping pass-through deps for SettingItemViewModel)
        services.AddSingleton<SettingViewModelDependencies>();

        // Setting ViewModel Enricher (hardware detection, cross-group info, review diff)
        services.AddSingleton<ISettingViewModelEnricher, SettingViewModelEnricher>();

        // Setting ViewModel Factory (Singleton - Creates fully-configured setting ViewModels)
        services.AddSingleton<ISettingViewModelFactory, SettingViewModelFactory>();

        // Settings Loading Service (Singleton - Orchestrates setting loading and refresh)
        services.AddSingleton<Features.Common.Interfaces.ISettingsLoadingService, SettingsLoadingService>();

        // MainWindow child ViewModels (Singleton - composed into MainWindowViewModel)
        services.AddSingleton<TaskProgressViewModel>();
        services.AddSingleton<UpdateCheckViewModel>();
        services.AddSingleton<ReviewModeBarViewModel>();
        services.AddSingleton<BuilderModeBarViewModel>();

        // Pending Explorer restart bar (Singleton - one bar, observed across every page)
        services.AddSingleton<PendingRestartViewModel>();

        // MainWindow ViewModel (Singleton - one main window)
        services.AddSingleton<MainWindowViewModel>();

        // More Menu ViewModel (Singleton - shared menu state)
        services.AddSingleton<MoreMenuViewModel>();

        // Settings ViewModels
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

        // AdvancedTools ViewModels
        services.AddSingleton<AdvancedToolsViewModel>();
        services.AddSingleton<WimUtilViewModel>();
        services.AddTransient<AutounattendGeneratorViewModel>();

        // AdvancedTools Services
        services.AddSingleton<IAutounattendXmlGeneratorService, AutounattendXmlGeneratorService>();

        // SoftwareApps ViewModels (Singleton - shared between UI and ConfigurationService)
        // Concrete VMs for XAML binding; interface aliases for service-layer decoupling (F-9)
        services.AddSingleton<WindowsAppsViewModel>();
        services.AddSingleton<IWindowsAppsItemsProvider>(sp => sp.GetRequiredService<WindowsAppsViewModel>());
        services.AddSingleton<ExternalAppsViewModel>();
        services.AddSingleton<IExternalAppsItemsProvider>(sp => sp.GetRequiredService<ExternalAppsViewModel>());
        services.AddSingleton<SoftwareAppsViewModel>();

        return services;
    }
}
