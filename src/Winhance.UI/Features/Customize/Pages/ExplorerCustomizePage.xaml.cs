using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize.Pages;

public sealed partial class ExplorerCustomizePage : Page
{
    public CustomizeViewModel ViewModel { get; }

    public ExplorerCustomizePage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<CustomizeViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string searchText && !string.IsNullOrWhiteSpace(searchText))
        {
            ViewModel.SearchText = searchText;
        }

        // Lightweight refresh: re-read setting states from the system
        _ = ViewModel.ExplorerViewModel.RefreshSettingStatesAsync();
    }
}
