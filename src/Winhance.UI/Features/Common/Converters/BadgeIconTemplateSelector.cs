using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Selects the appropriate icon <see cref="DataTemplate"/> for an InfoBadge pill
/// based on the <see cref="SettingBadgeState"/> value passed as the content.
/// </summary>
/// <remarks>
/// Icon controls are heterogeneous — Recommended/Custom use <c>FluentIcons:SymbolIcon</c>,
/// Default uses <c>PathIcon</c>. This selector keeps that heterogeneity out of the
/// consumer's XAML. Templates are set at resource-declaration time in BadgeStyles.xaml.
/// </remarks>
public partial class BadgeIconTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RecommendedTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? CustomTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not SettingBadgeState state)
        {
            return null;
        }

        return state switch
        {
            SettingBadgeState.Recommended => RecommendedTemplate,
            SettingBadgeState.Default => DefaultTemplate,
            SettingBadgeState.Custom => CustomTemplate,
            _ => null,
        };
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
