using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts null values to Collapsed visibility and non-null values to Visible.
/// This is the inverse of NullToVisibilityConverter.
/// </summary>
public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value == null || string.IsNullOrWhiteSpace(value?.ToString())
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
