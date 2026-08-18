using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Settings.ViewModels;

namespace Winhance.UI.Features.Settings;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        // Resolve ViewModel from DI container
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();

        this.InitializeComponent();

        // Settings page is lightweight - no need for caching
        this.NavigationCacheMode = NavigationCacheMode.Disabled;
    }
}
