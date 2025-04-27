using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace Winhance.WPF.Features.Common.Converters
{
    public class BoolToChevronConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? PackIconKind.ChevronUp : PackIconKind.ChevronDown;
            }
            
            return PackIconKind.ChevronDown;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PackIconKind kind)
            {
                return kind == PackIconKind.ChevronUp;
            }
            
            return false;
        }
    }
}
