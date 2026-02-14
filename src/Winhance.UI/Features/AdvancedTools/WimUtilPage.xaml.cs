using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.AdvancedTools.ViewModels;

namespace Winhance.UI.Features.AdvancedTools;

/// <summary>
/// WIM Utility page for creating custom Windows installation images.
/// </summary>
public sealed partial class WimUtilPage : Page
{
    public WimUtilViewModel ViewModel { get; }

    public WimUtilPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<WimUtilViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Set main window reference for file dialogs
        if (App.MainWindow != null)
        {
            ViewModel.SetMainWindow(App.MainWindow);
        }

        // Initialize the ViewModel
        await ViewModel.OnNavigatedToAsync();
    }

    private void Step1_Header_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.NavigateToStepCommand.Execute("1");
    }

    private void Step2_Header_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.NavigateToStepCommand.Execute("2");
    }

    private void Step3_Header_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.NavigateToStepCommand.Execute("3");
    }

    private void Step4_Header_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.NavigateToStepCommand.Execute("4");
    }

    private void Windows10Download_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenWindows10DownloadCommand.Execute(null);
    }

    private void Windows11Download_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenWindows11DownloadCommand.Execute(null);
    }

    private void SchneegansLink_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenSchneegansXmlGeneratorCommand.Execute(null);
    }
}
