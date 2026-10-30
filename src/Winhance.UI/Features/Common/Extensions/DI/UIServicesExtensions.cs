using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Autounattend.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.Settings.ViewModels;
using Winhance.UI.Features.SoftwareApps.Services;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.WimUtil.ViewModels;
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
        services.AddSingleton<ISaveFilePicker, SaveFilePicker>();

        services.AddSingleton<IAppSelectionSource, AppSelectionSource>();
        services.AddSingleton<ISelectionSetBuilder, SelectionSetBuilder>();
        services.AddSingleton<ISelectionSaveService, SelectionSaveService>();
        services.AddSingleton<IBuilderSaveService, BuilderSaveService>();
        services.AddSingleton<IBuilderModeEntry, BuilderModeEntry>();
        services.AddSingleton<IInstallConsent, DialogInstallConsent>();

        services.AddSingleton<IApplicationCloseService, ApplicationCloseService>();

        services.AddSingleton<IStartupNotificationService, StartupNotificationService>();

        services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();

        services.AddSingleton<IWindowsVersionFilterService, WindowsVersionFilterService>();
        services.AddSingleton<IHardwareFilterService, HardwareFilterService>();
        services.AddSingleton<ITargetWindowsService, TargetWindowsService>();
        services.AddSingleton<ICatalogScopeProvider, CatalogScopeProvider>();

        services.AddSingleton<IConfigLoadService, ConfigLoadService>();
        services.AddSingleton<IConfigAppSelectionService, ConfigAppSelectionService>();
        services.AddSingleton<IConfigApplicationExecutionService, ConfigApplicationExecutionService>();
        services.AddSingleton<IConfigReviewOrchestrationService, ConfigReviewOrchestrationService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ICoverageSummaryService, CoverageSummaryService>();

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

        // Optimize and Customize pages (Singleton for state preservation during inner navigation).
        // One feature VM per page section, registered from Sections so a new section cannot miss its VM.
        services.AddSingleton<OptimizeViewModel>();
        services.AddSingleton<CustomizeViewModel>();
        foreach (var moduleId in OptimizeViewModel.Sections.Concat(CustomizeViewModel.Sections).Select(s => s.ModuleId))
        {
            if (moduleId == FeatureIds.Power)
                services.AddSingleton<ISettingsFeatureViewModel, PowerOptimizationsViewModel>();
            else
                services.AddSingleton<ISettingsFeatureViewModel>(sp => ActivatorUtilities.CreateInstance<SettingsFeatureViewModel>(sp, moduleId));
        }

        services.AddSingleton<WimUtilSession>();
        services.AddSingleton<WimUtilViewModel>();
        services.AddSingleton<AutounattendViewModel>();
        services.AddSingleton<AutounattendPageViewModel>();

        // Concrete VMs for XAML binding; interface aliases for service-layer decoupling.
        services.AddSingleton<WindowsAppsViewModel>();
        services.AddSingleton<IWindowsAppsItemsProvider>(sp => sp.GetRequiredService<WindowsAppsViewModel>());
        services.AddSingleton<ExternalAppsViewModel>();
        services.AddSingleton<IExternalAppsItemsProvider>(sp => sp.GetRequiredService<ExternalAppsViewModel>());
        services.AddSingleton<SoftwareAppsViewModel>();

        // Tray icon, the background app update check and the profiles it can switch
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<ISystemTrayIcon, SystemTrayIcon>();
        services.AddSingleton<AppUpdateMonitor>();
        services.AddSingleton<TrayController>();

        return services;
    }
}
