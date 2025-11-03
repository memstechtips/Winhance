using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a boolean value to one of two possible values based on a parameter.
    /// The parameter should be in the format "TrueValue|FalseValue".
    /// </summary>
    public class BooleanToValueConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to one of two possible values.
        /// </summary>
        /// <param name="value">The boolean value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">A string in the format "TrueValue|FalseValue".</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>TrueValue if value is true, FalseValue otherwise.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool boolValue))
                return null;

            if (!(parameter is string paramString))
                return null;

            string[] values = paramString.Split('|');
            if (values.Length != 2)
                return null;

            return boolValue ? values[0] : values[1];
        }

        /// <summary>
        /// Converts back from the target value to the source value.
        /// </summary>
        /// <param name="value">The value that is produced by the binding target.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A converted value.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(parameter is string paramString))
                return null;

            string[] values = paramString.Split('|');
            if (values.Length != 2)
                return null;

            if (value is string stringValue)
            {
                return stringValue == values[0];
            }

            return null;
        }
    }
}
