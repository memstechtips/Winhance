using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Winhance.WPF.Features.Common.Converters
{
    public class ScriptStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                // Return green if scripts are active, gray if not
                return isActive
                    ? new SolidColorBrush(Color.FromRgb(0, 200, 83)) // Green for active
                    : new SolidColorBrush(Color.FromRgb(150, 150, 150)); // Gray for inactive
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }
}
