using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a LogLevel to a color for display in the UI.
    /// </summary>
    public class LogLevelToColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts a LogLevel to a color.
        /// </summary>
        /// <param name="value">The LogLevel value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>A color brush based on the log level.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Error:
                        return new SolidColorBrush(Colors.Red);
                    case LogLevel.Warning:
                        return new SolidColorBrush(Colors.Orange);
                    case LogLevel.Debug:
                        return new SolidColorBrush(Colors.Gray);
                    default:
                        return new SolidColorBrush(Colors.White);
                }
            }
            return new SolidColorBrush(Colors.White);
        }

        /// <summary>
        /// Converts a color back to a LogLevel (not implemented).
        /// </summary>
        /// <param name="value">The color value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The converter parameter.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Not implemented.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}