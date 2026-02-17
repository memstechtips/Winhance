using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize.Pages;

/// <summary>
/// Detail page for Start Menu customization settings.
/// </summary>
public sealed partial class StartMenuCustomizePage : Page
{
    public CustomizeViewModel ViewModel { get; }

    public StartMenuCustomizePage()
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
        _ = ViewModel.StartMenuViewModel.RefreshSettingStatesAsync();
    }
}
