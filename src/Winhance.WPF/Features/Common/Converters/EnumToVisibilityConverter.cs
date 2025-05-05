using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts an enum value to a Visibility value based on whether it matches the parameter.
    /// </summary>
    public class EnumToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts an enum value to a Visibility value.
        /// </summary>
        /// <param name="value">The enum value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The parameter to compare against.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// Visibility.Visible if the value matches the parameter; otherwise, Visibility.Collapsed.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                return Visibility.Collapsed;
            }

            // Check if the value is equal to the parameter
            bool isEqual = value.Equals(parameter);
            return isEqual ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a Visibility value back to an enum value.
        /// </summary>
        /// <param name="value">The Visibility value to convert back.</param>
        /// <param name="targetType">The type of the binding source property.</param>
        /// <param name="parameter">The parameter to use in the converter.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// The parameter if the value is Visibility.Visible; otherwise, null.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility && visibility == Visibility.Visible)
            {
                return parameter;
            }

            return Binding.DoNothing;
        }
    }
}
