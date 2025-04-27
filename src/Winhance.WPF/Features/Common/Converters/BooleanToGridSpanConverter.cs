using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Converters
{
    /// <summary>
    /// Converts a boolean value to an integer grid span - true returns 4 (full width), false returns 1
    /// </summary>
    public class BooleanToGridSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? 4 : 1; // Return 4 for true (header spans full width), 1 for false
            }
            return 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}