using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Settings.ViewModels;

namespace Winhance.UI.Features.Settings;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();

        this.InitializeComponent();

        // Settings page is lightweight - no need for caching
        this.NavigationCacheMode = NavigationCacheMode.Disabled;
        ScrollMemory.Keep(nameof(SettingsPage), PageScrollViewer);

        Loaded += (_, _) =>
        {
            if (App.MainWindow is { } window)
                window.Activated += OnWindowActivated;
        };
        Unloaded += (_, _) =>
        {
            if (App.MainWindow is { } window)
                window.Activated -= OnWindowActivated;
            ViewModel.Dispose();
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RecheckTaskSafety();
    }

    // A user can break the folder lock while Settings stays open, so the note is re-read each time they come back.
    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
            ViewModel.RecheckTaskSafety();
    }
}
