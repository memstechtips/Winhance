using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

// A constant rather than a theme-aware value: Application.Current.Resources.TryGetValue can't resolve
// ThemeDictionaries entries - those resolve only through {ThemeResource} markup on a FrameworkElement. If
// light-mode tuning is ever needed, use a DataTriggerBehavior setting Opacity to {ThemeResource BadgeDimOpacity}
// on each pill Border.
public sealed partial class BoolToDimOpacityConverter : IValueConverter
{
    private const double Highlighted = 1.0;
    private const double Dim = 0.35;

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool isHighlighted ? (isHighlighted ? Highlighted : Dim) : Highlighted;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
