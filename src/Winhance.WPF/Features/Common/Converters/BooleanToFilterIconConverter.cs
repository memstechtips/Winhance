using System;
using System.Globalization;
using System.Windows.Data;
using MahApps.Metro.IconPacks;

namespace Winhance.WPF.Features.Common.Converters
{
    public class BooleanToFilterIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled ? PackIconMaterialKind.FilterCheck : PackIconMaterialKind.FilterOff;
            }
            return PackIconMaterialKind.FilterCheck;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
