using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    public class BooleanToFilterTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled
                    ? "Windows Version Filter: ON\nClick to show settings for all Windows versions"
                    : "Windows Version Filter: OFF\nShowing all settings (incompatible settings marked)";
            }
            return "Toggle Windows Version Filter";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
