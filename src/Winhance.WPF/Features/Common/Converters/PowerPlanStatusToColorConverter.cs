using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Winhance.WPF.Features.Common.Converters
{
    public class PowerPlanStatusToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0, 255, 60));
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Color.FromRgb(255, 40, 0));

        static PowerPlanStatusToColorConverter()
        {
            GreenBrush.Freeze();
            RedBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool existsOnSystem)
            {
                return existsOnSystem ? GreenBrush : RedBrush;
            }
            return RedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
