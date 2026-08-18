using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

// Collapses itself when the URL is null, empty or not an absolute URI; the raw URL is the tooltip.
public sealed partial class WebsiteLinkButton : UserControl
{
    public WebsiteLinkButton()
    {
        InitializeComponent();

        // Accessible name for the icon-only link. Resolved on Loaded (matching NavButton) so
        // App.Services is available; localized once — automation names don't need live updates.
        Loaded += (_, _) =>
        {
            var localization = App.Services.GetService<ILocalizationService>();
            if (localization is not null)
                AutomationProperties.SetName(LinkButton, localization.GetString("Tooltip_OpenWebsite"));
        };
    }

    public static readonly DependencyProperty UrlProperty =
        DependencyProperty.Register(
            nameof(Url),
            typeof(string),
            typeof(WebsiteLinkButton),
            new PropertyMetadata(null, OnUrlChanged));

    public string? Url
    {
        get => (string?)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    private static void OnUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((WebsiteLinkButton)d).ApplyUrl();

    private void ApplyUrl()
    {
        if (!string.IsNullOrWhiteSpace(Url) && Uri.TryCreate(Url, UriKind.Absolute, out var uri))
        {
            LinkButton.NavigateUri = uri;
            ToolTipService.SetToolTip(LinkButton, Url);
            Visibility = Visibility.Visible;
        }
        else
        {
            LinkButton.NavigateUri = null;
            ToolTipService.SetToolTip(LinkButton, null);
            Visibility = Visibility.Collapsed;
        }
    }
}
