using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    public class WidthToColumnsConverter : IValueConverter
    {
        private const double MinItemWidth = 260;
        private const double MaxItemWidth = 320;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && width > 0)
            {
                int columns = Math.Max(1, (int)(width / MinItemWidth));

                if (width / columns > MaxItemWidth)
                {
                    columns++;
                }

                return columns;
            }
            return 4;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
