using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.SoftwareApps.Converters;

/// <summary>
/// Converts a boolean (CanBeReinstalled) to a brush for the reinstallable icon.
/// </summary>
public class BoolToReinstallBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool canBeReinstalled)
        {
            // Blue for can reinstall, Red for cannot
            return canBeReinstalled
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243))  // #2196F3 - Blue
                : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54));  // #F44336 - Red
        }
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54)); // Default to red
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
