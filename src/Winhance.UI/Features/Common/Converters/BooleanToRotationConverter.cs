using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts a boolean to a rotation angle.
/// True returns the parameter angle (default 180), False returns 0.
/// Used for chevron/arrow animations.
/// </summary>
public class BooleanToRotationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double rotationAngle = 180;
        if (parameter != null && double.TryParse(parameter.ToString(), out double parsed))
        {
            rotationAngle = parsed;
        }

        if (value is bool boolValue)
        {
            return boolValue ? rotationAngle : 0.0;
        }

        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
