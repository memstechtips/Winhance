using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Returns the value if it's not null or empty, otherwise returns the parameter as a fallback.
/// </summary>
public class CoalesceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return parameter ?? string.Empty;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
