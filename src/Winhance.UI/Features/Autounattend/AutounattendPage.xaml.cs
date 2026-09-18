using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Autounattend.ViewModels;

namespace Winhance.UI.Features.Autounattend;

public sealed partial class AutounattendPage : Page
{
    public AutounattendViewModel ViewModel { get; }

    public AutounattendPage()
    {
        ViewModel = App.Services.GetRequiredService<AutounattendViewModel>();

        this.InitializeComponent();

        this.NavigationCacheMode = NavigationCacheMode.Disabled;
    }
}
