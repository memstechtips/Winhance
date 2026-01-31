using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts an integer value to Visibility.
/// Returns Visible if value > 0, otherwise Collapsed.
/// Supports optional parameter for the threshold value.
/// </summary>
public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        int threshold = 0;
        if (parameter != null && int.TryParse(parameter.ToString(), out int parsedThreshold))
        {
            threshold = parsedThreshold;
        }

        if (value is int intValue)
        {
            return intValue > threshold ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
