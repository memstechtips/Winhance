using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Converters;

public sealed partial class BadgePillTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RecommendedTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? PreferenceTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not BadgePillState pill) return null;
        return PickByKind(pill.Kind, RecommendedTemplate, DefaultTemplate, PreferenceTemplate);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);

    // Pure switch-on-kind so it is testable without a WinUI dispatcher; null for an unknown kind.
    public static T? PickByKind<T>(SettingBadgeKind kind, T? recommended, T? @default, T? preference)
        where T : class
        => kind switch
        {
            SettingBadgeKind.Recommended => recommended,
            SettingBadgeKind.Default     => @default,
            SettingBadgeKind.Preference  => preference,
            _                            => null,
        };
}
