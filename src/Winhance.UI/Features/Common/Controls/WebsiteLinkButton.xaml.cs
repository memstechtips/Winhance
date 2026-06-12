using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// A reusable "visit website" link button (native <see cref="HyperlinkButton"/>) shared by the
/// Software &amp; Apps card, table and compact views. Set <see cref="Url"/>; the control converts
/// it to a <see cref="Uri"/> for navigation, shows the raw URL as its tooltip, and collapses
/// itself when the URL is null, empty or not an absolute URI.
/// </summary>
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

    /// <summary>The website URL to open. The control hides itself when this is null/empty/invalid.</summary>
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
