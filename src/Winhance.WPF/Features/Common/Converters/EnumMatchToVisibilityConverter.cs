using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts an enum value to Visibility.Visible if it matches the specified value, otherwise Visibility.Collapsed.
    /// </summary>
    public class EnumMatchToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Gets or sets the enum value that should result in Visibility.Visible.
        /// </summary>
        public object MatchValue { get; set; }

        /// <summary>
        /// Converts an enum value to a Visibility value.
        /// </summary>
        /// <param name="value">The enum value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>Visibility.Visible if the value matches the specified value, otherwise Visibility.Collapsed.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            // If parameter is provided, use it instead of MatchValue
            object compareValue = parameter ?? MatchValue;

            // Compare the enum values as integers if they are enums
            if (value is Enum && compareValue is Enum)
            {
                return System.Convert.ToInt32(value) == System.Convert.ToInt32(compareValue)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            return Equals(value, compareValue)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a Visibility value back to an enum value.
        /// </summary>
        /// <param name="value">The Visibility value to convert back.</param>
        /// <param name="targetType">The type of the binding source property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>The match value if Visibility.Visible, otherwise null.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility && visibility == Visibility.Visible)
                return parameter ?? MatchValue;

            return null;
        }
    }
}
