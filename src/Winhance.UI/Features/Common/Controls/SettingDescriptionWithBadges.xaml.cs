using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Reusable content for <c>SettingsCard.Description</c>: shows the setting's description
/// text on top and the multi-badge row beneath. Used across SettingItemTemplate,
/// SettingExpanderItemTemplate, and the inner grouped template in SettingTemplates.xaml
/// to eliminate the previous triplicated StackPanel+ItemsControl.
/// </summary>
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
            new PropertyMetadata(string.Empty));

    public string DescriptionText
    {
        get => (string)GetValue(DescriptionTextProperty);
        set => SetValue(DescriptionTextProperty, value);
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
