using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a boolean script status to a color.
    /// </summary>
    public class ScriptStatusToColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a color.
        /// </summary>
        /// <param name="value">The boolean value indicating if scripts are active.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A color based on the script status.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                // Return green if scripts are active, gray if not
                return isActive 
                    ? new SolidColorBrush(Color.FromRgb(0, 200, 83))  // Green for active
                    : new SolidColorBrush(Color.FromRgb(150, 150, 150)); // Gray for inactive
            }
            
            return new SolidColorBrush(Colors.Gray);
        }
        
        /// <summary>
        /// Converts a color back to a boolean value.
        /// </summary>
        /// <param name="value">The color to convert back.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A boolean value based on the color.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
