using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace Winhance.UI.Features.SoftwareApps.Converters;

/// <summary>
/// Converts a boolean (IsInstalled) to a color for status indicators.
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isInstalled)
        {
            // Green for installed, Gray for not installed
            return isInstalled
                ? Color.FromArgb(255, 76, 175, 80)   // #4CAF50 - Green
                : Color.FromArgb(255, 158, 158, 158); // #9E9E9E - Gray
        }
        return Color.FromArgb(255, 158, 158, 158);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
