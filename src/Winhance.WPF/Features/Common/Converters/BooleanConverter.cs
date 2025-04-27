using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// A simple boolean converter that returns the parameter when true, and null when false.
    /// Useful for conditional resource selection.
    /// </summary>
    public class BooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? parameter : null;
            }
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }
    }
}
