using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a boolean script status to a descriptive text.
    /// </summary>
    public class ScriptStatusToTextConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a descriptive text.
        /// </summary>
        /// <param name="value">The boolean value indicating if scripts are active.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A descriptive text based on the script status.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive 
                    ? "Winhance Removing Apps" 
                    : "No Active Removals";
            }
            
            return "Unknown Status";
        }
        
        /// <summary>
        /// Converts a descriptive text back to a boolean value.
        /// </summary>
        /// <param name="value">The text to convert back.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A boolean value based on the text.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
