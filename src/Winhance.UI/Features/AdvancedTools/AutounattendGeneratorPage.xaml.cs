using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.AdvancedTools.ViewModels;

namespace Winhance.UI.Features.AdvancedTools;

/// <summary>
/// Page for generating autounattend.xml files.
/// </summary>
public sealed partial class AutounattendGeneratorPage : Page
{
    public AutounattendGeneratorViewModel ViewModel { get; }

    public AutounattendGeneratorPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<AutounattendGeneratorViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (App.MainWindow != null)
        {
            ViewModel.SetMainWindow(App.MainWindow);
        }

        // Wire up navigation to WimUtil via parent AdvancedToolsPage
        ViewModel.NavigateToWimUtilRequested += OnNavigateToWimUtilRequested;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.NavigateToWimUtilRequested -= OnNavigateToWimUtilRequested;
    }

    private void OnNavigateToWimUtilRequested(object? sender, EventArgs e)
    {
        // Find parent AdvancedToolsPage via frame hierarchy and navigate to WimUtil
        if (Frame?.Parent is FrameworkElement parentElement)
        {
            var parent = parentElement;
            while (parent != null)
            {
                if (parent is AdvancedToolsPage advancedToolsPage)
                {
                    advancedToolsPage.NavigateToSection("WimUtil");
                    return;
                }
                parent = parent.Parent as FrameworkElement;
            }
        }
    }
}
