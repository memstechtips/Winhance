using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Selects the appropriate icon <see cref="DataTemplate"/> for an InfoBadge pill
/// based on the <see cref="SettingBadgeKind"/> value passed as the content.
/// </summary>
/// <remarks>
/// Icon controls are heterogeneous — Recommended/Custom use <c>FluentIcons:SymbolIcon</c>,
/// Default uses <c>PathIcon</c>. This selector keeps that heterogeneity out of the
/// consumer's XAML. Templates are set at resource-declaration time in BadgeStyles.xaml.
/// </remarks>
public sealed partial class BadgeIconTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RecommendedTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? CustomTemplate { get; set; }
    public DataTemplate? PreferenceTemplate { get; set; }

    /// <summary>
    /// Pure enum-to-slot mapping, extracted so the branching can be unit-tested
    /// without instantiating <see cref="DataTemplate"/> (which requires a running
    /// WinUI dispatcher and is not available in the xunit host).
    /// </summary>
    public static T? PickByState<T>(
        SettingBadgeKind state,
        T? recommended,
        T? @default,
        T? custom,
        T? preference)
        where T : class
        => state switch
        {
            SettingBadgeKind.Recommended => recommended,
            SettingBadgeKind.Default => @default,
            SettingBadgeKind.Custom => custom,
            SettingBadgeKind.Preference => preference,
            _ => null,
        };

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not SettingBadgeKind state)
        {
            return null;
        }

        return PickByState(state, RecommendedTemplate, DefaultTemplate, CustomTemplate, PreferenceTemplate);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
