using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentValue && parameter is string targetString && int.TryParse(targetString, out int targetValue))
            {
                return currentValue == targetValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
