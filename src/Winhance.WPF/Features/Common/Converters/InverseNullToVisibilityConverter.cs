using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts null values to Collapsed visibility and non-null values to Visible.
    /// This is the inverse of NullToVisibilityConverter.
    /// Used to hide UI elements when their bound data is null or empty.
    /// </summary>
    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Return Collapsed if value is null or empty string, otherwise Visible
            return value == null || string.IsNullOrWhiteSpace(value?.ToString())
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}