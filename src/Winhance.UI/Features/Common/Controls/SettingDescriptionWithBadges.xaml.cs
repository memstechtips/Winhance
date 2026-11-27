using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingDescriptionWithBadges : UserControl
{
    public SettingDescriptionWithBadges()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty DescriptionTextProperty =
        DependencyProperty.Register(
            nameof(DescriptionText),
            typeof(string),
            typeof(SettingDescriptionWithBadges),
            new PropertyMetadata(string.Empty, (d, _) => ((SettingDescriptionWithBadges)d).RenderDescription()));

    public string DescriptionText
    {
        get => (string)GetValue(DescriptionTextProperty);
        set => SetValue(DescriptionTextProperty, value);
    }

    // One phrase of the description drawn as a link with a tooltip; null for plain text.
    public static readonly DependencyProperty LinkProperty =
        DependencyProperty.Register(
            nameof(Link),
            typeof(DescriptionLinkSpec),
            typeof(SettingDescriptionWithBadges),
            new PropertyMetadata(null, (d, _) => ((SettingDescriptionWithBadges)d).RenderDescription()));

    public DescriptionLinkSpec? Link
    {
        get => (DescriptionLinkSpec?)GetValue(LinkProperty);
        set => SetValue(LinkProperty, value);
    }

    public bool ShowsDescription(string? text, DescriptionLinkSpec? link) => !string.IsNullOrEmpty(text) || link is not null;

    // TextBlock.Inlines cannot be bound, so the text is written here. FindName realizes the x:Load block early, before
    // its own binding has run.
    private void RenderDescription()
    {
        if (!ShowsDescription(DescriptionText, Link) || FindName(nameof(DescriptionBlock)) is not TextBlock block)
            return;

        block.Inlines.Clear();
        if (Link is not { } link)
        {
            block.Text = DescriptionText;
            return;
        }

        var parts = DescriptionLink.Locate(DescriptionText, link);
        block.Inlines.Add(new Run { Text = parts.Before });
        block.Inlines.Add(InlineLink.Create(parts.Link, link.Open, link.ToolTip));
        block.Inlines.Add(new Run { Text = parts.After });
    }

    public static readonly DependencyProperty BadgeRowProperty =
        DependencyProperty.Register(
            nameof(BadgeRow),
            typeof(IReadOnlyList<BadgePillState>),
            typeof(SettingDescriptionWithBadges),
            new PropertyMetadata(null));

    public IReadOnlyList<BadgePillState> BadgeRow
    {
        get => (IReadOnlyList<BadgePillState>)GetValue(BadgeRowProperty);
        set => SetValue(BadgeRowProperty, value);
    }

    public static readonly DependencyProperty ShowBadgesProperty =
        DependencyProperty.Register(
            nameof(ShowBadges),
            typeof(bool),
            typeof(SettingDescriptionWithBadges),
            new PropertyMetadata(false));

    public bool ShowBadges
    {
        get => (bool)GetValue(ShowBadgesProperty);
        set => SetValue(ShowBadgesProperty, value);
    }
}
