using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Winhance.WPF.Converters
{
    public class BooleanToReinstallableColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush BlueBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Color.FromRgb(255, 40, 0));
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(Colors.Gray);

        static BooleanToReinstallableColorConverter()
        {
            BlueBrush.Freeze();
            RedBrush.Freeze();
            GrayBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool canBeReinstalled)
            {
                return canBeReinstalled ? BlueBrush : RedBrush;
            }
            return GrayBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}