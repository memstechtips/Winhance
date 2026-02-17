using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize.Pages;

/// <summary>
/// Detail page for Windows Theme customization settings.
/// </summary>
public sealed partial class WindowsThemeCustomizePage : Page
{
    public CustomizeViewModel ViewModel { get; }

    public WindowsThemeCustomizePage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<CustomizeViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Apply search filter if passed as parameter
        if (e.Parameter is string searchText && !string.IsNullOrWhiteSpace(searchText))
        {
            ViewModel.SearchText = searchText;
        }

        // Lightweight refresh: re-read setting states from the system
        _ = ViewModel.WindowsThemeViewModel.RefreshSettingStatesAsync();
    }
}
