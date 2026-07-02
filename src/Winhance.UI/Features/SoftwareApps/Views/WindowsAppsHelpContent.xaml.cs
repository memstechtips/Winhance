using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.SoftwareApps.Views;

public sealed partial class WindowsAppsHelpContent : UserControl
{
    private readonly ILocalizationService _localizationService;

    public WindowsAppsHelpContent(ILocalizationService localizationService)
    {
        this.InitializeComponent();
        _localizationService = localizationService;

        InstalledText.Text = localizationService.GetString("Status_Installed");
        NotInstalledText.Text = localizationService.GetString("Status_NotInstalled");
        CanReinstallText.Text = localizationService.GetString("Status_CanReinstall");
        CannotReinstallText.Text = localizationService.GetString("Status_CannotReinstall");
        WarningText.Text = localizationService.GetString("Card_Pill_Warning");
        WinhanceStatusLabel.Text = localizationService.GetString("Help_WinhanceStatus");
        HelpContentText.Text = localizationService.GetString("Help_WindowsApps_Content");

        LearnMoreLink.Content = localizationService.GetString("Help_LearnMore_WindowsApps");
        LearnMoreLink.NavigateUri = new System.Uri("https://winhance.net/docs/features/software-apps/windows-apps.html");
    }

    private void RemovalButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not RemovalStatusViewModel item)
            return;

        var pathIcon = FindDescendant<PathIcon>(button);
        if (pathIcon == null) return;

        // Resolve the icon geometry from FeatureIcons resources
        if (Application.Current.Resources.TryGetValue(item.IconPath, out var pathData) && pathData is string pathString)
        {
            pathIcon.Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), pathString);
        }

        // Apply initial color
        UpdateIconColor(pathIcon, item);

        bool wasLoading = item.IsLoading;
        item.PropertyChanged += (s, args) =>
        {
            // Update icon color when IsActive flips.
            if (args.PropertyName == nameof(RemovalStatusViewModel.IsActive))
            {
                DispatcherQueue.TryEnqueue(() => UpdateIconColor(pathIcon, item));
            }

            // Live-region announcement on removal start / finish (issue #647).
            if (args.PropertyName == nameof(RemovalStatusViewModel.IsLoading))
            {
                if (item.IsLoading && !wasLoading)
                {
                    var template = _localizationService.GetString("Accessibility_Removing") ?? "Removing {0}";
                    DispatcherQueue.TryEnqueue(() => Announce(string.Format(template, item.ScheduledTaskName)));
                }
                else if (!item.IsLoading && wasLoading)
                {
                    var template = _localizationService.GetString("Accessibility_Removed") ?? "{0} removed";
                    DispatcherQueue.TryEnqueue(() => Announce(string.Format(template, item.ScheduledTaskName)));
                }
                wasLoading = item.IsLoading;
            }
        };
    }

    private void Announce(string message)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(this)
                   ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);

        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            message,
            "WindowsAppsRemoval");
    }

    private static void UpdateIconColor(PathIcon pathIcon, RemovalStatusViewModel item)
    {
        if (item.IsActive)
        {
            var hex = item.ActiveColor.TrimStart('#');
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            pathIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
        }
        else
        {
            pathIcon.Foreground = (Brush)Application.Current.Resources["TextFillColorDisabledBrush"];
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
                return found;
            var descendant = FindDescendant<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }
}
