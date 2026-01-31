using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts a boolean value to an opacity value.
/// True returns 1.0 (fully visible), False returns 0.5 (semi-transparent).
/// </summary>
public class BooleanToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool isEnabled && isEnabled ? 1.0 : 0.5;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
