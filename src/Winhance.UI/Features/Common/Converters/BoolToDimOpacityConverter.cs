using System;
using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Maps a highlighted/dim flag to an opacity value. Highlighted (true) returns 1.0; dim (false)
/// returns a constant 0.35. We use a constant rather than a theme-aware value because
/// <c>Application.Current.Resources.TryGetValue</c> can't resolve <c>ThemeDictionaries</c>
/// entries — those resolve only through <c>{ThemeResource}</c> markup on a
/// <see cref="Microsoft.UI.Xaml.FrameworkElement"/>. If light-mode tuning is required later,
/// replace this with <c>CommunityToolkit.WinUI.Behaviors.DataTriggerBehavior</c> setting
/// Opacity to <c>{ThemeResource BadgeDimOpacity}</c> on each pill Border.
/// </summary>
public sealed partial class BoolToDimOpacityConverter : IValueConverter
{
    private const double Highlighted = 1.0;
    private const double Dim = 0.35;

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool isHighlighted ? (isHighlighted ? Highlighted : Dim) : Highlighted;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
