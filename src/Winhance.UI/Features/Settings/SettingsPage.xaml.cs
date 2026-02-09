using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Settings.ViewModels;

namespace Winhance.UI.Features.Settings;

/// <summary>
/// Settings page for application configuration.
/// </summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>
    /// Gets the ViewModel for this page.
    /// </summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>
    /// Creates a new instance of the SettingsPage.
    /// </summary>
    public SettingsPage()
    {
        // Resolve ViewModel from DI container
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();

        this.InitializeComponent();

        // Settings page is lightweight - no need for caching
        this.NavigationCacheMode = NavigationCacheMode.Disabled;
    }
}
