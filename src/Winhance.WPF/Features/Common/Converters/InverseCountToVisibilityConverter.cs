using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts an integer count to a Visibility value, with the count inverted.
    /// Returns Visible if the count is 0, otherwise returns Collapsed.
    /// </summary>
    public class InverseCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
