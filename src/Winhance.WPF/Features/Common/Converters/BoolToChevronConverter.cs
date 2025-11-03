using System;
using System.Globalization;
using System.Windows.Data;
using MahApps.Metro.IconPacks;

namespace Winhance.WPF.Features.Common.Converters
{
    public class BoolToChevronConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? PackIconMaterialKind.ChevronUp : PackIconMaterialKind.ChevronDown;
            }

            return PackIconMaterialKind.ChevronDown;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PackIconMaterialKind kind)
            {
                return kind == PackIconMaterialKind.ChevronUp;
            }

            return false;
        }
    }
}
