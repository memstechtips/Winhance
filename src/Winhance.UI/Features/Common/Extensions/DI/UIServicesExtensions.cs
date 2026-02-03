using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.Settings.ViewModels;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Common.Extensions.DI;

/// <summary>
/// Extension methods for registering UI-specific services.
/// </summary>
public static class UIServicesExtensions
{
    /// <summary>
    /// Registers UI-specific services for the Winhance WinUI 3 application.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddUIServices(this IServiceCollection services)
    {
        // Dispatcher Service (Singleton - UI thread dispatching)
        // Requires late initialization in MainWindow.xaml.cs after window creation
        services.AddSingleton<IDispatcherService, DispatcherService>();

        // Theme Service (Singleton - Application-wide theme management)
        services.AddSingleton<IThemeService, ThemeService>();

        // Dialog Service (Singleton - ContentDialog management with queuing)
        // Requires XamlRoot to be set by MainWindow after content is loaded
        services.AddSingleton<IDialogService, DialogService>();

        // Configuration Service (Singleton - Import/Export configuration files)
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Setting Localization Service (Singleton - Localizes setting definitions)
        services.AddSingleton<ISettingLocalizationService, SettingLocalizationService>();

        // Settings Loading Service (Singleton - Creates setting ViewModels)
        services.AddSingleton<Features.Common.Interfaces.ISettingsLoadingService, SettingsLoadingService>();

        // MainWindow ViewModel (Singleton - one main window)
        services.AddSingleton<MainWindowViewModel>();

        // More Menu ViewModel (Singleton - shared menu state)
        services.AddSingleton<MoreMenuViewModel>();

        // Settings ViewModels
        services.AddTransient<SettingsViewModel>();

        // Optimize ViewModels (Transient - created per page)
        services.AddTransient<OptimizeViewModel>();
        services.AddTransient<SoundOptimizationsViewModel>();
        services.AddTransient<UpdateOptimizationsViewModel>();
        services.AddTransient<NotificationOptimizationsViewModel>();
        services.AddTransient<PrivacyOptimizationsViewModel>();
        services.AddTransient<PowerOptimizationsViewModel>();
        services.AddTransient<GamingOptimizationsViewModel>();

        // Customize ViewModels (Singleton for state preservation during inner navigation)
        services.AddSingleton<CustomizeViewModel>();
        services.AddSingleton<ExplorerCustomizationsViewModel>();
        services.AddSingleton<StartMenuCustomizationsViewModel>();
        services.AddSingleton<TaskbarCustomizationsViewModel>();
        services.AddSingleton<WindowsThemeCustomizationsViewModel>();

        // AdvancedTools ViewModels (Transient - created per page)
        services.AddTransient<WimUtilViewModel>();

        // SoftwareApps ViewModels (Transient - created per page)
        services.AddTransient<WindowsAppsViewModel>();
        services.AddTransient<ExternalAppsViewModel>();
        services.AddTransient<SoftwareAppsViewModel>();

        return services;
    }
}
