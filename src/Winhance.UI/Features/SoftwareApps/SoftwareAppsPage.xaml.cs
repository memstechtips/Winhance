using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.SoftwareApps;

/// <summary>
/// Page for managing Windows packages and installing external software.
/// </summary>
public sealed partial class SoftwareAppsPage : Page
{
    public SoftwareAppsViewModel ViewModel { get; }

    public SoftwareAppsPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SoftwareAppsViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }
}
