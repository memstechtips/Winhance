using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Picks one of four pill templates by <see cref="BadgePillState.Kind"/>. Each template
/// renders a complete pill (Border, icon, label, tooltip) with opacity bound to
/// <see cref="BadgePillState.IsHighlighted"/> via <c>BoolToDimOpacityConverter</c>.
/// </summary>
public sealed partial class BadgePillTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RecommendedTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? CustomTemplate { get; set; }
    public DataTemplate? PreferenceTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not BadgePillState pill) return null;
        return PickByKind(pill.Kind, RecommendedTemplate, DefaultTemplate, CustomTemplate, PreferenceTemplate);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);

    /// <summary>
    /// Pure switch-on-kind helper — test-friendly (no WinUI dispatcher required).
    /// Returns the slot corresponding to the kind, or null if the enum value is unknown.
    /// </summary>
    public static T? PickByKind<T>(SettingBadgeKind kind, T? recommended, T? @default, T? custom, T? preference)
        where T : class
        => kind switch
        {
            SettingBadgeKind.Recommended => recommended,
            SettingBadgeKind.Default     => @default,
            SettingBadgeKind.Custom      => custom,
            SettingBadgeKind.Preference  => preference,
            _                            => null,
        };
}
