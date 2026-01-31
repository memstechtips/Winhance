using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts a boolean to one of two values specified in the parameter.
/// Parameter format: "TrueValue|FalseValue"
/// </summary>
public class BooleanToValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not bool boolValue)
            return parameter;

        var paramString = parameter?.ToString() ?? string.Empty;
        var parts = paramString.Split('|');

        if (parts.Length >= 2)
        {
            return boolValue ? parts[0] : parts[1];
        }

        return boolValue ? paramString : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
