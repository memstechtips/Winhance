using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize;

/// <summary>
/// Page for customizing Windows appearance and behavior.
/// </summary>
public sealed partial class CustomizePage : Page
{
    public CustomizeViewModel ViewModel { get; }

    public CustomizePage()
    {
        this.InitializeComponent();

        // Get ViewModel from DI
        ViewModel = App.Services.GetRequiredService<CustomizeViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Initialize ViewModel when navigating to this page
        await ViewModel.InitializeAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        // Clear search when navigating away
        ViewModel.OnNavigatedFrom();
    }
}
