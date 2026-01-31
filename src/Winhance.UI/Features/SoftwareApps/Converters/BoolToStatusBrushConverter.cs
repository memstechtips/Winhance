using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.SoftwareApps.Converters;

/// <summary>
/// Converts a boolean (IsInstalled) to a brush for status indicators.
/// </summary>
public class BoolToStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isInstalled)
        {
            // Green for installed, Gray for not installed
            return isInstalled
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80))   // #4CAF50 - Green
                : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 158, 158, 158)); // #9E9E9E - Gray
        }
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 158, 158, 158)); // Default gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
