using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize.Pages;

/// <summary>
/// Detail page for Explorer customization settings.
/// </summary>
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

        // Apply search filter if passed as parameter
        if (e.Parameter is string searchText && !string.IsNullOrWhiteSpace(searchText))
        {
            ViewModel.SearchText = searchText;
        }
    }
}
