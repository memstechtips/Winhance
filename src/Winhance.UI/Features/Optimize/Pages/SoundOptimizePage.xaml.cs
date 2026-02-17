using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Optimize.Pages;

/// <summary>
/// Detail page for Sound optimization settings.
/// </summary>
public sealed partial class SoundOptimizePage : Page
{
    public OptimizeViewModel ViewModel { get; }

    public SoundOptimizePage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();
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
        _ = ViewModel.SoundViewModel.RefreshSettingStatesAsync();
    }
}
