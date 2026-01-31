using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts an enum value to Visibility based on whether it matches the parameter.
/// Returns Visible if the enum value matches the parameter, otherwise Collapsed.
/// </summary>
public class EnumMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var enumValue = value.ToString();
        var parameterValue = parameter.ToString();

        return enumValue?.Equals(parameterValue, StringComparison.OrdinalIgnoreCase) == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
