using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a boolean IsPrimary value to a Visibility value.
    /// Returns Visible if the value is true, otherwise returns Collapsed.
    /// </summary>
    public class IsPrimaryToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPrimary && isPrimary)
            {
                return Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
