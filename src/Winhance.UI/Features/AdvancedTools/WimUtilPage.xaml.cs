using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Helpers;

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
        ActualThemeChanged += (_, _) => UpdateWinhanceXmlCardIcon();
        UpdateWinhanceXmlCardIcon();

        // PageUp/PageDown fast-scroll + Home/End jump (issue #581).
        PageScrollHelper.Attach(this, PageScrollView);
    }

    private void UpdateWinhanceXmlCardIcon()
    {
        var uri = ActualTheme == ElementTheme.Light
            ? "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png"
            : "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png";
        WinhanceXmlCardIcon.Source = new BitmapImage(new Uri(uri));
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
