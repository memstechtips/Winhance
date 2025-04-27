using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a boolean value to a Visibility value, with the boolean value inverted.
    /// True becomes Collapsed, False becomes Visible.
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a Visibility value, with the boolean value inverted.
        /// </summary>
        /// <param name="value">The boolean value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>Visibility.Collapsed if the value is true, Visibility.Visible if the value is false.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Visible;
        }
        
        /// <summary>
        /// Converts a Visibility value back to a boolean value, with the boolean value inverted.
        /// </summary>
        /// <param name="value">The Visibility value to convert back.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>False if the value is Visibility.Visible, true otherwise.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            
            return false;
        }
    }
}
