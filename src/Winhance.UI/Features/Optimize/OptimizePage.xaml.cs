using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Optimize;

/// <summary>
/// Page for Windows optimization settings (Sound, Update, Notifications, Privacy, Power, Gaming).
/// </summary>
public sealed partial class OptimizePage : Page
{
    public OptimizeViewModel ViewModel { get; }

    public OptimizePage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();
        this.NavigationCacheMode = NavigationCacheMode.Required;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }
}
