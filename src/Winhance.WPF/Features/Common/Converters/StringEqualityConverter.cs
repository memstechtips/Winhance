using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converter that compares two strings for equality and returns a boolean result
    /// </summary>
    public class StringEqualityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a string value to a boolean by comparing it with the parameter
        /// </summary>
        /// <param name="value">The source string</param>
        /// <param name="targetType">The target type (should be boolean)</param>
        /// <param name="parameter">The comparison string</param>
        /// <param name="culture">The culture info</param>
        /// <returns>True if the strings are equal, otherwise false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Not implemented as this is a one-way converter
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
