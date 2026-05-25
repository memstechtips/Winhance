using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.AdvancedTools;

/// <summary>
/// Page for generating autounattend.xml files.
/// </summary>
public sealed partial class AutounattendGeneratorPage : Page
{
    private readonly ILocalizationService? _localizationService;

    public AutounattendGeneratorViewModel ViewModel { get; }

    public AutounattendGeneratorPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<AutounattendGeneratorViewModel>();
        _localizationService = App.Services.GetService<ILocalizationService>();

        // PageUp/PageDown fast-scroll + Home/End jump (issue #581).
        PageScrollHelper.Attach(this, PageScrollView);
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

        // Live-region announcements for screen readers (issue #647).
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.NavigateToWimUtilRequested -= OnNavigateToWimUtilRequested;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.IsGenerating)) return;

        var message = ViewModel.IsGenerating
            ? _localizationService?.GetString("Accessibility_GeneratingXml") ?? "Generating XML..."
            : _localizationService?.GetString("Accessibility_GenerationComplete") ?? "XML generation complete";
        Announce(message);
    }

    private void Announce(string message)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(this)
                   ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);

        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            message,
            "AutounattendGenerator");
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
