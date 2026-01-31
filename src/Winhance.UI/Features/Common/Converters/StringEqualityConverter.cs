using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converter that compares two strings for equality and returns a boolean result.
/// </summary>
public class StringEqualityConverter : IValueConverter
{
    /// <summary>
    /// Converts a string value to a boolean by comparing it with the parameter.
    /// </summary>
    /// <param name="value">The source string.</param>
    /// <param name="targetType">The target type (should be boolean).</param>
    /// <param name="parameter">The comparison string.</param>
    /// <param name="language">The language.</param>
    /// <returns>True if the strings are equal, otherwise false.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null || parameter == null)
            return false;

        return value.ToString()?.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
