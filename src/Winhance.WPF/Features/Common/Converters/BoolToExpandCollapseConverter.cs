using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a boolean value to "Collapse" or "Expand" text.
    /// </summary>
    public class BoolToExpandCollapseConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to "Collapse" or "Expand" text.
        /// </summary>
        /// <param name="value">The boolean value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>"Collapse" if true, "Expand" if false.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "Collapse" : "Expand";
            }

            return "Expand";
        }

        /// <summary>
        /// Converts a string value back to a boolean value.
        /// </summary>
        /// <param name="value">The string value to convert back.</param>
        /// <param name="targetType">The type of the binding source property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>True if "Collapse", false if "Expand".</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text == "Collapse";
            }

            return false;
        }
    }
}
